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
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AgValoniaGPS.Models.Navigation;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls.Pages;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// AppShell hosts the v2 dashboard chrome. Tab Buttons bind to
/// <c>MainViewModel.NavigateToPageCommand</c>; this code-behind listens
/// to <c>MainViewModel.CurrentPage</c> changes and updates the active
/// tab + content area accordingly.
/// </summary>
public partial class AppShell : UserControl
{
    private MainViewModel? _vm;

    public AppShell()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ApplyPage(PageType.Home);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            ApplyPage(_vm.CurrentPage);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage) && _vm != null)
            ApplyPage(_vm.CurrentPage);
    }

    private void ApplyPage(PageType page)
    {
        var tabs = new Dictionary<PageType, StackPanel?>
        {
            [PageType.Home]                = this.FindControl<StackPanel>("TabHome"),
            [PageType.OperatorProfile]     = this.FindControl<StackPanel>("TabOperator"),
            [PageType.Tractor]             = this.FindControl<StackPanel>("TabTractor"),
            [PageType.Implement]           = this.FindControl<StackPanel>("TabImplement"),
            [PageType.FieldsAndJobs]       = this.FindControl<StackPanel>("TabFieldsJobs"),
            [PageType.MovingMap]           = this.FindControl<StackPanel>("TabMap"),
            [PageType.NtripNetworking]     = this.FindControl<StackPanel>("TabNtrip"),
            [PageType.ApplicationSettings] = this.FindControl<StackPanel>("TabSettings"),
        };

        foreach (var (key, panel) in tabs)
        {
            if (panel == null) continue;
            var active = key == page;
            panel.Classes.Set("Active", active);
            if (panel.Children.Count > 0 && panel.Children[0] is Button btn)
                btn.Classes.Set("Active", active);
        }

        var content = this.FindControl<ContentControl>("PageContent");
        if (content == null) return;

        content.Content = page switch
        {
            PageType.Home => (Control)new HomePage(),
            _             => BuildPlaceholder(page),
        };
    }

    /// <summary>Inline "coming soon" placeholder for pages whose content
    /// isn't built yet.</summary>
    private static Control BuildPlaceholder(PageType page) =>
        new Border
        {
            Padding = new Thickness(40),
            Child = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = page.ToString().ToUpperInvariant(),
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        [!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("LabelTextBrush"),
                    },
                    new TextBlock
                    {
                        Text = $"{page} page",
                        FontSize = 32,
                        FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        [!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("PrimaryTextBrush"),
                    },
                    new TextBlock
                    {
                        Text = "Real content arrives in a later phase.",
                        FontSize = 13,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        [!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SecondaryTextBrush"),
                    },
                },
            },
        };
}
