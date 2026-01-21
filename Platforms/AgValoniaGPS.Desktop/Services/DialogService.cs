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
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Desktop.Services;

/// <summary>
/// Desktop implementation of IDialogService.
/// Most dialogs are now shown via shared overlay panels in MainWindow,
/// triggered by ViewModel commands. This service handles basic message/confirmation dialogs.
/// </summary>
public class DialogService : IDialogService
{
    private Window? _parentWindow;

    public DialogService(IServiceProvider serviceProvider)
    {
    }

    /// <summary>
    /// Set the parent window for dialogs. Must be called after window is created.
    /// </summary>
    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    private Window GetParentWindow()
    {
        if (_parentWindow == null)
            throw new InvalidOperationException("Parent window not set. Call SetParentWindow first.");
        return _parentWindow;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var messageBox = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        var stack = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 16
        };

        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 16,
            Foreground = Brushes.White,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Avalonia.Thickness(24, 8),
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0)
        };
        okButton.Click += (s, args) => messageBox.Close();
        stack.Children.Add(okButton);

        messageBox.Content = stack;
        await messageBox.ShowDialog(GetParentWindow());
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        var stack = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 16
        };

        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 16,
            Foreground = Brushes.White,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16
        };

        var yesButton = new Button
        {
            Content = "Yes",
            Padding = new Avalonia.Thickness(24, 8),
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0)
        };
        yesButton.Click += (s, args) => { result = true; dialog.Close(); };

        var noButton = new Button
        {
            Content = "No",
            Padding = new Avalonia.Thickness(24, 8),
            Background = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0)
        };
        noButton.Click += (s, args) => { result = false; dialog.Close(); };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);
        stack.Children.Add(buttonPanel);

        dialog.Content = stack;
        await dialog.ShowDialog(GetParentWindow());
        return result;
    }

    // The following dialog methods are now handled by shared overlay panels in MainWindow,
    // triggered by ViewModel commands (e.g., ShowDataIODialogCommand sets IsDataIODialogVisible = true).
    // These stub implementations return null/false to indicate no action.

    public Task ShowDataIODialogAsync()
    {
        // Handled by shared DataIODialogPanel overlay
        return Task.CompletedTask;
    }

    public Task<(double Latitude, double Longitude)?> ShowSimCoordsDialogAsync(double currentLatitude, double currentLongitude)
    {
        // Handled by shared SimCoordsDialogPanel overlay
        return Task.FromResult<(double, double)?>(null);
    }

    public Task<DialogFieldSelectionResult?> ShowFieldSelectionDialogAsync(string fieldsDirectory)
    {
        // Handled by shared FieldSelectionDialogPanel overlay
        return Task.FromResult<DialogFieldSelectionResult?>(null);
    }

    public Task<DialogNewFieldResult?> ShowNewFieldDialogAsync(Position currentPosition)
    {
        // Handled by shared NewFieldDialogPanel overlay
        return Task.FromResult<DialogNewFieldResult?>(null);
    }

    public Task<DialogFromExistingFieldResult?> ShowFromExistingFieldDialogAsync(string fieldsDirectory)
    {
        // Handled by shared FromExistingFieldDialogPanel overlay
        return Task.FromResult<DialogFromExistingFieldResult?>(null);
    }

    public Task<DialogIsoXmlImportResult?> ShowIsoXmlImportDialogAsync(string fieldsDirectory)
    {
        // Handled by shared IsoXmlImportDialogPanel overlay
        return Task.FromResult<DialogIsoXmlImportResult?>(null);
    }

    public Task<DialogKmlImportResult?> ShowKmlImportDialogAsync(string fieldsDirectory, string? currentFieldPath = null)
    {
        // Handled by shared KmlImportDialogPanel overlay
        return Task.FromResult<DialogKmlImportResult?>(null);
    }

    public Task<DialogAgShareDownloadResult?> ShowAgShareDownloadDialogAsync(string apiKey, string fieldsDirectory)
    {
        // Handled by shared AgShareDownloadDialogPanel overlay
        return Task.FromResult<DialogAgShareDownloadResult?>(null);
    }

    public Task<bool> ShowAgShareUploadDialogAsync(string apiKey, string fieldName, string fieldDirectory)
    {
        // Handled by shared AgShareUploadDialogPanel overlay
        return Task.FromResult(false);
    }

    public Task ShowAgShareSettingsDialogAsync()
    {
        // Handled by shared AgShareSettingsDialogPanel overlay
        return Task.CompletedTask;
    }

    public Task<DialogMapBoundaryResult?> ShowMapBoundaryDialogAsync(double centerLatitude, double centerLongitude)
    {
        // Handled by shared BoundaryMapDialogPanel overlay
        return Task.FromResult<DialogMapBoundaryResult?>(null);
    }

    public Task<double?> ShowNumericInputDialogAsync(string description, double initialValue, double minValue = double.MinValue, double maxValue = double.MaxValue, int decimalPlaces = 2)
    {
        // Handled by shared NumericInputDialogPanel overlay
        return Task.FromResult<double?>(null);
    }
}
