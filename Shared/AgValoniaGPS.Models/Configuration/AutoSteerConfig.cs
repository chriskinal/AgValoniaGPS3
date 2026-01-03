using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// AutoSteer module configuration.
/// Settings sent to the steering module via PGN 251/252.
/// </summary>
public class AutoSteerConfig : ReactiveObject
{
    // ============================================
    // Tab 1: Pure Pursuit / Stanley Algorithm
    // ============================================

    private double _steerResponseHold = 3.0;
    /// <summary>
    /// Goal point look ahead hold distance (meters).
    /// Maps to goalPointLookAheadHold in AgOpenGPS.
    /// Range: 1.0 - 10.0
    /// </summary>
    public double SteerResponseHold
    {
        get => _steerResponseHold;
        set => this.RaiseAndSetIfChanged(ref _steerResponseHold, value);
    }

    private double _integralGain = 0.0;
    /// <summary>
    /// Integral gain for steering correction.
    /// Range: 0.0 - 1.0 (displayed as 0-100%)
    /// </summary>
    public double IntegralGain
    {
        get => _integralGain;
        set => this.RaiseAndSetIfChanged(ref _integralGain, value);
    }

    private bool _isStanleyMode = false;
    /// <summary>
    /// True = Stanley controller, False = Pure Pursuit
    /// </summary>
    public bool IsStanleyMode
    {
        get => _isStanleyMode;
        set => this.RaiseAndSetIfChanged(ref _isStanleyMode, value);
    }

    // Stanley-specific settings
    private double _stanleyAggressiveness = 1.0;
    /// <summary>
    /// Stanley aggressiveness gain.
    /// Range: 0.0 - 10.0
    /// </summary>
    public double StanleyAggressiveness
    {
        get => _stanleyAggressiveness;
        set => this.RaiseAndSetIfChanged(ref _stanleyAggressiveness, value);
    }

    private double _stanleyOvershootReduction = 1.0;
    /// <summary>
    /// Stanley overshoot reduction factor.
    /// Range: 0.0 - 10.0
    /// </summary>
    public double StanleyOvershootReduction
    {
        get => _stanleyOvershootReduction;
        set => this.RaiseAndSetIfChanged(ref _stanleyOvershootReduction, value);
    }

    // ============================================
    // Tab 2: Steering Sensor Calibration
    // ============================================

    private int _wasOffset = 0;
    /// <summary>
    /// Wheel Angle Sensor zero offset (raw counts).
    /// Used to zero the WAS when wheels are straight.
    /// </summary>
    public int WasOffset
    {
        get => _wasOffset;
        set => this.RaiseAndSetIfChanged(ref _wasOffset, value);
    }

    private double _countsPerDegree = 100;
    /// <summary>
    /// WAS counts per degree of steering angle.
    /// Range: 1 - 255
    /// </summary>
    public double CountsPerDegree
    {
        get => _countsPerDegree;
        set => this.RaiseAndSetIfChanged(ref _countsPerDegree, value);
    }

    private int _ackermann = 100;
    /// <summary>
    /// Ackermann steering geometry correction.
    /// 100 = neutral, less = more correction.
    /// Range: 0 - 200
    /// </summary>
    public int Ackermann
    {
        get => _ackermann;
        set => this.RaiseAndSetIfChanged(ref _ackermann, value);
    }

    private int _maxSteerAngle = 45;
    /// <summary>
    /// Maximum physical steering angle (degrees).
    /// Range: 10 - 90
    /// </summary>
    public int MaxSteerAngle
    {
        get => _maxSteerAngle;
        set => this.RaiseAndSetIfChanged(ref _maxSteerAngle, value);
    }

    // ============================================
    // Tab 3: Deadzone / Timing
    // ============================================

    private double _deadzoneHeading = 0.1;
    /// <summary>
    /// Dead zone around guidance line (degrees).
    /// No steering correction within this zone.
    /// Range: 0.0 - 5.0
    /// </summary>
    public double DeadzoneHeading
    {
        get => _deadzoneHeading;
        set => this.RaiseAndSetIfChanged(ref _deadzoneHeading, value);
    }

