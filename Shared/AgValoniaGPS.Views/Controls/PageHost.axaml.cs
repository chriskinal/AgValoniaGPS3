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

using Avalonia;
using Avalonia.Controls;
using AgValoniaGPS.Models.Navigation;
using AgValoniaGPS.Views.Controls.Pages;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Hosts the active page in the page-based navigation model.
/// Bind <see cref="CurrentPage"/> to whatever surfaces the
/// <c>INavigationService.CurrentPage</c> — typically MainViewModel.
///
/// Page UserControls are instantiated lazily on first navigation and
/// cached for subsequent visits, so a tile-tap → return-Home → tile-tap
/// loop doesn't churn state inside the page.
/// </summary>
public partial class PageHost : UserControl
{
    public static readonly StyledProperty<PageType> CurrentPageProperty =
        AvaloniaProperty.Register<PageHost, PageType>(nameof(CurrentPage), PageType.Home);

    public PageType CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    private readonly System.Collections.Generic.Dictionary<PageType, Control> _pageCache = new();
    private ContentControl? _pageContent;

    public PageHost()
    {
        InitializeComponent();
        _pageContent = this.FindControl<ContentControl>("PageContent");
        SwitchToPage(CurrentPage);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentPageProperty)
            SwitchToPage((PageType)change.NewValue!);
    }

    private void SwitchToPage(PageType page)
    {
        if (_pageContent == null)
            return;

        if (!_pageCache.TryGetValue(page, out var view))
        {
            view = CreatePage(page);
            _pageCache[page] = view;
        }
        _pageContent.Content = view;
    }

    /// <summary>
    /// Factory for each page UserControl. Pages inherit the host's
    /// DataContext (typically MainViewModel) by default — no need to
    /// pass it explicitly.
    ///
    /// <see cref="PageType.MovingMap"/> intentionally returns an empty
    /// control: the moving-map view is rendered by the platform shell
    /// at the layer underneath the PageHost. When CurrentPage is
    /// MovingMap, the shell hides the PageHost so the map shows through.
    /// (Phase 7 of the plan will move the map+panels layout into a
    /// proper MovingMapPage and the shell stops owning it directly.)
    /// </summary>
    private static Control CreatePage(PageType page) => page switch
    {
        PageType.Home                => new HomePage(),
        PageType.OperatorProfile     => new OperatorProfilePage(),
        PageType.Tractor             => new TractorPage(),
        PageType.Implement           => new ImplementPage(),
        PageType.FieldsAndJobs       => new FieldsAndJobsPage(),
        PageType.MovingMap           => new ContentControl(),
        PageType.NtripNetworking     => new NtripNetworkingPage(),
        PageType.ApplicationSettings => new ApplicationSettingsPage(),
        PageType.AgShare             => new AgSharePage(),
        PageType.LogViewer           => new LogViewerPage(),
        _                            => new HomePage(),
    };
}
