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

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class NewFieldDialogPanel : UserControl
{
    public NewFieldDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Backdrop = clicking on the map below. Full-abandon the flow rather
        // than return to the parent dialog: Cancel ⇒ "go back to launcher",
        // backdrop ⇒ "I'm done with all of this".
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.State.UI.CloseDialog();
            vm.NewFieldName = string.Empty;
        }
    }
}
