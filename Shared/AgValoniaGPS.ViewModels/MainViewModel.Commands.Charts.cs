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
        ToggleSteerChartPanelCommand = ReactiveCommand.Create(() =>
        {
            IsSteerChartPanelVisible = !IsSteerChartPanelVisible;
            EnsureChartDataServiceRunning();
        });

        ToggleHeadingChartPanelCommand = ReactiveCommand.Create(() =>
        {
            IsHeadingChartPanelVisible = !IsHeadingChartPanelVisible;
            EnsureChartDataServiceRunning();
        });

        ToggleXTEChartPanelCommand = ReactiveCommand.Create(() =>
        {
            IsXTEChartPanelVisible = !IsXTEChartPanelVisible;
            EnsureChartDataServiceRunning();
        });
    }

    /// <summary>
    /// Start/stop chart data collection based on whether any chart panel is visible.
    /// </summary>
    private void EnsureChartDataServiceRunning()
    {
        bool anyVisible = IsSteerChartPanelVisible || IsHeadingChartPanelVisible || IsXTEChartPanelVisible;

        if (anyVisible && !_chartDataService.IsRunning)
            _chartDataService.Start();
        else if (!anyVisible && _chartDataService.IsRunning)
            _chartDataService.Stop();
    }
}
