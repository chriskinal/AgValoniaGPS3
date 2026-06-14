// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Threading.Tasks;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Threading;

/// <summary>
/// Framework-free <see cref="IUiDispatcher"/> that runs callbacks synchronously
/// on the calling thread. Intended for unit tests and headless hosts that have
/// no separate UI thread — there is nothing to marshal to, so everything is
/// "already on the UI thread". See Plans/CONFIG_STATE_AUDIT.md §11.
/// </summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action action, UiDispatcherPriority priority = UiDispatcherPriority.Default)
        => action();

    public Task InvokeAsync(Action action, UiDispatcherPriority priority = UiDispatcherPriority.Default)
    {
        action();
        return Task.CompletedTask;
    }
}
