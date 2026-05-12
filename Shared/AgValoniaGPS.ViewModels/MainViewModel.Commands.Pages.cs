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

using AgValoniaGPS.Models.Navigation;
using AgValoniaGPS.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Page-navigation glue. Owns the observable <see cref="CurrentPage"/>
/// mirror of <see cref="INavigationService.CurrentPage"/> and exposes
/// the commands the Home tiles + per-page Home buttons bind to.
///
/// See Plans/PageBasedNavigation/PLAN.md.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageHostVisible))]
    [NotifyPropertyChangedFor(nameof(IsSensorInfoVisible))]
    [NotifyPropertyChangedFor(nameof(IsFieldStatsInfoVisible))]
    [NotifyPropertyChangedFor(nameof(CurrentPageTitle))]
    [NotifyPropertyChangedFor(nameof(IsPageTitleVisible))]
    [NotifyPropertyChangedFor(nameof(IsHomeButtonVisible))]
    private PageType _currentPage = PageType.Home;

    /// <summary>
    /// Home-card status strings. Each Home tile doubles as a readout for
    /// the most recent value in its domain — these properties feed those
    /// readouts. Phase 1 returns placeholder dashes; Phases 3-5 wire
    /// real data (active operator profile, active tractor profile,
    /// active implement profile, last-opened field).
    /// </summary>
    public string LastOperatorName => "—";
    public string LastTractorName  => "—";
    public string LastImplementName => "—";
    public string LastFieldName    => "—";

    /// <summary>
    /// True when the PageHost overlay should be visible — i.e. on any
    /// page other than the Moving Map. On Moving Map the PageHost
    /// collapses so the platform shell's underlying map shows through.
    /// </summary>
    public bool IsPageHostVisible => CurrentPage != PageType.MovingMap;

    /// <summary>
    /// True when the top bar should show speed / heading / roll — Tractor
    /// page (for setup validation) and Moving Map (driving). See Plan §Top bar.
    /// </summary>
    public bool IsSensorInfoVisible =>
        CurrentPage == PageType.Tractor || CurrentPage == PageType.MovingMap;

    /// <summary>
    /// True when the top bar should show field/job stats — Fields & Jobs
    /// page and Moving Map, gated on an active field.
    /// </summary>
    public bool IsFieldStatsInfoVisible =>
        (CurrentPage == PageType.FieldsAndJobs || CurrentPage == PageType.MovingMap)
        && HasActiveField;

    /// <summary>Page title rendered in the top bar's center slot. Empty on Home + Moving Map.</summary>
    public string CurrentPageTitle => CurrentPage switch
    {
        PageType.OperatorProfile     => "Operator Profile",
        PageType.Tractor             => "Tractor",
        PageType.Implement           => "Implement",
        PageType.FieldsAndJobs       => "Fields & Jobs",
        PageType.NtripNetworking     => "NTRIP / Networking",
        PageType.ApplicationSettings => "Application Settings",
        PageType.AgShare             => "AgShare",
        PageType.LogViewer           => "Log Viewer",
        _                            => string.Empty,
    };

    /// <summary>Whether the center page-title slot is shown. Hidden on Home + Moving Map.</summary>
    public bool IsPageTitleVisible =>
        CurrentPage != PageType.Home && CurrentPage != PageType.MovingMap;

    /// <summary>Home button in the TopBar — visible everywhere except on Home itself.</summary>
    public bool IsHomeButtonVisible => CurrentPage != PageType.Home;

    /// <summary>
    /// Wires the observable <see cref="CurrentPage"/> mirror to the
    /// navigation service. Called from the constructor once the
    /// service is available.
    /// </summary>
    private void InitializeNavigation(INavigationService navigationService)
    {
        CurrentPage = navigationService.CurrentPage;
        navigationService.CurrentPageChanged += (_, page) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentPage = page);
    }

    /// <summary>Home button — used on every non-Home page's top-left.</summary>
    [RelayCommand]
    private void GoHome() => _navigationService.GoHome();

    /// <summary>
    /// Generic tile/button navigation. Bind <c>CommandParameter</c> to
    /// the target <see cref="PageType"/>.
    /// </summary>
    [RelayCommand]
    private void NavigateToPage(PageType page) => _navigationService.Navigate(page);
}
