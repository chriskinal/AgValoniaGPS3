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
using AgValoniaGPS.ViewModels.Wizards;

namespace AgValoniaGPS.Views.Controls.Wizards;

public partial class WizardDialogPanel : UserControl
{
    public WizardDialogPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the wizard ViewModel to display.
    /// </summary>
    public void SetWizard(WizardViewModel wizard)
    {
        WizardHostControl.DataContext = wizard;
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Optional: clicking backdrop could cancel the wizard
        // For now, do nothing - require explicit Cancel button
    }
}
