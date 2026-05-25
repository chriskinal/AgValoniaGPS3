// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AgValoniaGPS.Models.Navigation;
using AgValoniaGPS.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Page-navigation glue. Owns the observable <see cref="CurrentPage"/>
/// mirror of <see cref="INavigationService.CurrentPage"/> and exposes
/// the commands the AppShell tabs + Home button bind to.
///
/// See Plans/PageBasedNavigation/PLAN.md.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMovingMapPage))]
    [NotifyPropertyChangedFor(nameof(IsNotMovingMapPage))]
    [NotifyPropertyChangedFor(nameof(ShowsAppShellTabBar))]
    private PageType _currentPage = PageType.Home;

    /// <summary>
    /// PNG bytes of the latest map thumbnail, captured when the operator
    /// leaves the Moving Map page. The Home page's Map card binds to this
    /// (decoded to a Bitmap in the View via BytesToBitmapConverter). Null
    /// until the map has been viewed at least once this session.
    /// </summary>
    [ObservableProperty]
    private byte[]? _mapThumbnailPng;

    /// <summary>
    /// PNG thumbnail of the last field/job, frozen at the moment the field
    /// is closed or switched (see CloseFieldAsync) from whatever the map last
    /// showed. The Home page's Resume Last Job card binds to this so the
    /// preview reflects the job you'd resume, not the current map.
    /// </summary>
    [ObservableProperty]
    private byte[]? _lastJobThumbnailPng;

    // Max thumbnail dimension in pixels. ~360 covers a half-width Home card
    // crisply on tablet without an expensive full-res PNG encode.
    private const int MapThumbnailMaxDim = 360;

    /// <summary>
    /// True iff the current page is MovingMap. AppShell uses this to
    /// collapse its full chrome and surface only the floating Home
    /// button, letting the v1 map+panels layout show through.
    /// </summary>
    public bool IsMovingMapPage => CurrentPage == PageType.MovingMap;
    public bool IsNotMovingMapPage => CurrentPage != PageType.MovingMap;

    /// <summary>
    /// Whether the AppShell's bottom tab bar should be visible. Pages
    /// that host their own bottom navigation (Tractor today; Implement /
    /// FieldsAndJobs in time) hide the main bar to reclaim vertical
    /// space on tablet viewports — those pages provide a Home button
    /// in their own header so the user can navigate away.
    /// </summary>
    public bool ShowsAppShellTabBar =>
        CurrentPage != PageType.MovingMap &&
        CurrentPage != PageType.Tractor;

    /// <summary>
    /// Wires the observable <see cref="CurrentPage"/> mirror to the
    /// navigation service. Called from the constructor once the
    /// service is available.
    /// </summary>
    private void InitializeNavigation(INavigationService navigationService)
    {
        CurrentPage = navigationService.CurrentPage;
        navigationService.CurrentPageChanged += (_, page) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var previous = CurrentPage;
                CurrentPage = page;
                // Capture a fresh map thumbnail when leaving the Moving Map
                // page so the Home Map card shows what was last on screen.
                if (previous == PageType.MovingMap && page != PageType.MovingMap)
                    _mapService.RequestMapSnapshot(MapThumbnailMaxDim);
            });

        // Snapshot bytes arrive (already on the UI thread) after the render
        // thread captures them; mirror into the bound property.
        _mapService.MapSnapshotCaptured += (_, png) => MapThumbnailPng = png;

        // Restore the last-job thumbnail + camera framing persisted from a
        // prior session so the Resume Last Job card previews correctly and
        // Resume restores the same zoom/center the operator left.
        LoadPersistedLastJobThumbnail();
        LoadLastJobView();

        // Seed the Map card with a snapshot of the current map shortly after
        // launch (once the map control is registered and has rendered a
        // frame). The request persists in the handler until a real frame is
        // drawn, so the 1.5s delay only needs to clear control registration.
        SeedMapThumbnailOnStartup();
    }

    private void SeedMapThumbnailOnStartup()
    {
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _mapService.RequestMapSnapshot(MapThumbnailMaxDim);
        };
        timer.Start();
    }

    /// <summary>
    /// Requests a fresh map snapshot and awaits its delivery, returning the
    /// PNG bytes (or null on timeout). Used at field close/switch to capture
    /// the open field's appearance regardless of whether the operator ever
    /// visited the Moving Map page — the map control is always mounted and
    /// renders the loaded field underneath the page chrome.
    /// </summary>
    public Task<byte[]?> CaptureMapThumbnailAsync(int maxDimension, int timeoutMs = 1500)
    {
        var tcs = new TaskCompletionSource<byte[]?>();
        EventHandler<byte[]>? handler = null;
        handler = (_, png) =>
        {
            _mapService.MapSnapshotCaptured -= handler;
            tcs.TrySetResult(png);
        };
        _mapService.MapSnapshotCaptured += handler;
        _mapService.RequestMapSnapshot(maxDimension);

        // Safety net: if no frame renders (e.g. control not mounted), don't
        // hang the close. Detach and resolve null so the caller falls back.
        _ = Task.Delay(timeoutMs).ContinueWith(_ =>
        {
            _mapService.MapSnapshotCaptured -= handler;
            tcs.TrySetResult(null);
        }, TaskScheduler.Default);

        return tcs.Task;
    }

    // Persisted next to appsettings.json so it survives app restarts. Unlike
    // MapThumbnailPng (the live current-map view), the last-job thumbnail is a
    // durable record of the field/job the operator would resume.
    private bool _loadingLastJobThumbnail;

    private string LastJobThumbnailPath()
    {
        var dir = Path.GetDirectoryName(_settingsService.GetSettingsFilePath()) ?? string.Empty;
        return Path.Combine(dir, "last_job_thumbnail.png");
    }

    private void LoadPersistedLastJobThumbnail()
    {
        try
        {
            var path = LastJobThumbnailPath();
            if (!File.Exists(path)) return;
            _loadingLastJobThumbnail = true;
            LastJobThumbnailPng = File.ReadAllBytes(path);
        }
        catch { /* non-fatal: card falls back to its placeholder */ }
        finally { _loadingLastJobThumbnail = false; }
    }

    partial void OnLastJobThumbnailPngChanged(byte[]? value)
    {
        if (_loadingLastJobThumbnail) return;  // don't echo a just-loaded value back to disk
        try
        {
            var path = LastJobThumbnailPath();
            if (value == null || value.Length == 0)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                File.WriteAllBytes(path, value);
            }
        }
        catch { /* non-fatal */ }
    }

    // Camera framing of the last job, persisted beside the thumbnail so Resume
    // Last Job restores the same zoom/center/rotation the operator left,
    // keeping the resumed view consistent with the Resume card's preview.
    private sealed record LastJobView(double Zoom, double CenterX, double CenterY, double Rotation, bool Is3D);
    private LastJobView? _lastJobView;

    private string LastJobViewPath()
    {
        var dir = Path.GetDirectoryName(_settingsService.GetSettingsFilePath()) ?? string.Empty;
        return Path.Combine(dir, "last_job_view.json");
    }

    // Record the current map camera at field close, matching the thumbnail framing.
    private void SaveLastJobView()
    {
        try
        {
            var (cx, cy) = _mapService.GetCameraCenter();
            var view = new LastJobView(_mapService.ZoomLevel, cx, cy, _mapService.Rotation, _mapService.Is3DMode);
            _lastJobView = view;
            File.WriteAllText(LastJobViewPath(), JsonSerializer.Serialize(view));
        }
        catch { /* non-fatal */ }
    }

    private void LoadLastJobView()
    {
        try
        {
            var path = LastJobViewPath();
            if (!File.Exists(path)) return;
            _lastJobView = JsonSerializer.Deserialize<LastJobView>(File.ReadAllText(path));
        }
        catch { _lastJobView = null; }
    }

    // Apply the saved framing after a resume so the map matches the preview.
    public void RestoreLastJobView()
    {
        var v = _lastJobView;
        if (v == null) return;
        _mapService.Set3DMode(v.Is3D);
        _mapService.SetCamera(v.CenterX, v.CenterY, v.Zoom, v.Rotation);
    }

    [RelayCommand]
    private void GoHome() => _navigationService.GoHome();

    [RelayCommand]
    private void NavigateToPage(PageType page) => _navigationService.Navigate(page);
}
