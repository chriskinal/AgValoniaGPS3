// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Views.Infrastructure;

/// <summary>
/// Avalonia-backed <see cref="IUiDispatcher"/> wrapping <c>Dispatcher.UIThread</c>.
/// The single adapter that keeps the Avalonia UI-thread dependency in the View
/// layer instead of the ViewModels. Registered by each platform's DI.
/// See Plans/CONFIG_STATE_AUDIT.md §11.
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action, UiDispatcherPriority priority = UiDispatcherPriority.Default)
        => Dispatcher.UIThread.Post(action, Map(priority));

    public Task InvokeAsync(Action action, UiDispatcherPriority priority = UiDispatcherPriority.Default)
        => Dispatcher.UIThread.InvokeAsync(action, Map(priority)).GetTask();

    private static DispatcherPriority Map(UiDispatcherPriority priority) => priority switch
    {
        UiDispatcherPriority.Background => DispatcherPriority.Background,
        UiDispatcherPriority.Render => DispatcherPriority.Render,
        _ => DispatcherPriority.Default
    };
}