    private int _deadzoneDelay = 5;
    /// <summary>
    /// Counter delay before activating dead zone.
    /// Range: 0 - 50
    /// </summary>
    public int DeadzoneDelay
    {
        get => _deadzoneDelay;
        set => this.RaiseAndSetIfChanged(ref _deadzoneDelay, value);
    }

    private double _speedFactor = 1.0;
    /// <summary>
    /// Speed-based look ahead multiplier.
    /// Maps to goalPointLookAheadMult.
    /// Range: 0.5 - 3.0
    /// </summary>
    public double SpeedFactor
    {
        get => _speedFactor;
        set => this.RaiseAndSetIfChanged(ref _speedFactor, value);
    }

    private double _acquireFactor = 0.9;
    /// <summary>
    /// Acquire distance factor.
    /// Maps to goalPointAcquireFactor.
    /// Range: 0.5 - 1.0
    /// </summary>
    public double AcquireFactor
    {
        get => _acquireFactor;
        set => this.RaiseAndSetIfChanged(ref _acquireFactor, value);
    }

    // ============================================
    // Tab 4: Gain / PWM Settings
    // ============================================

    private int _proportionalGain = 10;
    /// <summary>
    /// Proportional gain (Kp) for steering PID.
    /// Range: 1 - 100
    /// </summary>
    public int ProportionalGain
    {
        get => _proportionalGain;
        set => this.RaiseAndSetIfChanged(ref _proportionalGain, value);
    }

    private int _maxPwm = 235;
    /// <summary>
    /// Maximum PWM output (high steer PWM).
    /// Range: 50 - 255
    /// </summary>
    public int MaxPwm
    {
        get => _maxPwm;
        set => this.RaiseAndSetIfChanged(ref _maxPwm, value);
    }

    private int _minPwm = 5;
    /// <summary>
    /// Minimum PWM to overcome static friction.
    /// Range: 1 - 50
    /// </summary>
    public int MinPwm
    {
        get => _minPwm;
        set => this.RaiseAndSetIfChanged(ref _minPwm, value);
    }

    // ============================================
    // Tab 5: Turn Sensors
    // ============================================

    private bool _turnSensorEnabled = false;
    /// <summary>
    /// Enable encoder-based turn sensor.
    /// </summary>
    public bool TurnSensorEnabled
    {
        get => _turnSensorEnabled;
        set => this.RaiseAndSetIfChanged(ref _turnSensorEnabled, value);
    }

    private bool _pressureSensorEnabled = false;
    /// <summary>
    /// Enable pressure-based turn sensor.
    /// </summary>
    public bool PressureSensorEnabled
    {
        get => _pressureSensorEnabled;
        set => this.RaiseAndSetIfChanged(ref _pressureSensorEnabled, value);
    }

    private bool _currentSensorEnabled = false;
    /// <summary>
    /// Enable current-based turn sensor.
    /// </summary>
    public bool CurrentSensorEnabled
    {
        get => _currentSensorEnabled;
        set => this.RaiseAndSetIfChanged(ref _currentSensorEnabled, value);
    }

    private int _turnSensorCounts = 255;
    /// <summary>
    /// Turn sensor pulse counts threshold.
    /// </summary>
    public int TurnSensorCounts
    {
        get => _turnSensorCounts;
        set => this.RaiseAndSetIfChanged(ref _turnSensorCounts, Math.Clamp(value, 0, 255));
    }

    private int _pressureTripPoint = 0;
    /// <summary>
    /// Pressure sensor trip point percentage (0-99%).
    /// </summary>
    public int PressureTripPoint
    {
        get => _pressureTripPoint;
        set => this.RaiseAndSetIfChanged(ref _pressureTripPoint, Math.Clamp(value, 0, 99));
    }

