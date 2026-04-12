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

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Connection status for all external systems.
/// Updated by communication services.
/// </summary>
public class ConnectionState : ObservableObject
{
    // GPS
    private bool _isGpsConnected;
    public bool IsGpsConnected
    {
        get => _isGpsConnected;
        set => SetProperty(ref _isGpsConnected, value);
    }

    private bool _isGpsDataOk;
    public bool IsGpsDataOk
    {
        get => _isGpsDataOk;
        set => SetProperty(ref _isGpsDataOk, value);
    }

    // NTRIP
    private bool _isNtripConnected;
    public bool IsNtripConnected
    {
        get => _isNtripConnected;
        set => SetProperty(ref _isNtripConnected, value);
    }

    private string _ntripStatus = "Not Connected";
    public string NtripStatus
    {
        get => _ntripStatus;
        set => SetProperty(ref _ntripStatus, value);
    }

    private ulong _ntripBytesReceived;
    public ulong NtripBytesReceived
    {
        get => _ntripBytesReceived;
        set => SetProperty(ref _ntripBytesReceived, value);
    }

    // AutoSteer module
    private bool _isAutoSteerConnected;
    public bool IsAutoSteerConnected
    {
        get => _isAutoSteerConnected;
        set => SetProperty(ref _isAutoSteerConnected, value);
    }

    private bool _isAutoSteerDataOk;
    public bool IsAutoSteerDataOk
    {
        get => _isAutoSteerDataOk;
        set => SetProperty(ref _isAutoSteerDataOk, value);
    }

    private bool _isAutoSteerEngaged;
    public bool IsAutoSteerEngaged
    {
        get => _isAutoSteerEngaged;
        set => SetProperty(ref _isAutoSteerEngaged, value);
    }

    // Machine module
    private bool _isMachineConnected;
    public bool IsMachineConnected
    {
        get => _isMachineConnected;
        set => SetProperty(ref _isMachineConnected, value);
    }

    private bool _isMachineDataOk;
    public bool IsMachineDataOk
    {
        get => _isMachineDataOk;
        set => SetProperty(ref _isMachineDataOk, value);
    }

    // IMU
    private bool _isImuConnected;
    public bool IsImuConnected
    {
        get => _isImuConnected;
        set => SetProperty(ref _isImuConnected, value);
    }

    private bool _isImuDataOk;
    public bool IsImuDataOk
    {
        get => _isImuDataOk;
        set => SetProperty(ref _isImuDataOk, value);
    }

    // Overall status
    public bool IsFullyConnected =>
        IsGpsConnected && IsAutoSteerConnected && IsMachineConnected;

    public string OverallStatus
    {
        get
        {
            if (!IsGpsConnected) return "No GPS";
            if (!IsAutoSteerConnected) return "No AutoSteer";
            if (!IsMachineConnected) return "No Machine";
            if (!IsNtripConnected) return "No RTK";
            return "Connected";
        }
    }

    public void Reset()
    {
        // Connection state typically persists, but provide reset for full app restart
        IsGpsConnected = IsGpsDataOk = false;
        IsNtripConnected = false;
        NtripStatus = "Not Connected";
        NtripBytesReceived = 0;
        IsAutoSteerConnected = IsAutoSteerDataOk = IsAutoSteerEngaged = false;
        IsMachineConnected = IsMachineDataOk = false;
        IsImuConnected = IsImuDataOk = false;
    }
}
