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

using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.ViewModels;

public class ShortcutButtonViewModel : ObservableObject
{
    public required string ButtonId { get; init; }
    public required string Tooltip { get; init; }
    public required ICommand Command { get; init; }
    public required string DefaultIconResource { get; init; }
    public string? ActiveIconResource { get; init; }

    private string? _overlayText;
    /// <summary>
    /// Optional text rendered on top of/instead of the icon (e.g., section count "8").
    /// </summary>
    public string? OverlayText
    {
        get => _overlayText;
        set => SetProperty(ref _overlayText, value);
    }

    private string _backgroundColorHex = "Transparent";
    /// <summary>
    /// Dynamic background color hex (e.g., section mode indicator: green/yellow/red).
    /// </summary>
    public string BackgroundColorHex
    {
        get => _backgroundColorHex;
        set => SetProperty(ref _backgroundColorHex, value);
    }

    /// <summary>
    /// True if this button uses overlay text instead of an icon image.
    /// </summary>
    public bool HasOverlayText => OverlayText != null;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// Returns the avares:// path for the current icon based on active state.
    /// The View converts this to a Bitmap via a value converter.
    /// </summary>
    public string CurrentIconResource => IsActive && ActiveIconResource != null
        ? ActiveIconResource : DefaultIconResource;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsActive))
        {
            OnPropertyChanged(nameof(CurrentIconResource));
        }
    }
}