    private int _currentTripPoint = 0;
    /// <summary>
    /// Current sensor trip point percentage (0-99%).
    /// </summary>
    public int CurrentTripPoint
    {
        get => _currentTripPoint;
        set => this.RaiseAndSetIfChanged(ref _currentTripPoint, Math.Clamp(value, 0, 99));
    }

    // ============================================
    // Tab 6: Hardware Configuration
    // ============================================

    private bool _danfossEnabled = false;
    /// <summary>
    /// Enable Danfoss valve mode.
    /// </summary>
    public bool DanfossEnabled
    {
        get => _danfossEnabled;
        set => this.RaiseAndSetIfChanged(ref _danfossEnabled, value);
    }

    private bool _invertWas = false;
    /// <summary>
    /// Invert wheel angle sensor direction.
    /// </summary>
    public bool InvertWas
    {
        get => _invertWas;
        set => this.RaiseAndSetIfChanged(ref _invertWas, value);
    }

    private bool _invertMotor = false;
    /// <summary>
    /// Invert motor direction.
    /// </summary>
    public bool InvertMotor
    {
        get => _invertMotor;
        set => this.RaiseAndSetIfChanged(ref _invertMotor, value);
    }

    private bool _invertRelays = false;
    /// <summary>
    /// Invert relay output polarity.
    /// </summary>
    public bool InvertRelays
    {
        get => _invertRelays;
        set => this.RaiseAndSetIfChanged(ref _invertRelays, value);
    }

    private int _motorDriver = 0;
    /// <summary>
    /// Motor driver type: 0 = IBT2, 1 = Cytron
    /// </summary>
    public int MotorDriver
    {
        get => _motorDriver;
        set => this.RaiseAndSetIfChanged(ref _motorDriver, value);
    }

    private int _adConverter = 0;
    /// <summary>
    /// A/D converter type: 0 = Differential, 1 = Single
    /// </summary>
    public int AdConverter
    {
        get => _adConverter;
        set => this.RaiseAndSetIfChanged(ref _adConverter, value);
    }

    private int _imuAxisSwap = 0;
    /// <summary>
    /// IMU axis: 0 = X, 1 = Y
    /// </summary>
    public int ImuAxisSwap
    {
        get => _imuAxisSwap;
        set => this.RaiseAndSetIfChanged(ref _imuAxisSwap, value);
    }

    private int _externalEnable = 0;
    /// <summary>
    /// External enable mode: 0 = None, 1 = Switch, 2 = Button
    /// </summary>
    public int ExternalEnable
    {
        get => _externalEnable;
        set => this.RaiseAndSetIfChanged(ref _externalEnable, value);
    }

    // ============================================
    // Tab 7: Steering Algorithm Settings
    // ============================================

    private double _uTurnCompensation = 0.0;
    /// <summary>
    /// U-Turn path compensation factor.
    /// Range: -100 to 100 (negative = out, positive = in)
    /// </summary>
    public double UTurnCompensation
    {
        get => _uTurnCompensation;
        set => this.RaiseAndSetIfChanged(ref _uTurnCompensation, value);
    }

    private double _sideHillCompensation = 0.0;
    /// <summary>
    /// Side hill per degree compensation.
    /// Range: 0.0 - 1.0
    /// </summary>
    public double SideHillCompensation
    {
        get => _sideHillCompensation;
        set => this.RaiseAndSetIfChanged(ref _sideHillCompensation, value);
    }

    private bool _steerInReverse = false;
    /// <summary>
    /// Allow steering while driving in reverse.
    /// </summary>
    public bool SteerInReverse
    {
        get => _steerInReverse;
        set => this.RaiseAndSetIfChanged(ref _steerInReverse, value);
    }

    // ============================================
    // Tab 8: Speed Limits
    // ============================================

    private bool _manualTurnsEnabled = false;
    /// <summary>
    /// Enable manual turns feature.
    /// </summary>
    public bool ManualTurnsEnabled
    {
        get => _manualTurnsEnabled;
        set => this.RaiseAndSetIfChanged(ref _manualTurnsEnabled, value);
    }

