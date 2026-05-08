// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AgValoniaGPS.Views.Controls;

public partial class GlyphButton : UserControl
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<GlyphButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<GlyphButton, string>(nameof(Label), defaultValue: string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<GlyphButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<GlyphButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<GlyphButton, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsGlyphFilledProperty =
        AvaloniaProperty.Register<GlyphButton, bool>(nameof(IsGlyphFilled));

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>
    /// When true, the glyph is rendered as a filled silhouette (Fill=Foreground,
    /// Stroke=null). When false (default), it's rendered as an outline
    /// (Stroke=Foreground, Fill=null).
    /// </summary>
    public bool IsGlyphFilled
    {
        get => GetValue(IsGlyphFilledProperty);
        set => SetValue(IsGlyphFilledProperty, value);
    }

    public GlyphButton()
    {
        InitializeComponent();
    }
}
