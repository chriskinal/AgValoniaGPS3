// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Threading.Tasks;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Priority for a dispatched UI-thread callback. Mirrors the subset of
/// Avalonia's DispatcherPriority the ViewModels actually use, without leaking
/// the Avalonia type into the VM/host layer.
/// </summary>
public enum UiDispatcherPriority
{
    /// <summary>Normal queue priority.</summary>
    Default,

    /// <summary>Run after higher-priority work (e.g. deferred bookkeeping).</summary>
    Background,

    /// <summary>Run at render priority — used to yield until a frame is painted.</summary>
    Render
}

/// <summary>
/// Abstraction over the UI-thread marshaller. Replaces direct
/// <c>Avalonia.Threading.Dispatcher.UIThread</c> access in the ViewModel layer
/// so the VMs depend on an injected service rather than an ambient framework
/// static. This keeps the VM (and a headless host that owns it) free of a hard
/// Avalonia dependency, and lets tests supply an inline dispatcher.
/// See Plans/CONFIG_STATE_AUDIT.md §11.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>True when the caller is already on the UI thread.</summary>
    bool CheckAccess();

    /// <summary>Queue <paramref name="action"/> to run on the UI thread (fire-and-forget).</summary>
    void Post(Action action, UiDispatcherPriority priority = UiDispatcherPriority.Default);

    /// <summary>Run <paramref name="action"/> on the UI thread; await completion.</summary>
    Task InvokeAsync(Action action, UiDispatcherPriority priority = UiDispatcherPriority.Default);
}
