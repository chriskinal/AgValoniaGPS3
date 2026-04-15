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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Models.Tram;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Tram line configuration for controlled traffic farming (CTF).
/// Tram lines are permanent wheel tracks to reduce soil compaction.
/// </summary>
public class TramConfig : ObservableObject
{
    /// <summary>
    /// Width between tram passes in meters (typically 2x or 3x tool width)
    /// </summary>
    private double _tramWidth = 12.0;
    public double TramWidth
    {
        get => _tramWidth;
        set => SetProperty(ref _tramWidth, value);
    }

    /// <summary>
    /// Number of passes between tram lines (e.g., 3 = every 3rd pass)
    /// </summary>
    private int _passes = 3;
    public int Passes
    {
        get => _passes;
        set => SetProperty(ref _passes, System.Math.Max(1, value));
    }

    /// <summary>
    /// Display mode: 0=off, 1=all, 2=lines only, 3=outer only
    /// </summary>
    private TramDisplayMode _displayMode = TramDisplayMode.Off;
    public TramDisplayMode DisplayMode
    {
        get => _displayMode;
        set => SetProperty(ref _displayMode, value);
    }

    /// <summary>
    /// Display transparency (0.0 = fully transparent, 1.0 = fully opaque)
    /// </summary>
    private double _alpha = 0.8;
    public double Alpha
    {
        get => _alpha;
        set => SetProperty(ref _alpha, System.Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    /// Invert outer/inner tram line determination
    /// </summary>
    private bool _isOuterInverted;
    public bool IsOuterInverted
    {
        get => _isOuterInverted;
        set => SetProperty(ref _isOuterInverted, value);
    }

    /// <summary>
    /// Whether tram lines are enabled for the current field
    /// </summary>
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Current pass number (for tracking which pass we're on)
    /// </summary>
    private int _currentPass;
    public int CurrentPass
    {
        get => _currentPass;
        set => SetProperty(ref _currentPass, value);
    }

    /// <summary>
    /// Start pass offset (which pass number to begin tram lines from).
    /// 0 = start from first pass, 1 = skip first pass, etc.
    /// </summary>
    private int _startPass;
    public int StartPass
    {
        get => _startPass;
        set => SetProperty(ref _startPass, System.Math.Max(0, value));
    }

    /// <summary>
    /// Show left/right tram detection indicators on the map display.
    /// </summary>
    private bool _isDisplayTramControl = true;
    public bool IsDisplayTramControl
    {
        get => _isDisplayTramControl;
        set => SetProperty(ref _isDisplayTramControl, value);
    }

    /// <summary>
    /// Collection of tram systems. Each system generates its own set of tram lines.
    /// </summary>
    public ObservableCollection<TramSystem> Systems { get; } = new();
}

/// <summary>
/// Tram line display modes
/// </summary>
public enum TramDisplayMode
{
    /// <summary>Tram lines not displayed</summary>
    Off = 0,
    /// <summary>Display all tram line elements</summary>
    All = 1,
    /// <summary>Display only parallel tram lines (no boundary tracks)</summary>
    LinesOnly = 2,
    /// <summary>Display only outer boundary track</summary>
    OuterOnly = 3
}
