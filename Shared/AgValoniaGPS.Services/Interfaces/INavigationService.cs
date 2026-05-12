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

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// App-wide page navigation. Page-based UX replaces the prior
/// "single map + overlay panels" model — see
/// Plans/PageBasedNavigation/PLAN.md.
/// </summary>
public interface INavigationService
{
    /// <summary>The page currently shown by the PageHost.</summary>
    PageType CurrentPage { get; }

    /// <summary>The previous page, useful for "back to where you were" flows.</summary>
    PageType PreviousPage { get; }

    /// <summary>
    /// Raised after <see cref="CurrentPage"/> changes. The argument carries
    /// the new page; consumers needing the previous value can read
    /// <see cref="PreviousPage"/> before the next navigation.
    /// </summary>
    event EventHandler<PageType>? CurrentPageChanged;

    /// <summary>
    /// Navigate to <paramref name="page"/>. No-op if already on it.
    /// Leaving <see cref="PageType.MovingMap"/> disengages autosteer
    /// as a safety guardrail (Plan §guardrail).
    /// </summary>
    void Navigate(PageType page);

    /// <summary>Convenience for the Home button: navigates to Home.</summary>
    void GoHome();
}
