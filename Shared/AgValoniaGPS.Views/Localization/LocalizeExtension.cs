// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace AgValoniaGPS.Views.Localization;

/// <summary>
/// AXAML markup extension for localized strings.
/// Usage: Content="{loc:Localize Key=ABLine}"
/// Falls back to the key name if no translation is found.
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

        return TranslationSource.Instance[Key];
    }
}
