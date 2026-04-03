// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace AgValoniaGPS.Views.Localization;

/// <summary>
/// AXAML markup extension for localized strings with live updating.
/// Usage: Content="{loc:Localize ABLine}"
/// When the language changes at runtime, all bound strings update automatically.
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        // Return a binding to TranslationSource.CurrentCulture with a converter
        // that resolves the string key. When culture changes, PropertyChanged fires
        // and the binding re-evaluates, giving us live language switching.
        var binding = new Binding
        {
            Source = TranslationSource.Instance,
            Path = nameof(TranslationSource.CurrentCulture),
            Converter = TranslationConverter.Instance,
            ConverterParameter = Key,
            Mode = BindingMode.OneWay
        };

        return binding;
    }
}

/// <summary>
/// Converter that resolves a localization key when the culture binding updates.
/// </summary>
internal class TranslationConverter : IValueConverter
{
    public static readonly TranslationConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string key)
            return TranslationSource.Instance[key];
        return parameter?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