    private double _manualTurnsSpeed = 12.0;
    /// <summary>
    /// Speed threshold for manual turns (km/h).
    /// </summary>
    public double ManualTurnsSpeed
    {
        get => _manualTurnsSpeed;
        set => this.RaiseAndSetIfChanged(ref _manualTurnsSpeed, value);
    }

    private double _minSteerSpeed = 0.0;
    /// <summary>
    /// Minimum speed to enable steering (km/h).
    /// </summary>
    public double MinSteerSpeed
    {
        get => _minSteerSpeed;
        set => this.RaiseAndSetIfChanged(ref _minSteerSpeed, value);
    }

    private double _maxSteerSpeed = 15.0;
    /// <summary>
    /// Maximum speed for full steering (km/h).
    /// </summary>
    public double MaxSteerSpeed
    {
        get => _maxSteerSpeed;
        set => this.RaiseAndSetIfChanged(ref _maxSteerSpeed, value);
    }

    // ============================================
    // Tab 9: Display Settings
    // ============================================

    private int _lineWidth = 2;
    /// <summary>
    /// Guidance line width in pixels.
    /// </summary>
    public int LineWidth
    {
        get => _lineWidth;
        set => this.RaiseAndSetIfChanged(ref _lineWidth, value);
    }

    private int _nudgeDistance = 20;
    /// <summary>
    /// Nudge distance in centimeters.
    /// </summary>
    public int NudgeDistance
    {
        get => _nudgeDistance;
        set => this.RaiseAndSetIfChanged(ref _nudgeDistance, value);
    }

    private double _nextGuidanceTime = 1.5;
    /// <summary>
    /// Next guidance line look-ahead time in seconds.
    /// </summary>
    public double NextGuidanceTime
    {
        get => _nextGuidanceTime;
        set => this.RaiseAndSetIfChanged(ref _nextGuidanceTime, value);
    }

    private int _cmPerPixel = 5;
    /// <summary>
    /// Centimeters per pixel for light bar.
    /// </summary>
    public int CmPerPixel
    {
        get => _cmPerPixel;
        set => this.RaiseAndSetIfChanged(ref _cmPerPixel, value);
    }

    private bool _lightbarEnabled = true;
    /// <summary>
    /// Enable light bar display.
    /// </summary>
    public bool LightbarEnabled
    {
        get => _lightbarEnabled;
        set => this.RaiseAndSetIfChanged(ref _lightbarEnabled, value);
    }

    private bool _steerBarEnabled = false;
    /// <summary>
    /// Enable steering bar display.
    /// </summary>
    public bool SteerBarEnabled
    {
        get => _steerBarEnabled;
        set => this.RaiseAndSetIfChanged(ref _steerBarEnabled, value);
    }

    private bool _guidanceBarOn = true;
    /// <summary>
    /// Guidance bar master on/off.
    /// </summary>
    public bool GuidanceBarOn
    {
        get => _guidanceBarOn;
        set => this.RaiseAndSetIfChanged(ref _guidanceBarOn, value);
    }

    // ============================================
    // PGN 251 Bitfield Helpers
    // ============================================

    /// <summary>
    /// Build Setting 0 byte for PGN 251.
    /// Bit 0: InvertWAS
    /// Bit 1: InvertRelays
    /// Bit 2: InvertMotor
    /// Bit 3: ADConverter (0=Diff, 1=Single)
    /// Bit 4: MotorDriver (0=IBT2, 1=Cytron)
    /// Bit 5-6: ExternalEnable (0=None, 1=Switch, 2=Button)
    /// Bit 7: TurnSensorEnabled
    /// </summary>
    public byte GetSetting0Byte()
    {
        byte result = 0;
        if (InvertWas) result |= 0x01;
        if (InvertRelays) result |= 0x02;
        if (InvertMotor) result |= 0x04;
        if (AdConverter == 1) result |= 0x08;
        if (MotorDriver == 1) result |= 0x10;
        result |= (byte)((ExternalEnable & 0x03) << 5);
        if (TurnSensorEnabled) result |= 0x80;
        return result;
    }

