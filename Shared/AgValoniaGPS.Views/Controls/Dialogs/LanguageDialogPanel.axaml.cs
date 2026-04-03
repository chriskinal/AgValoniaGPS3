// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AgValoniaGPS.Views.Localization;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class LanguageDialogPanel : UserControl
{
    private readonly List<Button> _buttons = new();

    public LanguageDialogPanel()
    {
        InitializeComponent();
        BuildLanguageButtons();

        // Update highlight when dialog becomes visible
        PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(IsVisible) && IsVisible)
                HighlightCurrentLanguage();
        };
    }

    private void BuildLanguageButtons()
    {
        var list = this.FindControl<ItemsControl>("LanguageList");
        if (list == null) return;

        foreach (var code in TranslationSource.AvailableLanguages)
        {
            string displayName;
            try
            {
                var ci = new CultureInfo(code);
                displayName = $"{ci.NativeName}  ({code})";
            }
            catch
            {
                displayName = code;
            }

            var btn = new Button
            {
                Content = displayName,
                Tag = code,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                MinHeight = 40,
                FontSize = 15,
                Margin = new Thickness(0, 1),
            };
            btn.Classes.Add("ModernButton");
            btn.Click += (s, e) =>
            {
                if (s is Button b && b.Tag is string langCode &&
                    DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
                {
                    vm.SetLanguageCommand?.Execute(langCode);
                    HighlightCurrentLanguage();
                }
            };
            _buttons.Add(btn);
        }

        list.ItemsSource = _buttons;
    }

    private void HighlightCurrentLanguage()
    {
        var currentCode = TranslationSource.Instance.CurrentCulture.Name;
        // Fallback: "en" has empty culture name
        if (string.IsNullOrEmpty(currentCode))
            currentCode = "en";

        foreach (var btn in _buttons)
        {
            bool isSelected = btn.Tag is string code &&
                (code == currentCode || currentCode.StartsWith(code));

            btn.FontWeight = isSelected ? FontWeight.Bold : FontWeight.Normal;
            btn.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            btn.BorderBrush = isSelected
                ? new SolidColorBrush(Color.FromRgb(50, 180, 50))
                : null;
        }
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
            vm.CloseLanguageDialogCommand?.Execute(null);
    }
}
