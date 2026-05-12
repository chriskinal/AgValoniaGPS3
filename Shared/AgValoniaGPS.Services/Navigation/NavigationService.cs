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
using AgValoniaGPS.Models.Navigation;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Navigation;

/// <summary>
/// Singleton owner of the app's current page. Page-based UX —
/// see Plans/PageBasedNavigation/PLAN.md.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IAutoSteerService _autoSteer;
    private PageType _currentPage = PageType.Home;
    private PageType _previousPage = PageType.Home;

    public NavigationService(IAutoSteerService autoSteer)
    {
        _autoSteer = autoSteer;
    }

    public PageType CurrentPage => _currentPage;
    public PageType PreviousPage => _previousPage;
    public event EventHandler<PageType>? CurrentPageChanged;

    public void Navigate(PageType page)
    {
        if (page == _currentPage)
            return;

        // Leaving the Moving Map page is the cue to disengage autosteer.
        // Physical reality already enforces this (operator wouldn't be
        // changing pages while moving with autosteer engaged), but the
        // software check is the belt to that suspenders.
        if (_currentPage == PageType.MovingMap && _autoSteer.IsEngaged)
            _autoSteer.Disengage();

        _previousPage = _currentPage;
        _currentPage = page;
        CurrentPageChanged?.Invoke(this, page);
    }

    public void GoHome() => Navigate(PageType.Home);
}
