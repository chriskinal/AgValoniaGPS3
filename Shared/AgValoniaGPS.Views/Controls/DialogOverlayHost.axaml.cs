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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Shared host for all modal dialog overlays.
/// Used by both Desktop and iOS platforms to avoid duplication.
/// </summary>
public partial class DialogOverlayHost : UserControl
{
    public DialogOverlayHost()
    {
        InitializeComponent();

        // Reset the idle auto-close countdown on any interaction with an open
        // dialog. Tunnel + handledEventsToo so the reset still fires even when a
        // child control marks the event handled. These only route through here
        // when the event target is inside a visible dialog (when none is open the
        // overlays aren't hit-test-visible), and the VM no-ops if nothing is open.
        AddHandler(PointerPressedEvent, OnDialogInteraction, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnDialogInteraction, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnDialogInteraction, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnDialogInteraction(object? sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.NotifyDialogInteraction();
}