    /// <summary>
    /// Build Setting 1 byte for PGN 251.
    /// Bit 0: DanfossEnabled
    /// Bit 1: PressureSensorEnabled
    /// Bit 2: CurrentSensorEnabled
    /// Bit 3: ImuAxisSwap (0=X, 1=Y)
    /// </summary>
    public byte GetSetting1Byte()
    {
        byte result = 0;
        if (DanfossEnabled) result |= 0x01;
        if (PressureSensorEnabled) result |= 0x02;
        if (CurrentSensorEnabled) result |= 0x04;
        if (ImuAxisSwap == 1) result |= 0x08;
        return result;
    }

    /// <summary>
    /// Parse Setting 0 byte from PGN 251.
    /// </summary>
    public void SetFromSetting0Byte(byte value)
    {
        InvertWas = (value & 0x01) != 0;
        InvertRelays = (value & 0x02) != 0;
        InvertMotor = (value & 0x04) != 0;
        AdConverter = (value & 0x08) != 0 ? 1 : 0;
        MotorDriver = (value & 0x10) != 0 ? 1 : 0;
        ExternalEnable = (value >> 5) & 0x03;
        TurnSensorEnabled = (value & 0x80) != 0;
    }

    /// <summary>
    /// Parse Setting 1 byte from PGN 251.
    /// </summary>
    public void SetFromSetting1Byte(byte value)
    {
        DanfossEnabled = (value & 0x01) != 0;
        PressureSensorEnabled = (value & 0x02) != 0;
        CurrentSensorEnabled = (value & 0x04) != 0;
        ImuAxisSwap = (value & 0x08) != 0 ? 1 : 0;
    }

    // ============================================
    // Serialization (for profile persistence)
    // ============================================

    /// <summary>
    /// Create a DTO for JSON serialization.
    /// </summary>
    public AutoSteerConfigDto ToDto()
    {
        return new AutoSteerConfigDto
        {
            SteerResponseHold = SteerResponseHold,
            IntegralGain = IntegralGain,
            IsStanleyMode = IsStanleyMode,
            StanleyAggressiveness = StanleyAggressiveness,
            StanleyOvershootReduction = StanleyOvershootReduction,
            WasOffset = WasOffset,
            CountsPerDegree = CountsPerDegree,
            Ackermann = Ackermann,
            MaxSteerAngle = MaxSteerAngle,
            DeadzoneHeading = DeadzoneHeading,
            DeadzoneDelay = DeadzoneDelay,
            SpeedFactor = SpeedFactor,
            AcquireFactor = AcquireFactor,
            ProportionalGain = ProportionalGain,
            MaxPwm = MaxPwm,
            MinPwm = MinPwm,
            TurnSensorEnabled = TurnSensorEnabled,
            PressureSensorEnabled = PressureSensorEnabled,
            CurrentSensorEnabled = CurrentSensorEnabled,
            TurnSensorCounts = TurnSensorCounts,
            PressureTripPoint = PressureTripPoint,
            CurrentTripPoint = CurrentTripPoint,
            DanfossEnabled = DanfossEnabled,
            InvertWas = InvertWas,
            InvertMotor = InvertMotor,
            InvertRelays = InvertRelays,
            MotorDriver = MotorDriver,
            AdConverter = AdConverter,
            ImuAxisSwap = ImuAxisSwap,
            ExternalEnable = ExternalEnable,
            UTurnCompensation = UTurnCompensation,
            SideHillCompensation = SideHillCompensation,
            SteerInReverse = SteerInReverse,
            ManualTurnsEnabled = ManualTurnsEnabled,
            ManualTurnsSpeed = ManualTurnsSpeed,
            MinSteerSpeed = MinSteerSpeed,
            MaxSteerSpeed = MaxSteerSpeed,
            LineWidth = LineWidth,
            NudgeDistance = NudgeDistance,
            NextGuidanceTime = NextGuidanceTime,
            CmPerPixel = CmPerPixel,
            LightbarEnabled = LightbarEnabled,
            SteerBarEnabled = SteerBarEnabled,
            GuidanceBarOn = GuidanceBarOn
        };
    }

