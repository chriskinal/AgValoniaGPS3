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
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AgValoniaGPS.Views.Controls;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class SimCoordsDialogPanel : UserControl
{
    private enum ActiveField { Latitude, Longitude }
    private ActiveField _activeField = ActiveField.Latitude;

    private decimal? _latitudeValue;
    private decimal? _longitudeValue;

    public SimCoordsDialogPanel()
    {
        InitializeComponent();

        // Subscribe to keyboard value changes
        KeyboardPanel.PropertyChanged += KeyboardPanel_PropertyChanged;

        // Subscribe to visibility changes to initialize values
        this.PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible)
        {
            // Dialog just became visible - load values from ViewModel
            if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
            {
                _latitudeValue = vm.SimCoordsDialogLatitude;
                _longitudeValue = vm.SimCoordsDialogLongitude;

                UpdateDisplays();

                // Default to latitude field
                SelectLatitude();
            }
        }
    }

    private void UpdateDisplays()
    {
        LatitudeDisplay.Text = FormatValue(_latitudeValue);
        LongitudeDisplay.Text = FormatValue(_longitudeValue);
    }

    private string FormatValue(decimal? value)
    {
        if (value.HasValue)
        {
            return value.Value.ToString("F8", CultureInfo.InvariantCulture);
        }
        return "0.00000000";
    }

    private void KeyboardPanel_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NumericKeyboardPanel.DisplayTextProperty)
        {
            // Use DisplayText to show partial input (like just "-")
            var displayText = KeyboardPanel.DisplayText;
            var displayValue = string.IsNullOrEmpty(displayText) ? "0" : displayText;

            if (_activeField == ActiveField.Latitude)
            {
                _latitudeValue = KeyboardPanel.Value;
                LatitudeDisplay.Text = displayValue;
            }
            else
            {
                _longitudeValue = KeyboardPanel.Value;
                LongitudeDisplay.Text = displayValue;
            }
        }
    }

    private void SelectLatitude()
    {
        _activeField = ActiveField.Latitude;
        LatitudeBorder.Classes.Add("Selected");
        LongitudeBorder.Classes.Remove("Selected");
        KeyboardPanel.Value = _latitudeValue;
    }

    private void SelectLongitude()
    {
        _activeField = ActiveField.Longitude;
        LatitudeBorder.Classes.Remove("Selected");
        LongitudeBorder.Classes.Add("Selected");
        KeyboardPanel.Value = _longitudeValue;
    }

    private void Latitude_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SelectLatitude();
        e.Handled = true;
    }

    private void Longitude_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SelectLongitude();
        e.Handled = true;
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Cancel the dialog when clicking/tapping the backdrop
        CancelDialog();
        e.Handled = true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        CancelDialog();
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            // Clamp values to valid ranges
            var lat = _latitudeValue ?? 0m;
            var lon = _longitudeValue ?? 0m;

            if (lat < -90m) lat = -90m;
            if (lat > 90m) lat = 90m;
            if (lon < -180m) lon = -180m;
            if (lon > 180m) lon = 180m;

            vm.SimCoordsDialogLatitude = lat;
            vm.SimCoordsDialogLongitude = lon;

            vm.ConfirmSimCoordsDialogCommand?.Execute(null);
        }
    }

    private void CancelDialog()
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CancelSimCoordsDialogCommand?.Execute(null);
        }
    }
}
