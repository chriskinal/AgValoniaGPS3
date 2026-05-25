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

using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.Models.Navigation;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Pages;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Launcher-tile tap handler. Each card carries its target PageType
    /// in Border.Tag; dispatch through MainViewModel.NavigateToPageCommand
    /// so the standard NavigationService guardrails (autosteer disengage
    /// on leaving Map, etc.) run.
    /// </summary>
    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { Tag: PageType page }
            && DataContext is MainViewModel vm
            && vm.NavigateToPageCommand.CanExecute(page))
        {
            vm.NavigateToPageCommand.Execute(page);
        }
    }

    /// <summary>
    /// Resume-last-job card tap. Reopens the most recent field/job (async)
    /// and routes to the Moving Map so the operator lands on the driving
    /// view; the field populates as it finishes loading.
    /// </summary>
    private void OnResumeLastJobTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (vm.ResumeLastJobCommand?.CanExecute(null) == true)
            vm.ResumeLastJobCommand.Execute(null);

        if (vm.NavigateToPageCommand.CanExecute(PageType.MovingMap))
            vm.NavigateToPageCommand.Execute(PageType.MovingMap);
    }
}
