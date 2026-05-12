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

using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AgValoniaGPS.Views.Controls.Pages;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// PoC code-behind for AppShell: handles bottom-tab clicks, swaps the
/// content area, toggles the .Active class on tab containers so the
/// cyan underline indicator moves. Real navigation (via
/// NavigationService) replaces this in a follow-up.
/// </summary>
public partial class AppShell : UserControl
{
    public AppShell()
    {
        InitializeComponent();
        // Default page: Home.
        SetActiveTab("Home");
    }

    private void OnTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
            SetActiveTab(tag);
    }

    private void SetActiveTab(string tag)
    {
        var tabs = new Dictionary<string, StackPanel?>
        {
            ["Home"]       = this.FindControl<StackPanel>("TabHome"),
            ["Operator"]   = this.FindControl<StackPanel>("TabOperator"),
            ["Tractor"]    = this.FindControl<StackPanel>("TabTractor"),
            ["Implement"]  = this.FindControl<StackPanel>("TabImplement"),
            ["FieldsJobs"] = this.FindControl<StackPanel>("TabFieldsJobs"),
            ["Map"]        = this.FindControl<StackPanel>("TabMap"),
            ["Ntrip"]      = this.FindControl<StackPanel>("TabNtrip"),
            ["Settings"]   = this.FindControl<StackPanel>("TabSettings"),
        };

        // Move the Active class to the clicked tab; also move it to the
        // tab's Button (which carries the cyan-text style).
        foreach (var (key, panel) in tabs)
        {
            if (panel == null) continue;
            var active = key == tag;
            // Toggle .Active on the StackPanel (controls the underline).
            panel.Classes.Set("Active", active);
            // Toggle .Active on the inner button (controls the text color).
            if (panel.Children.Count > 0 && panel.Children[0] is Button btn)
                btn.Classes.Set("Active", active);
        }

        // Swap the page content. Home gets the real HomePage UserControl;
        // every other tab gets an inline "[Name] coming soon" placeholder
        // — enough to judge the look + tab-switch animation.
        var content = this.FindControl<ContentControl>("PageContent");
        if (content == null) return;

        content.Content = tag switch
        {
            "Home" => (Control)new HomePage(),
            _ => BuildPlaceholder(tag),
        };
    }

    /// <summary>Inline "coming soon" placeholder for tabs whose content
    /// isn't built yet. Stays dark, uses the theme brushes, big page name
    /// + small sub-text. Mirrors the dashboard aesthetic.</summary>
    private static Control BuildPlaceholder(string tag) =>
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
                        Text = tag.ToUpperInvariant(),
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        [!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("LabelTextBrush"),
                    },
                    new TextBlock
                    {
                        Text = $"{tag} page",
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
