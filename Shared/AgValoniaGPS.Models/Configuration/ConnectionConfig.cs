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

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Network and communication configuration.
/// Replaces: NTRIP and AgShare parts of AppSettings
/// </summary>
public class ConnectionConfig : ObservableObject
{
    // NTRIP
    private string _ntripCasterHost = string.Empty;
    public string NtripCasterHost
    {
        get => _ntripCasterHost;
        set => SetProperty(ref _ntripCasterHost, value);
    }

    private int _ntripCasterPort = 2101;
    public int NtripCasterPort
    {
        get => _ntripCasterPort;
        set => SetProperty(ref _ntripCasterPort, value);
    }

    private string _ntripMountPoint = string.Empty;
    public string NtripMountPoint
    {
        get => _ntripMountPoint;
        set => SetProperty(ref _ntripMountPoint, value);
    }

    private string _ntripUsername = string.Empty;
    public string NtripUsername
    {
        get => _ntripUsername;
        set => SetProperty(ref _ntripUsername, value);
    }

    private string _ntripPassword = string.Empty;
    public string NtripPassword
    {
        get => _ntripPassword;
        set => SetProperty(ref _ntripPassword, value);
    }

    private bool _ntripAutoConnect;
    public bool NtripAutoConnect
    {
        get => _ntripAutoConnect;
        set => SetProperty(ref _ntripAutoConnect, value);
    }

    // AgShare
    private string _agShareServer = "https://agshare.agopengps.com";
    public string AgShareServer
    {
        get => _agShareServer;
        set => SetProperty(ref _agShareServer, value);
    }

    private string _agShareApiKey = string.Empty;
    public string AgShareApiKey
    {
        get => _agShareApiKey;
        set => SetProperty(ref _agShareApiKey, value);
    }

    private bool _agShareEnabled;
    public bool AgShareEnabled
    {
        get => _agShareEnabled;
        set => SetProperty(ref _agShareEnabled, value);
    }

    // GPS Mode
    private bool _isDualGps;
    public bool IsDualGps
    {
        get => _isDualGps;
        set => SetProperty(ref _isDualGps, value);
    }

    private int _gpsUpdateRate = 10;
    public int GpsUpdateRate
    {
        get => _gpsUpdateRate;
        set => SetProperty(ref _gpsUpdateRate, value);
    }

    private bool _useRtk = true;
    public bool UseRtk
    {
        get => _useRtk;
        set => SetProperty(ref _useRtk, value);
    }

    // Dual Antenna Settings
    private double _dualHeadingOffset = 90.0;
    public double DualHeadingOffset
    {
        get => _dualHeadingOffset;
        set => SetProperty(ref _dualHeadingOffset, value);
    }

    private double _dualReverseDistance = 0.25;
    public double DualReverseDistance
    {
        get => _dualReverseDistance;
        set => SetProperty(ref _dualReverseDistance, value);
    }

    private bool _autoDualFix;
    public bool AutoDualFix
    {
        get => _autoDualFix;
        set => SetProperty(ref _autoDualFix, value);
    }

    private double _dualSwitchSpeed = 1.2;
    public double DualSwitchSpeed
    {
        get => _dualSwitchSpeed;
        set => SetProperty(ref _dualSwitchSpeed, value);
    }

    // Single Antenna Settings
    private double _minGpsStep = 0.05;
    public double MinGpsStep
    {
        get => _minGpsStep;
        set => SetProperty(ref _minGpsStep, value);
    }

    private double _fixToFixDistance = 0.5;
    public double FixToFixDistance
    {
        get => _fixToFixDistance;
        set => SetProperty(ref _fixToFixDistance, value);
    }

    private double _headingFusionWeight = 0.7;
    public double HeadingFusionWeight
    {
        get => _headingFusionWeight;
        set => SetProperty(ref _headingFusionWeight, value);
    }

    private bool _reverseDetection = true;
    public bool ReverseDetection
    {
        get => _reverseDetection;
        set => SetProperty(ref _reverseDetection, value);
    }

    // Heading Source (0=GPS, 1=Dual, 2=IMU, 3=Fusion) - may not be needed with new layout
    private int _headingSource;
    public int HeadingSource
    {
        get => _headingSource;
        set => SetProperty(ref _headingSource, value);
    }

    // RTK Monitoring
    private int _minFixQuality = 4;
    public int MinFixQuality
    {
        get => _minFixQuality;
        set => SetProperty(ref _minFixQuality, value);
    }

    private bool _rtkLostAlarm = true;
    public bool RtkLostAlarm
    {
        get => _rtkLostAlarm;
        set => SetProperty(ref _rtkLostAlarm, value);
    }

    private int _rtkLostAction; // 0=Warn, 1=Pause AutoSteer, 2=Stop Sections
    public int RtkLostAction
    {
        get => _rtkLostAction;
        set => SetProperty(ref _rtkLostAction, value);
    }

    private double _maxDifferentialAge = 5.0;
    public double MaxDifferentialAge
    {
        get => _maxDifferentialAge;
        set => SetProperty(ref _maxDifferentialAge, value);
    }

    private double _maxHdop = 2.0;
    public double MaxHdop
    {
        get => _maxHdop;
        set => SetProperty(ref _maxHdop, value);
    }

    // Module presence — which modules the user expects to be present.
    // Drives the aggregate Module-status indicator in the top status strip:
    //   Green  = every configured module currently OK
    //   Yellow = ≥1 configured module currently OK, ≥1 absent
    //   Red    = no configured module currently OK
    // Toggle UI ships with the Network panel (next commit); defaults make
    // first-launch behavior the same as the previous four-letter cluster.
    private bool _isGpsConfigured = true;
    public bool IsGpsConfigured
    {
        get => _isGpsConfigured;
        set => SetProperty(ref _isGpsConfigured, value);
    }

    private bool _isImuConfigured = true;
    public bool IsImuConfigured
    {
        get => _isImuConfigured;
        set => SetProperty(ref _isImuConfigured, value);
    }

    private bool _isAutoSteerConfigured = true;
    public bool IsAutoSteerConfigured
    {
        get => _isAutoSteerConfigured;
        set => SetProperty(ref _isAutoSteerConfigured, value);
    }

    private bool _isMachineConfigured = true;
    public bool IsMachineConfigured
    {
        get => _isMachineConfigured;
        set => SetProperty(ref _isMachineConfigured, value);
    }
}
