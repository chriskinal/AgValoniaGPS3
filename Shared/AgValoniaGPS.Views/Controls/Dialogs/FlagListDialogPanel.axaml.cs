// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FlagListDialogPanel : UserControl
{
    public FlagListDialogPanel()
    {
        InitializeComponent();
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
}

/// <summary>
/// Converts FlagColor enum to an Avalonia Color for the flag indicator.
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
