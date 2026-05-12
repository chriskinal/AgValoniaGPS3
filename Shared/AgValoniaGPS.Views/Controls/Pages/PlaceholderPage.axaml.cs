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

using Avalonia;
using Avalonia.Controls;

namespace AgValoniaGPS.Views.Controls.Pages;

/// <summary>
/// Shared placeholder body used by every page that doesn't yet have
/// its real content. Subclasses set <see cref="PageName"/> in their
/// constructor (or via XAML attribute) and inherit the layout.
/// </summary>
public partial class PlaceholderPage : UserControl
{
    public static readonly StyledProperty<string> PageNameProperty =
        AvaloniaProperty.Register<PlaceholderPage, string>(nameof(PageName), "Page");

    public string PageName
    {
        get => GetValue(PageNameProperty);
        set => SetValue(PageNameProperty, value);
    }

    public PlaceholderPage()
    {
        InitializeComponent();
        UpdateText(PageName);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageNameProperty)
            UpdateText((string)change.NewValue!);
    }

    private void UpdateText(string pageName)
    {
        if (this.FindControl<TextBlock>("TitleText") is { } title)
            title.Text = pageName;
        if (this.FindControl<TextBlock>("ComingSoonText") is { } cs)
            cs.Text = $"{pageName} — Coming Soon";
    }
}
