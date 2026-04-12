// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// A numeric field that switches between tap-to-edit (shows numpad overlay)
/// and inline TextBox based on Display.KeyboardEnabled.
///
/// KeyboardEnabled ON (touch): Button that fires EditCommand (opens numpad)
/// KeyboardEnabled OFF (desktop): Inline editable TextBox
/// </summary>
public partial class EditableNumericField : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<EditableNumericField, double>(nameof(Value));

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<EditableNumericField, string>(nameof(Unit), "m");

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<EditableNumericField, string>(nameof(FormatString), "F2");

    public static readonly StyledProperty<ICommand?> EditCommandProperty =
        AvaloniaProperty.Register<EditableNumericField, ICommand?>(nameof(EditCommand));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    public ICommand? EditCommand
    {
        get => GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public EditableNumericField()
    {
        InitializeComponent();

        TapButton.Click += TapButton_Click;
        DirectInput.LostFocus += DirectInput_LostFocus;
        DirectInput.KeyDown += DirectInput_KeyDown;

        // React to KeyboardEnabled changes
        var display = ConfigurationStore.Instance.Display;
        display.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DisplayConfig.KeyboardEnabled))
                UpdateMode();
        };

        UpdateMode();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            UpdateDisplay();
        }
        else if (change.Property == UnitProperty)
        {
            UnitText.Text = Unit;
            DirectUnitText.Text = Unit;
        }
    }

    private void UpdateMode()
    {
        bool keyboard = ConfigurationStore.Instance.Display.KeyboardEnabled;
        TapButton.IsVisible = keyboard;
        DirectEditPanel.IsVisible = !keyboard;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var text = Value.ToString(FormatString, CultureInfo.InvariantCulture);
        DisplayText.Text = text;
        UnitText.Text = Unit;
        DirectUnitText.Text = Unit;

        // Only update TextBox if not focused (avoid overwriting user input)
        if (!DirectInput.IsFocused)
        {
            DirectInput.Text = text;
        }
    }

    private void TapButton_Click(object? sender, RoutedEventArgs e)
    {
        EditCommand?.Execute(null);
    }

    private void DirectInput_LostFocus(object? sender, RoutedEventArgs e)
    {
        ApplyDirectInput();
    }

    private void DirectInput_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            ApplyDirectInput();
            e.Handled = true;
        }
    }

    private void ApplyDirectInput()
    {
        if (double.TryParse(DirectInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            Value = parsed;
        }
        else
        {
            // Reset to current value on invalid input
            DirectInput.Text = Value.ToString(FormatString, CultureInfo.InvariantCulture);
        }
    }
}
