// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FlagListDialogPanel : UserControl
{
    private Flag? _editingFlag;

    public FlagListDialogPanel()
    {
        InitializeComponent();

        // Update vehicle position for distance display when dialog becomes visible
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(IsVisible) && e.NewValue is true
                && DataContext is ViewModels.MainViewModel vm)
            {
                FlagDistanceConverter.VehicleEasting = vm.Easting;
                FlagDistanceConverter.VehicleNorthing = vm.Northing;
            }
        };
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
            vm.CloseFlagListCommand?.Execute(null);
    }

    private void DeleteFlag_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Flag flag && DataContext is ViewModels.MainViewModel vm)
            vm.DeleteFlagCommand?.Execute(flag);
    }

    private void ColorButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Flag flag) return;

        // Find the ColorSelector WrapPanel in this item's visual tree
        var parent = btn.Parent;
        while (parent != null)
        {
            if (parent is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is WrapPanel wp && wp.Name == "ColorSelector")
                    {
                        // Toggle visibility
                        wp.IsVisible = !wp.IsVisible;
                        if (wp.IsVisible && wp.Children.Count == 0)
                            PopulateColorSelector(wp, flag);
                        return;
                    }
                }
            }
            parent = parent.Parent as Control;
        }
    }

    private void PopulateColorSelector(WrapPanel panel, Flag flag)
    {
        foreach (FlagColor fc in Enum.GetValues<FlagColor>())
        {
            var colorBtn = new Button
            {
                Width = 28, Height = 28,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(2),
                Background = new SolidColorBrush(Color.Parse(Flag.ColorToHex(fc))),
                BorderThickness = new Thickness(2),
                BorderBrush = flag.FlagColor == fc
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
                Tag = fc
            };
            colorBtn.Click += (s, _) =>
            {
                if (s is Button b && b.Tag is FlagColor selectedColor)
                {
                    flag.FlagColor = selectedColor;
                    panel.IsVisible = false;
                    // Update map rendering
                    if (DataContext is ViewModels.MainViewModel vm)
                    {
                        vm.UpdateFlagsOnMap();
                    }
                }
            };
            panel.Children.Add(colorBtn);
        }
    }

    private void FlagName_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is Flag flag)
        {
            _editingFlag = flag;
            NameEditBox.Text = flag.Name;
            NameEditOverlay.IsVisible = true;
            NameEditBox.Focus();
            NameEditBox.SelectAll();
        }
    }

    private void NameEditOK_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_editingFlag != null && !string.IsNullOrWhiteSpace(NameEditBox.Text))
        {
            _editingFlag.Name = NameEditBox.Text;
        }
        _editingFlag = null;
        NameEditOverlay.IsVisible = false;
    }

    private void NameEditCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _editingFlag = null;
        NameEditOverlay.IsVisible = false;
    }
}

/// <summary>
/// Converts FlagColor enum to an Avalonia Color for display.
/// </summary>
public class FlagColorToBrushConverter : IValueConverter
{
    public static readonly FlagColorToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FlagColor fc)
            return Color.Parse(Flag.ColorToHex(fc));
        return Colors.Red;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a Flag to a distance+direction string from the current vehicle position.
/// Uses a static vehicle position set when the dialog opens.
/// </summary>
public class FlagDistanceConverter : IValueConverter
{
    public static readonly FlagDistanceConverter Instance = new();

    // Set these before the dialog opens
    public static double VehicleEasting { get; set; }
    public static double VehicleNorthing { get; set; }

    private static readonly string[] Arrows = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Flag flag) return "";

        double dx = flag.Easting - VehicleEasting;
        double dy = flag.Northing - VehicleNorthing;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist < 0.5) return "here";

        // Bearing: 0=N, 90=E, etc.
        double bearing = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        if (bearing < 0) bearing += 360;
        int idx = ((int)Math.Round(bearing / 45)) % 8;
        string arrow = Arrows[idx];

        return dist < 1000
            ? $"{arrow} {dist:F0}m"
            : $"{arrow} {dist / 1000:F1}km";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