    /// <summary>
    /// Apply values from a DTO (loaded from JSON).
    /// </summary>
    public void ApplyFromDto(AutoSteerConfigDto dto)
    {
        SteerResponseHold = dto.SteerResponseHold;
        IntegralGain = dto.IntegralGain;
        IsStanleyMode = dto.IsStanleyMode;
        StanleyAggressiveness = dto.StanleyAggressiveness;
        StanleyOvershootReduction = dto.StanleyOvershootReduction;
        WasOffset = dto.WasOffset;
        CountsPerDegree = dto.CountsPerDegree;
        Ackermann = dto.Ackermann;
        MaxSteerAngle = dto.MaxSteerAngle;
        DeadzoneHeading = dto.DeadzoneHeading;
        DeadzoneDelay = dto.DeadzoneDelay;
        SpeedFactor = dto.SpeedFactor;
        AcquireFactor = dto.AcquireFactor;
        ProportionalGain = dto.ProportionalGain;
        MaxPwm = dto.MaxPwm;
        MinPwm = dto.MinPwm;
        TurnSensorEnabled = dto.TurnSensorEnabled;
        PressureSensorEnabled = dto.PressureSensorEnabled;
        CurrentSensorEnabled = dto.CurrentSensorEnabled;
        TurnSensorCounts = dto.TurnSensorCounts;
        PressureTripPoint = dto.PressureTripPoint;
        CurrentTripPoint = dto.CurrentTripPoint;
        DanfossEnabled = dto.DanfossEnabled;
        InvertWas = dto.InvertWas;
        InvertMotor = dto.InvertMotor;
        InvertRelays = dto.InvertRelays;
        MotorDriver = dto.MotorDriver;
        AdConverter = dto.AdConverter;
        ImuAxisSwap = dto.ImuAxisSwap;
        ExternalEnable = dto.ExternalEnable;
        UTurnCompensation = dto.UTurnCompensation;
        SideHillCompensation = dto.SideHillCompensation;
        SteerInReverse = dto.SteerInReverse;
        ManualTurnsEnabled = dto.ManualTurnsEnabled;
        ManualTurnsSpeed = dto.ManualTurnsSpeed;
        MinSteerSpeed = dto.MinSteerSpeed;
        MaxSteerSpeed = dto.MaxSteerSpeed;
        LineWidth = dto.LineWidth;
        NudgeDistance = dto.NudgeDistance;
        NextGuidanceTime = dto.NextGuidanceTime;
        CmPerPixel = dto.CmPerPixel;
        LightbarEnabled = dto.LightbarEnabled;
        SteerBarEnabled = dto.SteerBarEnabled;
        GuidanceBarOn = dto.GuidanceBarOn;
    }

    // ============================================
    // Reset to Defaults
    // ============================================

    /// <summary>
    /// Reset all settings to factory defaults.
    /// </summary>
    public void ResetToDefaults()
    {
        // Tab 1: Pure Pursuit / Stanley
        SteerResponseHold = 3.0;
        IntegralGain = 0.0;
        IsStanleyMode = false;
        StanleyAggressiveness = 1.0;
        StanleyOvershootReduction = 1.0;

        // Tab 2: Steering Sensor
        WasOffset = 0;
        CountsPerDegree = 100;
        Ackermann = 100;
        MaxSteerAngle = 45;

        // Tab 3: Deadzone / Timing
        DeadzoneHeading = 0.1;
        DeadzoneDelay = 5;
        SpeedFactor = 1.0;
        AcquireFactor = 0.9;

        // Tab 4: Gain / PWM
        ProportionalGain = 10;
        MaxPwm = 235;
        MinPwm = 5;

        // Tab 5: Turn Sensors
        TurnSensorEnabled = false;
        PressureSensorEnabled = false;
        CurrentSensorEnabled = false;
        TurnSensorCounts = 255;
        PressureTripPoint = 0;
        CurrentTripPoint = 0;

        // Tab 6: Hardware Config
        DanfossEnabled = false;
        InvertWas = false;
        InvertMotor = false;
        InvertRelays = false;
        MotorDriver = 0;
        AdConverter = 0;
        ImuAxisSwap = 0;
        ExternalEnable = 0;

        // Tab 7: Algorithm
        UTurnCompensation = 0.0;
        SideHillCompensation = 0.0;
        SteerInReverse = false;

        // Tab 8: Speed Limits
        ManualTurnsEnabled = false;
        ManualTurnsSpeed = 12.0;
        MinSteerSpeed = 0.0;
        MaxSteerSpeed = 15.0;

        // Tab 9: Display
        LineWidth = 2;
        NudgeDistance = 20;
        NextGuidanceTime = 1.5;
        CmPerPixel = 5;
        LightbarEnabled = true;
        SteerBarEnabled = false;
        GuidanceBarOn = true;
    }
}

