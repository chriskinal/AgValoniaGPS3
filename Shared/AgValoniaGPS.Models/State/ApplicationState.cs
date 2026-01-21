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

using System;
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Central application state container.
/// Single source of truth for ALL runtime state.
/// Singleton, Observable, Injectable.
/// </summary>
public class ApplicationState : ReactiveObject
{
    private static ApplicationState? _instance;

    /// <summary>
    /// Singleton instance for static access (use DI when possible)
    /// </summary>
    public static ApplicationState Instance => _instance ??= new ApplicationState();

    /// <summary>
    /// Create a new ApplicationState (primarily for DI registration)
    /// </summary>
    public ApplicationState()
    {
        _instance = this;
    }

    // Domain state objects
    public VehicleState Vehicle { get; } = new();
    public GuidanceState Guidance { get; } = new();
    public SectionState Sections { get; } = new();
    public ConnectionState Connections { get; } = new();
    public FieldState Field { get; } = new();
    public YouTurnState YouTurn { get; } = new();
    public BoundaryRecState BoundaryRec { get; } = new();
    public SimulatorState Simulator { get; } = new();
    public UIState UI { get; } = new();

    // Global events
    public event EventHandler? StateReset;

    /// <summary>
    /// Reset all state (e.g., when closing a field)
    /// </summary>
    public void Reset()
    {
        Vehicle.Reset();
        Guidance.Reset();
        Sections.Reset();
        YouTurn.Reset();
        BoundaryRec.Reset();
        Simulator.Reset();
        // Field and Connections typically persist across field changes
        StateReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Full reset including field (e.g., app restart)
    /// </summary>
    public void ResetAll()
    {
        Reset();
        Field.Reset();
        Connections.Reset();
    }
}
