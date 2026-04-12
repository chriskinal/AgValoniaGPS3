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

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class NumericInputDialogPanel : UserControl
{
    public NumericInputDialogPanel()
    {
        InitializeComponent();
        DirectInput.TextChanged += DirectInput_TextChanged;
    }

    private void DirectInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // Parse direct text input into the ViewModel's numeric value
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            if (decimal.TryParse(DirectInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                vm.NumericInputDialogValue = value;
            }
        }
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Clicking the backdrop cancels the dialog
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CancelNumericInputDialogCommand?.Execute(null);
        }
    }
}