/// <summary>
/// DTO for AutoSteerConfig JSON serialization.
/// </summary>
public record AutoSteerConfigDto
{
    // Tab 1: Pure Pursuit / Stanley
    public double SteerResponseHold { get; init; } = 3.0;
    public double IntegralGain { get; init; } = 0.0;
    public bool IsStanleyMode { get; init; } = false;
    public double StanleyAggressiveness { get; init; } = 1.0;
    public double StanleyOvershootReduction { get; init; } = 1.0;

    // Tab 2: Steering Sensor
    public int WasOffset { get; init; } = 0;
    public double CountsPerDegree { get; init; } = 100;
    public int Ackermann { get; init; } = 100;
    public int MaxSteerAngle { get; init; } = 45;

    // Tab 3: Deadzone / Timing
    public double DeadzoneHeading { get; init; } = 0.1;
    public int DeadzoneDelay { get; init; } = 5;
    public double SpeedFactor { get; init; } = 1.0;
    public double AcquireFactor { get; init; } = 0.9;

    // Tab 4: Gain / PWM
    public int ProportionalGain { get; init; } = 10;
    public int MaxPwm { get; init; } = 235;
    public int MinPwm { get; init; } = 5;

    // Tab 5: Turn Sensors
    public bool TurnSensorEnabled { get; init; } = false;
    public bool PressureSensorEnabled { get; init; } = false;
    public bool CurrentSensorEnabled { get; init; } = false;
    public int TurnSensorCounts { get; init; } = 255;
    public int PressureTripPoint { get; init; } = 0;
    public int CurrentTripPoint { get; init; } = 0;

    // Tab 6: Hardware Config
    public bool DanfossEnabled { get; init; } = false;
    public bool InvertWas { get; init; } = false;
    public bool InvertMotor { get; init; } = false;
    public bool InvertRelays { get; init; } = false;
    public int MotorDriver { get; init; } = 0;
    public int AdConverter { get; init; } = 0;
    public int ImuAxisSwap { get; init; } = 0;
    public int ExternalEnable { get; init; } = 0;

    // Tab 7: Algorithm
    public double UTurnCompensation { get; init; } = 0.0;
    public double SideHillCompensation { get; init; } = 0.0;
    public bool SteerInReverse { get; init; } = false;

    // Tab 8: Speed Limits
    public bool ManualTurnsEnabled { get; init; } = false;
    public double ManualTurnsSpeed { get; init; } = 12.0;
    public double MinSteerSpeed { get; init; } = 0.0;
    public double MaxSteerSpeed { get; init; } = 15.0;

    // Tab 9: Display
    public int LineWidth { get; init; } = 2;
    public int NudgeDistance { get; init; } = 20;
    public double NextGuidanceTime { get; init; } = 1.5;
    public int CmPerPixel { get; init; } = 5;
    public bool LightbarEnabled { get; init; } = true;
    public bool SteerBarEnabled { get; init; } = false;
    public bool GuidanceBarOn { get; init; } = true;
}
