// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using AgValoniaGPS.Views.Localization;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class LanguageDialogPanel : UserControl
{
    public LanguageDialogPanel()
    {
        InitializeComponent();
        BuildLanguageButtons();
    }

    private void BuildLanguageButtons()
    {
        var list = this.FindControl<ItemsControl>("LanguageList");
        if (list == null) return;

        var items = new System.Collections.Generic.List<Button>();

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
                Margin = new Avalonia.Thickness(0, 1),
            };
            btn.Classes.Add("ModernButton");
            btn.Click += (s, e) =>
            {
                if (s is Button b && b.Tag is string langCode &&
                    DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
                {
                    vm.SetLanguageCommand?.Execute(langCode);
                }
            };
            items.Add(btn);
        }

        list.ItemsSource = items;
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
            vm.CloseLanguageDialogCommand?.Execute(null);
    }
}
