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
/// the commands the AppShell tabs + Home button bind to.
///
/// See Plans/PageBasedNavigation/PLAN.md.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMovingMapPage))]
    [NotifyPropertyChangedFor(nameof(IsNotMovingMapPage))]
    private PageType _currentPage = PageType.Home;

    /// <summary>
    /// True iff the current page is MovingMap. AppShell uses this to
    /// collapse its full chrome and surface only the floating Home
    /// button, letting the v1 map+panels layout show through.
    /// </summary>
    public bool IsMovingMapPage => CurrentPage == PageType.MovingMap;
    public bool IsNotMovingMapPage => CurrentPage != PageType.MovingMap;

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

    [RelayCommand]
    private void GoHome() => _navigationService.GoHome();

    [RelayCommand]
    private void NavigateToPage(PageType page) => _navigationService.Navigate(page);
}
