// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;
using Avalonia.Threading;
using Foundation;
using UIKit;

namespace AgValoniaGPS.iOS.Services;

/// <summary>
/// iOS battery reader backed by <c>UIDevice.CurrentDevice</c>. iOS notifies
/// us via <c>BatteryLevelDidChangeNotification</c> and
/// <c>BatteryStateDidChangeNotification</c>; we publish each change on the
/// dispatcher thread so bindings can react without explicit polling.
/// </summary>
public sealed class IOSBatteryService : IBatteryService, IDisposable
{
    private NSObject? _levelObserver;
    private NSObject? _stateObserver;
    private BatteryStatus _current = BatteryStatus.Unavailable;
    private readonly object _gate = new();
    private bool _started;

    public BatteryStatus CurrentStatus
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<BatteryStatus>? StatusChanged;

    public void Start()
    {
        if (_started) return;
        _started = true;

        UIDevice.CurrentDevice.BatteryMonitoringEnabled = true;

        _levelObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIDevice.BatteryLevelDidChangeNotification, _ => Read());
        _stateObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIDevice.BatteryStateDidChangeNotification, _ => Read());

        // Prime with the current reading.
        Read();
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;

        if (_levelObserver is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_levelObserver);
            _levelObserver.Dispose();
            _levelObserver = null;
        }
        if (_stateObserver is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_stateObserver);
            _stateObserver.Dispose();
            _stateObserver = null;
        }

        UIDevice.CurrentDevice.BatteryMonitoringEnabled = false;
    }

    public void Dispose() => Stop();

    private void Read()
    {
        BatteryStatus next;
        if (AgValoniaGPS.Services.BatteryForceMarker.IsEnabled())
        {
            next = AgValoniaGPS.Services.BatteryForceMarker.FakeStatus;
        }
        else
        {
            var device = UIDevice.CurrentDevice;
            var level = device.BatteryLevel;       // 0.0-1.0; -1 when unknown
            var state = device.BatteryState;

            if (level < 0 || state == UIDeviceBatteryState.Unknown)
            {
                next = BatteryStatus.Unavailable;
            }
            else
            {
                next = new BatteryStatus
                {
                    IsAvailable = true,
                    Level = level,
                    IsCharging = state == UIDeviceBatteryState.Charging
                              || state == UIDeviceBatteryState.Full,
                };
            }
        }

        bool changed;
        lock (_gate)
        {
            changed = !_current.Equals(next);
            _current = next;
        }
        if (changed)
        {
            Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(this, next));
        }
    }
}
