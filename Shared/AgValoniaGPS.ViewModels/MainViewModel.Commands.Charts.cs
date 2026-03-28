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

using ReactiveUI;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Chart panel commands - toggle diagnostic chart panels.
/// </summary>
public partial class MainViewModel
{
    private void InitializeChartCommands()
    {
        // Start chart data collection immediately so users see recent
        // history when opening a chart mid-session. The overhead is
        // minimal (a few hundred data points in rolling buffers).
        _chartDataService.Start();

        ToggleSteerChartPanelCommand = ReactiveCommand.Create(() =>
        {
            IsSteerChartPanelVisible = !IsSteerChartPanelVisible;
        });

        ToggleHeadingChartPanelCommand = ReactiveCommand.Create(() =>
        {
            IsHeadingChartPanelVisible = !IsHeadingChartPanelVisible;
        });

        ToggleXTEChartPanelCommand = ReactiveCommand.Create(() =>
        {
            IsXTEChartPanelVisible = !IsXTEChartPanelVisible;
        });
    }
}
