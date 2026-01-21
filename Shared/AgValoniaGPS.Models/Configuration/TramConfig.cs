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

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Tram line configuration for controlled traffic farming (CTF).
/// Tram lines are permanent wheel tracks to reduce soil compaction.
/// </summary>
public class TramConfig : ReactiveObject
{
    /// <summary>
    /// Width between tram passes in meters (typically 2x or 3x tool width)
    /// </summary>
    private double _tramWidth = 12.0;
    public double TramWidth
    {
        get => _tramWidth;
        set => this.RaiseAndSetIfChanged(ref _tramWidth, value);
    }

    /// <summary>
    /// Number of passes between tram lines (e.g., 3 = every 3rd pass)
    /// </summary>
    private int _passes = 3;
    public int Passes
    {
        get => _passes;
        set => this.RaiseAndSetIfChanged(ref _passes, System.Math.Max(1, value));
    }

    /// <summary>
    /// Display mode: 0=off, 1=all, 2=lines only, 3=outer only
    /// </summary>
    private TramDisplayMode _displayMode = TramDisplayMode.Off;
    public TramDisplayMode DisplayMode
    {
        get => _displayMode;
        set => this.RaiseAndSetIfChanged(ref _displayMode, value);
    }

    /// <summary>
    /// Display transparency (0.0 = fully transparent, 1.0 = fully opaque)
    /// </summary>
    private double _alpha = 0.8;
    public double Alpha
    {
        get => _alpha;
        set => this.RaiseAndSetIfChanged(ref _alpha, System.Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    /// Invert outer/inner tram line determination
    /// </summary>
    private bool _isOuterInverted;
    public bool IsOuterInverted
    {
        get => _isOuterInverted;
        set => this.RaiseAndSetIfChanged(ref _isOuterInverted, value);
    }

    /// <summary>
    /// Whether tram lines are enabled for the current field
    /// </summary>
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    /// <summary>
    /// Current pass number (for tracking which pass we're on)
    /// </summary>
    private int _currentPass;
    public int CurrentPass
    {
        get => _currentPass;
        set => this.RaiseAndSetIfChanged(ref _currentPass, value);
    }
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
