using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Network and communication configuration.
/// Replaces: NTRIP and AgShare parts of AppSettings
/// </summary>
public class ConnectionConfig : ReactiveObject
{
    // NTRIP
    private string _ntripCasterHost = string.Empty;
    public string NtripCasterHost
    {
        get => _ntripCasterHost;
        set => this.RaiseAndSetIfChanged(ref _ntripCasterHost, value);
    }

    private int _ntripCasterPort = 2101;
    public int NtripCasterPort
    {
        get => _ntripCasterPort;
        set => this.RaiseAndSetIfChanged(ref _ntripCasterPort, value);
    }

    private string _ntripMountPoint = string.Empty;
    public string NtripMountPoint
    {
        get => _ntripMountPoint;
        set => this.RaiseAndSetIfChanged(ref _ntripMountPoint, value);
    }

    private string _ntripUsername = string.Empty;
    public string NtripUsername
    {
        get => _ntripUsername;
        set => this.RaiseAndSetIfChanged(ref _ntripUsername, value);
    }

    private string _ntripPassword = string.Empty;
    public string NtripPassword
    {
        get => _ntripPassword;
        set => this.RaiseAndSetIfChanged(ref _ntripPassword, value);
    }

    private bool _ntripAutoConnect;
    public bool NtripAutoConnect
    {
        get => _ntripAutoConnect;
        set => this.RaiseAndSetIfChanged(ref _ntripAutoConnect, value);
    }

    // AgShare
    private string _agShareServer = "https://agshare.agopengps.com";
    public string AgShareServer
    {
        get => _agShareServer;
        set => this.RaiseAndSetIfChanged(ref _agShareServer, value);
    }

    private string _agShareApiKey = string.Empty;
    public string AgShareApiKey
    {
        get => _agShareApiKey;
        set => this.RaiseAndSetIfChanged(ref _agShareApiKey, value);
    }

    private bool _agShareEnabled;
    public bool AgShareEnabled
    {
        get => _agShareEnabled;
        set => this.RaiseAndSetIfChanged(ref _agShareEnabled, value);
    }

    // GPS Mode
    private bool _isDualGps;
    public bool IsDualGps
    {
        get => _isDualGps;
        set => this.RaiseAndSetIfChanged(ref _isDualGps, value);
    }

    private int _gpsUpdateRate = 10;
    public int GpsUpdateRate
    {
        get => _gpsUpdateRate;
        set => this.RaiseAndSetIfChanged(ref _gpsUpdateRate, value);
    }

    private bool _useRtk = true;
    public bool UseRtk
    {
        get => _useRtk;
        set => this.RaiseAndSetIfChanged(ref _useRtk, value);
    }

    // Dual Antenna Settings
    private double _dualHeadingOffset = 90.0;
    public double DualHeadingOffset
    {
        get => _dualHeadingOffset;
        set => this.RaiseAndSetIfChanged(ref _dualHeadingOffset, value);
    }

    private double _dualReverseDistance = 0.25;
    public double DualReverseDistance
    {
        get => _dualReverseDistance;
        set => this.RaiseAndSetIfChanged(ref _dualReverseDistance, value);
    }

    private bool _autoDualFix;
    public bool AutoDualFix
    {
        get => _autoDualFix;
        set => this.RaiseAndSetIfChanged(ref _autoDualFix, value);
    }

    private double _dualSwitchSpeed = 1.2;
    public double DualSwitchSpeed
    {
        get => _dualSwitchSpeed;
        set => this.RaiseAndSetIfChanged(ref _dualSwitchSpeed, value);
    }

    // Single Antenna Settings
    private double _minGpsStep = 0.05;
    public double MinGpsStep
    {
        get => _minGpsStep;
        set => this.RaiseAndSetIfChanged(ref _minGpsStep, value);
    }

    private double _fixToFixDistance = 0.5;
    public double FixToFixDistance
    {
        get => _fixToFixDistance;
        set => this.RaiseAndSetIfChanged(ref _fixToFixDistance, value);
    }

    private double _headingFusionWeight = 0.7;
    public double HeadingFusionWeight
    {
        get => _headingFusionWeight;
        set => this.RaiseAndSetIfChanged(ref _headingFusionWeight, value);
    }

    private bool _reverseDetection = true;
    public bool ReverseDetection
    {
        get => _reverseDetection;
        set => this.RaiseAndSetIfChanged(ref _reverseDetection, value);
    }

    // Heading Source (0=GPS, 1=Dual, 2=IMU, 3=Fusion) - may not be needed with new layout
    private int _headingSource;
    public int HeadingSource
    {
        get => _headingSource;
        set => this.RaiseAndSetIfChanged(ref _headingSource, value);
    }

    // RTK Monitoring
    private int _minFixQuality = 4;
    public int MinFixQuality
    {
        get => _minFixQuality;
        set => this.RaiseAndSetIfChanged(ref _minFixQuality, value);
    }

    private bool _rtkLostAlarm = true;
    public bool RtkLostAlarm
    {
        get => _rtkLostAlarm;
        set => this.RaiseAndSetIfChanged(ref _rtkLostAlarm, value);
    }

    private int _rtkLostAction; // 0=Warn, 1=Pause AutoSteer, 2=Stop Sections
    public int RtkLostAction
    {
        get => _rtkLostAction;
        set => this.RaiseAndSetIfChanged(ref _rtkLostAction, value);
    }

    private double _maxDifferentialAge = 5.0;
    public double MaxDifferentialAge
    {
        get => _maxDifferentialAge;
        set => this.RaiseAndSetIfChanged(ref _maxDifferentialAge, value);
    }

    private double _maxHdop = 2.0;
    public double MaxHdop
    {
        get => _maxHdop;
        set => this.RaiseAndSetIfChanged(ref _maxHdop, value);
    }
}
