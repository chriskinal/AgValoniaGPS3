// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace AgValoniaGPS.Views.Localization;

/// <summary>
/// Singleton that provides access to localized strings from Strings.resx.
/// Supports runtime culture switching.
/// </summary>
public class TranslationSource : INotifyPropertyChanged
{
    public static TranslationSource Instance { get; } = new();

    private readonly ResourceManager _resourceManager =
        new("AgValoniaGPS.Views.Localization.Strings",
            typeof(TranslationSource).Assembly);

    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? key; // Fall back to key name
        }
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(_currentCulture, value)) return;
            _currentCulture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    /// <summary>
    /// Get the display name for a language code (e.g. "de" -> "Deutsch").
    /// </summary>
    public static string GetLanguageDisplayName(string code)
    {
        try
        {
            return new CultureInfo(code).NativeName;
        }
        catch
        {
            return code;
        }
    }

    /// <summary>
    /// Available language codes (those with .resx translation files).
    /// </summary>
    public static readonly string[] AvailableLanguages = new[]
    {
        "en", "da", "de", "es", "et", "fi", "fr", "hu", "it", "ko",
        "lt", "lv", "nl", "no", "pl", "pt", "ru", "sk", "sr", "tr",
        "uk", "zh-Hans"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
}
