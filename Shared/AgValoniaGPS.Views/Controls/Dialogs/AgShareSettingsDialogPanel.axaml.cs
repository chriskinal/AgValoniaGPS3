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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AgValoniaGPS.Services.AgShare;
using AgValoniaGPS.Views.Controls;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class AgShareSettingsDialogPanel : UserControl
{
    private enum ActiveField { ServerUrl, ApiKey }
    private ActiveField _activeField = ActiveField.ServerUrl;

    private string _serverUrl = "https://agshare.agopengps.com";
    private string _apiKey = "";

    public AgShareSettingsDialogPanel()
    {
        InitializeComponent();

        // Subscribe to keyboard text changes
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
                _serverUrl = vm.AgShareSettingsServerUrl;
                _apiKey = vm.AgShareSettingsApiKey;

                UpdateDisplays();

                // Default to server URL field
                SelectServerUrl();

                // Set enabled checkbox
                EnabledCheckBox.IsChecked = vm.AgShareSettingsEnabled;

                // Clear status
                ConnectionStatusLabel.Text = "";
            }
        }
    }

    private void UpdateDisplays()
    {
        ServerUrlDisplay.Text = string.IsNullOrEmpty(_serverUrl) ? "https://agshare.agopengps.com" : _serverUrl;
        ApiKeyDisplay.Text = MaskApiKey(_apiKey);
    }

    private string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return "(not set)";
        if (apiKey.Length <= 8)
            return new string('*', apiKey.Length);
        return apiKey.Substring(0, 4) + new string('*', apiKey.Length - 8) + apiKey.Substring(apiKey.Length - 4);
    }

    private void KeyboardPanel_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AlphanumericKeyboardPanel.TextProperty)
        {
            var text = KeyboardPanel.Text ?? "";

            if (_activeField == ActiveField.ServerUrl)
            {
                _serverUrl = text;
                ServerUrlDisplay.Text = string.IsNullOrEmpty(text) ? "" : text;
            }
            else
            {
                _apiKey = text;
                ApiKeyDisplay.Text = string.IsNullOrEmpty(text) ? "" : text;
            }
        }
    }

    private void SelectServerUrl()
    {
        _activeField = ActiveField.ServerUrl;
        ServerUrlBorder.Classes.Add("Selected");
        ApiKeyBorder.Classes.Remove("Selected");
        KeyboardPanel.Text = _serverUrl;
    }

    private void SelectApiKey()
    {
        _activeField = ActiveField.ApiKey;
        ServerUrlBorder.Classes.Remove("Selected");
        ApiKeyBorder.Classes.Add("Selected");
        KeyboardPanel.Text = _apiKey;
    }

    private void ServerUrl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SelectServerUrl();
        e.Handled = true;
    }

    private void ApiKey_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SelectApiKey();
        e.Handled = true;
    }

    private async void TestConnection_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            ConnectionStatusLabel.Text = "Enter API key first";
            ConnectionStatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
            return;
        }

        ConnectionStatusLabel.Text = "Testing...";
        ConnectionStatusLabel.Foreground = new SolidColorBrush(Color.Parse("#BDC3C7"));

        try
        {
            var serverUrl = string.IsNullOrEmpty(_serverUrl) ? "https://agshare.agopengps.com" : _serverUrl;
            var client = new AgShareClient(serverUrl, _apiKey);

            var (ok, message) = await client.CheckApiAsync();

            if (ok)
            {
                ConnectionStatusLabel.Text = "Success!";
                ConnectionStatusLabel.Foreground = new SolidColorBrush(Color.Parse("#27AE60"));
            }
            else
            {
                ConnectionStatusLabel.Text = message ?? "Failed";
                ConnectionStatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
            }
        }
        catch (Exception ex)
        {
            ConnectionStatusLabel.Text = ex.Message;
            ConnectionStatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
        }
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CancelDialog();
        e.Handled = true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        CancelDialog();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            // Update ViewModel with entered values
            vm.AgShareSettingsServerUrl = string.IsNullOrEmpty(_serverUrl) ? "https://agshare.agopengps.com" : _serverUrl;
            vm.AgShareSettingsApiKey = _apiKey;
            vm.AgShareSettingsEnabled = EnabledCheckBox.IsChecked ?? false;

            vm.ConfirmAgShareSettingsDialogCommand?.Execute(null);
        }
    }

    private void CancelDialog()
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CancelAgShareSettingsDialogCommand?.Execute(null);
        }
    }
}
