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
using Avalonia.Media.Imaging;

namespace AgValoniaGPS.ViewModels;

public class ShortcutButtonViewModel : ObservableObject
{
    public required string ButtonId { get; init; }
    public required string Tooltip { get; init; }
    public required ICommand Command { get; init; }
    public required Bitmap DefaultIcon { get; init; }
    public Bitmap? ActiveIcon { get; init; }

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

    public Bitmap CurrentIcon => IsActive && ActiveIcon != null ? ActiveIcon : DefaultIcon;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsActive))
        {
            OnPropertyChanged(nameof(CurrentIcon));
        }
    }
}
