// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using AgValoniaGPS.Services.Interfaces;
using Android.Content;
using Android.OS;
using Avalonia.Threading;

// Android.OS.BatteryStatus collides with our AgValoniaGPS.Models.BatteryStatus
// record. Alias both so each call site can pick the right type explicitly.
using AppBatteryStatus = AgValoniaGPS.Models.BatteryStatus;
using AndroidBatteryStatusEnum = Android.OS.BatteryStatus;

namespace AgValoniaGPS.Android.Services;

/// <summary>
/// Android battery reader. Subscribes to the sticky
/// <c>ACTION_BATTERY_CHANGED</c> broadcast — Android delivers it immediately
/// on registration AND on every level/state change, so no polling timer is
/// needed.
/// </summary>
public sealed class AndroidBatteryService : IBatteryService, IDisposable
{
    private readonly Context _context;
    private BatteryReceiver? _receiver;
    private AppBatteryStatus _current = AppBatteryStatus.Unavailable;
    private readonly object _gate = new();

    public AndroidBatteryService(Context context)
    {
        _context = context;
    }

    public AppBatteryStatus CurrentStatus
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<AppBatteryStatus>? StatusChanged;

    public void Start()
    {
        if (_receiver is not null) return;
        _receiver = new BatteryReceiver(this);
        var filter = new IntentFilter(Intent.ActionBatteryChanged);
        _context.RegisterReceiver(_receiver, filter);
    }

    public void Stop()
    {
        if (_receiver is null) return;
        try { _context.UnregisterReceiver(_receiver); } catch { /* not registered */ }
        _receiver.Dispose();
        _receiver = null;
    }

    public void Dispose() => Stop();

    private void OnBatteryIntent(Intent intent)
    {
        AppBatteryStatus next;
        if (AgValoniaGPS.Services.BatteryForceMarker.IsEnabled())
        {
            next = AgValoniaGPS.Services.BatteryForceMarker.FakeStatus;
        }
        else
        {
            int level = intent.GetIntExtra(BatteryManager.ExtraLevel, -1);
            int scale = intent.GetIntExtra(BatteryManager.ExtraScale, -1);
            int status = intent.GetIntExtra(BatteryManager.ExtraStatus, -1);

            if (level < 0 || scale <= 0)
            {
                next = AppBatteryStatus.Unavailable;
            }
            else
            {
                bool charging = status == (int)AndroidBatteryStatusEnum.Charging
                             || status == (int)AndroidBatteryStatusEnum.Full;
                next = new AppBatteryStatus
                {
                    IsAvailable = true,
                    Level = Math.Clamp((double)level / scale, 0.0, 1.0),
                    IsCharging = charging,
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

    private sealed class BatteryReceiver : BroadcastReceiver
    {
        private readonly AndroidBatteryService _owner;
        public BatteryReceiver(AndroidBatteryService owner) { _owner = owner; }
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null) _owner.OnBatteryIntent(intent);
        }
    }
}
