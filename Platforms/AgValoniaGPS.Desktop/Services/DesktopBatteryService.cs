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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;
using Avalonia.Threading;

namespace AgValoniaGPS.Desktop.Services;

/// <summary>
/// Desktop battery reader. Polls every 15 s on a background timer.
///
/// <list type="bullet">
///   <item>Windows: <c>GetSystemPowerStatus</c> P/Invoke.</item>
///   <item>macOS: shell out to <c>pmset -g batt</c> and parse the percentage + status.</item>
///   <item>Linux: read <c>/sys/class/power_supply/BAT*/capacity</c> and <c>status</c>.</item>
///   <item>Other / no battery: report <see cref="BatteryStatus.Unavailable"/>.</item>
/// </list>
///
/// The icon in the strip is hidden when the reading is unavailable, so a
/// plain desktop without a battery silently drops the slot.
/// </summary>
public sealed class DesktopBatteryService : IBatteryService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private Timer? _timer;
    private BatteryStatus _current = BatteryStatus.Unavailable;
    private readonly object _gate = new();

    public BatteryStatus CurrentStatus
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<BatteryStatus>? StatusChanged;

    public void Start()
    {
        if (_timer is not null) return;
        // Fire immediately, then on a 15 s cadence.
        _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, PollInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private void Poll()
    {
        BatteryStatus next;
        try
        {
            if (AgValoniaGPS.Services.BatteryForceMarker.IsEnabled())
                next = AgValoniaGPS.Services.BatteryForceMarker.FakeStatus;
            else if (OperatingSystem.IsWindows())      next = ReadWindows();
            else if (OperatingSystem.IsMacOS())        next = ReadMacOS();
            else if (OperatingSystem.IsLinux())        next = ReadLinux();
            else                                        next = BatteryStatus.Unavailable;
        }
        catch
        {
            next = BatteryStatus.Unavailable;
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

    // --- Windows ---------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int  BatteryLifeTime;
        public int  BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    private static BatteryStatus ReadWindows()
    {
        if (!GetSystemPowerStatus(out var s)) return BatteryStatus.Unavailable;
        // BatteryFlag 128 = "no system battery" → desktop without a battery.
        if ((s.BatteryFlag & 128) != 0) return BatteryStatus.Unavailable;
        if (s.BatteryLifePercent == 255) return BatteryStatus.Unavailable;
        return new BatteryStatus
        {
            IsAvailable = true,
            Level = s.BatteryLifePercent / 100.0,
            IsCharging = s.ACLineStatus == 1,
        };
    }

    // --- macOS -----------------------------------------------------------

    private static BatteryStatus ReadMacOS()
    {
        var psi = new ProcessStartInfo("/usr/bin/pmset", "-g batt")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi);
        if (proc is null) return BatteryStatus.Unavailable;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(2000);

        // Output line of interest: "  -InternalBattery-0 ... 73%; charging; ..."
        int pctIdx = output.IndexOf('%');
        if (pctIdx < 0) return BatteryStatus.Unavailable;
        int start = pctIdx;
        while (start > 0 && (char.IsDigit(output[start - 1]) || output[start - 1] == '.'))
            start--;
        var pctText = output.AsSpan(start, pctIdx - start);
        if (!double.TryParse(pctText, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            return BatteryStatus.Unavailable;

        bool charging = output.Contains("charging", StringComparison.OrdinalIgnoreCase)
                     && !output.Contains("discharging", StringComparison.OrdinalIgnoreCase);

        return new BatteryStatus
        {
            IsAvailable = true,
            Level = Math.Clamp(pct / 100.0, 0.0, 1.0),
            IsCharging = charging,
        };
    }

    // --- Linux -----------------------------------------------------------

    private static BatteryStatus ReadLinux()
    {
        const string root = "/sys/class/power_supply";
        if (!Directory.Exists(root)) return BatteryStatus.Unavailable;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            if (!Path.GetFileName(dir).StartsWith("BAT", StringComparison.Ordinal)) continue;
            var capacityPath = Path.Combine(dir, "capacity");
            var statusPath = Path.Combine(dir, "status");
            if (!File.Exists(capacityPath)) continue;

            var capacityText = File.ReadAllText(capacityPath).Trim();
            if (!int.TryParse(capacityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap))
                continue;

            bool charging = false;
            if (File.Exists(statusPath))
            {
                var statusText = File.ReadAllText(statusPath).Trim();
                charging = statusText.Equals("Charging", StringComparison.OrdinalIgnoreCase)
                        || statusText.Equals("Full", StringComparison.OrdinalIgnoreCase);
            }

            return new BatteryStatus
            {
                IsAvailable = true,
                Level = Math.Clamp(cap / 100.0, 0.0, 1.0),
                IsCharging = charging,
            };
        }
        return BatteryStatus.Unavailable;
    }
}
