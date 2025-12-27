using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for the Configuration Dialog.
/// Binds directly to ConfigurationStore - no property mapping needed.
/// </summary>
public class ConfigurationViewModel : ReactiveObject
{
    private readonly IConfigurationService _configService;
    private readonly INtripClientService? _ntripService;

    #region Dialog Visibility

    private bool _isDialogVisible;
    public bool IsDialogVisible
    {
        get => _isDialogVisible;
        set => this.RaiseAndSetIfChanged(ref _isDialogVisible, value);
    }

    #endregion

    #region Numeric Input Dialog

    private bool _isNumericInputVisible;
    public bool IsNumericInputVisible
    {
        get => _isNumericInputVisible;
        set => this.RaiseAndSetIfChanged(ref _isNumericInputVisible, value);
    }

    private string _numericInputTitle = string.Empty;
    public string NumericInputTitle
    {
        get => _numericInputTitle;
        set => this.RaiseAndSetIfChanged(ref _numericInputTitle, value);
    }

    private string _numericInputUnit = string.Empty;
    public string NumericInputUnit
    {
        get => _numericInputUnit;
        set => this.RaiseAndSetIfChanged(ref _numericInputUnit, value);
    }

    private decimal? _numericInputValue;
    public decimal? NumericInputValue
    {
        get => _numericInputValue;
        set => this.RaiseAndSetIfChanged(ref _numericInputValue, value);
    }

    private string _numericInputDisplayText = string.Empty;
    public string NumericInputDisplayText
    {
        get => _numericInputDisplayText;
        set => this.RaiseAndSetIfChanged(ref _numericInputDisplayText, value);
    }

    private bool _numericInputIntegerOnly;
    public bool NumericInputIntegerOnly
    {
        get => _numericInputIntegerOnly;
        set => this.RaiseAndSetIfChanged(ref _numericInputIntegerOnly, value);
    }

    private bool _numericInputAllowNegative = true;
    public bool NumericInputAllowNegative
    {
        get => _numericInputAllowNegative;
        set => this.RaiseAndSetIfChanged(ref _numericInputAllowNegative, value);
    }

    private double _numericInputMin = double.MinValue;
    private double _numericInputMax = double.MaxValue;
    private bool _isFirstDigitEntry = true; // Track if user has started typing

    private Action<double>? _numericInputCallback;

    public ICommand ConfirmNumericInputCommand { get; private set; } = null!;
    public ICommand CancelNumericInputCommand { get; private set; } = null!;
    public ICommand NumericInputDigitCommand { get; private set; } = null!;
    public ICommand NumericInputBackspaceCommand { get; private set; } = null!;
    public ICommand NumericInputClearCommand { get; private set; } = null!;
    public ICommand NumericInputNegateCommand { get; private set; } = null!;

    /// <summary>
    /// Shows the numeric input dialog for editing a value
    /// </summary>
    private void ShowNumericInput(
        string title,
        double currentValue,
        Action<double> onConfirm,
        string unit = "m",
        bool integerOnly = false,
        bool allowNegative = true,
        double min = double.MinValue,
        double max = double.MaxValue)
    {
        NumericInputTitle = title;
        NumericInputUnit = unit;
        NumericInputIntegerOnly = integerOnly;
        NumericInputAllowNegative = allowNegative;
        _numericInputMin = min;
        _numericInputMax = max;
        _numericInputCallback = onConfirm;
        _isFirstDigitEntry = true; // Reset - first digit will replace current value

        // Set initial value and display
        _numericInputValue = (decimal)currentValue;
        this.RaisePropertyChanged(nameof(NumericInputValue));

        NumericInputDisplayText = integerOnly
            ? ((int)currentValue).ToString()
            : currentValue.ToString("F2");

        IsNumericInputVisible = true;
    }

    private void InitializeNumericInputCommands()
    {
        ConfirmNumericInputCommand = new RelayCommand(() =>
        {
            if (_numericInputCallback != null)
            {
                // Parse from display text to get actual value
                // Use InvariantCulture to handle decimal point consistently
                if (decimal.TryParse(NumericInputDisplayText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    var value = (double)parsed;
                    // Clamp to min/max
                    value = Math.Clamp(value, _numericInputMin, _numericInputMax);
                    _numericInputCallback(value);
                    Config.MarkChanged();
                }
            }
            IsNumericInputVisible = false;
            _numericInputCallback = null;
        });

        CancelNumericInputCommand = new RelayCommand(() =>
        {
            IsNumericInputVisible = false;
            _numericInputCallback = null;
        });

        NumericInputDigitCommand = new RelayCommand<string>(digit =>
        {
            if (string.IsNullOrEmpty(digit)) return;

            // Handle decimal point
            if (digit == ".")
            {
                if (NumericInputIntegerOnly) return;

                if (_isFirstDigitEntry)
                {
                    // Start fresh with "0."
                    NumericInputDisplayText = "0.";
                    _isFirstDigitEntry = false;
                }
                else if (!NumericInputDisplayText.Contains("."))
                {
                    NumericInputDisplayText += ".";
                }
                return;
            }

            // First digit replaces the initial value
            if (_isFirstDigitEntry)
            {
                NumericInputDisplayText = digit;
                _isFirstDigitEntry = false;
            }
            else
            {
                // Append digit
                if (NumericInputDisplayText == "0")
                    NumericInputDisplayText = digit;
                else
                    NumericInputDisplayText += digit;
            }

            // Update the backing value
            if (decimal.TryParse(NumericInputDisplayText, out var parsed))
            {
                _numericInputValue = parsed;
                this.RaisePropertyChanged(nameof(NumericInputValue));
            }
        });

        NumericInputBackspaceCommand = new RelayCommand(() =>
        {
            _isFirstDigitEntry = false; // User is editing

            var current = NumericInputDisplayText;
            if (current.Length > 1)
            {
                // Handle negative numbers - don't delete just the minus sign
                if (current.Length == 2 && current.StartsWith("-"))
                    NumericInputDisplayText = "0";
                else
                    NumericInputDisplayText = current.Substring(0, current.Length - 1);
            }
            else
            {
                NumericInputDisplayText = "0";
            }

            if (decimal.TryParse(NumericInputDisplayText, out var parsed))
            {
                _numericInputValue = parsed;
                this.RaisePropertyChanged(nameof(NumericInputValue));
            }
        });

        NumericInputClearCommand = new RelayCommand(() =>
        {
            NumericInputDisplayText = "0";
            _numericInputValue = 0;
            _isFirstDigitEntry = false;
            this.RaisePropertyChanged(nameof(NumericInputValue));
        });

        NumericInputNegateCommand = new RelayCommand(() =>
        {
            if (!NumericInputAllowNegative) return;

            _isFirstDigitEntry = false; // User is editing

            if (NumericInputDisplayText.StartsWith("-"))
            {
                NumericInputDisplayText = NumericInputDisplayText.Substring(1);
            }
            else if (NumericInputDisplayText != "0")
            {
                NumericInputDisplayText = "-" + NumericInputDisplayText;
            }

            if (decimal.TryParse(NumericInputDisplayText, out var parsed))
            {
                _numericInputValue = parsed;
                this.RaisePropertyChanged(nameof(NumericInputValue));
            }
        });
    }

    #endregion

    #region Text Input Dialog

    private bool _isTextInputVisible;
    public bool IsTextInputVisible
    {
        get => _isTextInputVisible;
        set => this.RaiseAndSetIfChanged(ref _isTextInputVisible, value);
    }

    private string _textInputTitle = string.Empty;
    public string TextInputTitle
    {
        get => _textInputTitle;
        set => this.RaiseAndSetIfChanged(ref _textInputTitle, value);
    }

    private string _textInputValue = string.Empty;
    public string TextInputValue
    {
        get => _textInputValue;
        set => this.RaiseAndSetIfChanged(ref _textInputValue, value);
    }

    private bool _textInputIsPassword;
    public bool TextInputIsPassword
    {
        get => _textInputIsPassword;
        set => this.RaiseAndSetIfChanged(ref _textInputIsPassword, value);
    }

    private Action<string>? _textInputCallback;

    public ICommand ConfirmTextInputCommand { get; private set; } = null!;
    public ICommand CancelTextInputCommand { get; private set; } = null!;
    public ICommand TextInputKeyCommand { get; private set; } = null!;
    public ICommand TextInputBackspaceCommand { get; private set; } = null!;
    public ICommand TextInputClearCommand { get; private set; } = null!;
    public ICommand TextInputSpaceCommand { get; private set; } = null!;

    private void ShowTextInput(string title, string currentValue, Action<string> callback, bool isPassword = false)
    {
        TextInputTitle = title;
        TextInputValue = currentValue;
        TextInputIsPassword = isPassword;
        _textInputCallback = callback;
        IsTextInputVisible = true;
    }

    private void InitializeTextInputCommands()
    {
        ConfirmTextInputCommand = new RelayCommand(() =>
        {
            _textInputCallback?.Invoke(TextInputValue);
            IsTextInputVisible = false;
        });

        CancelTextInputCommand = new RelayCommand(() =>
        {
            IsTextInputVisible = false;
        });

        TextInputKeyCommand = new RelayCommand<string>(key =>
        {
            if (string.IsNullOrEmpty(key)) return;
            TextInputValue += key;
        });

        TextInputBackspaceCommand = new RelayCommand(() =>
        {
            if (TextInputValue.Length > 0)
                TextInputValue = TextInputValue.Substring(0, TextInputValue.Length - 1);
        });

        TextInputClearCommand = new RelayCommand(() =>
        {
            TextInputValue = string.Empty;
        });

        TextInputSpaceCommand = new RelayCommand(() =>
        {
            TextInputValue += " ";
        });
    }

    #endregion

    #region NTRIP Connection State

    private bool _isNtripConnected;
    public bool IsNtripConnected
    {
        get => _isNtripConnected;
        set => this.RaiseAndSetIfChanged(ref _isNtripConnected, value);
    }

    private string _ntripStatus = "Not Connected";
    public string NtripStatus
    {
        get => _ntripStatus;
        set => this.RaiseAndSetIfChanged(ref _ntripStatus, value);
    }

    private string _ntripBytesReceived = "0";
    public string NtripBytesReceived
    {
        get => _ntripBytesReceived;
        set => this.RaiseAndSetIfChanged(ref _ntripBytesReceived, value);
    }

    public ICommand ConnectNtripCommand { get; private set; } = null!;
    public ICommand DisconnectNtripCommand { get; private set; } = null!;

    private async Task ConnectToNtripAsync()
    {
        if (_ntripService == null) return;

        try
        {
            NtripStatus = "Connecting...";
            var config = new NtripConfiguration
            {
                CasterAddress = Connections.NtripCasterHost,
                CasterPort = Connections.NtripCasterPort,
                MountPoint = Connections.NtripMountPoint,
                Username = Connections.NtripUsername,
                Password = Connections.NtripPassword,
                SubnetAddress = "192.168.5",
                UdpForwardPort = 2233,
                GgaIntervalSeconds = 10,
                UseManualPosition = false
            };

            await _ntripService.ConnectAsync(config);
        }
        catch (Exception ex)
        {
            NtripStatus = $"Error: {ex.Message}";
        }
    }

    private async Task DisconnectFromNtripAsync()
    {
        if (_ntripService == null) return;

        try
        {
            await _ntripService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            NtripStatus = $"Error: {ex.Message}";
        }
    }

    private void InitializeNtripConnectionCommands()
    {
        ConnectNtripCommand = new AsyncRelayCommand(ConnectToNtripAsync);
        DisconnectNtripCommand = new AsyncRelayCommand(DisconnectFromNtripAsync);

        // Subscribe to NTRIP events if service is available
        if (_ntripService != null)
        {
            _ntripService.ConnectionStatusChanged += (s, e) =>
            {
                IsNtripConnected = e.IsConnected;
                NtripStatus = e.Message ?? (e.IsConnected ? "Connected" : "Not Connected");
            };

            _ntripService.RtcmDataReceived += (s, e) =>
            {
                NtripBytesReceived = FormatBytes(_ntripService.TotalBytesReceived);
            };

            // Initialize state from service
            IsNtripConnected = _ntripService.IsConnected;
            NtripStatus = _ntripService.IsConnected ? "Connected" : "Not Connected";
            NtripBytesReceived = FormatBytes(_ntripService.TotalBytesReceived);
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    #endregion

    #region Direct Access to Configuration

    /// <summary>
    /// The configuration store - bind directly to sub-configs in XAML
    /// Example: {Binding Config.Vehicle.Wheelbase}
    /// </summary>
    public ConfigurationStore Config => _configService.Store;

    // Convenience accessors for cleaner XAML bindings
    public VehicleConfig Vehicle => Config.Vehicle;
    public ToolConfig Tool => Config.Tool;
    public GuidanceConfig Guidance => Config.Guidance;
    public DisplayConfig Display => Config.Display;
    public SimulatorConfig Simulator => Config.Simulator;
    public ConnectionConfig Connections => Config.Connections;
    public AhrsConfig Ahrs => Config.Ahrs;
    public MachineConfig Machine => Config.Machine;

    // Pin function options for dropdowns
    public ObservableCollection<string> PinFunctionOptions { get; } = new()
    {
        "-", "Section 1", "Section 2", "Section 3", "Section 4",
        "Section 5", "Section 6", "Section 7", "Section 8",
        "Section 9", "Section 10", "Section 11", "Section 12",
        "Section 13", "Section 14", "Section 15", "Section 16",
        "Hyd Up", "Hyd Down", "Tram Left", "Tram Right", "Geo Stop"
    };

    // Individual pin function properties for binding
    public string Pin1Function { get => GetPinFunctionName(0); set => SetPinFunctionByName(0, value); }
    public string Pin2Function { get => GetPinFunctionName(1); set => SetPinFunctionByName(1, value); }
    public string Pin3Function { get => GetPinFunctionName(2); set => SetPinFunctionByName(2, value); }
    public string Pin4Function { get => GetPinFunctionName(3); set => SetPinFunctionByName(3, value); }
    public string Pin5Function { get => GetPinFunctionName(4); set => SetPinFunctionByName(4, value); }
    public string Pin6Function { get => GetPinFunctionName(5); set => SetPinFunctionByName(5, value); }
    public string Pin7Function { get => GetPinFunctionName(6); set => SetPinFunctionByName(6, value); }
    public string Pin8Function { get => GetPinFunctionName(7); set => SetPinFunctionByName(7, value); }
    public string Pin9Function { get => GetPinFunctionName(8); set => SetPinFunctionByName(8, value); }
    public string Pin10Function { get => GetPinFunctionName(9); set => SetPinFunctionByName(9, value); }
    public string Pin11Function { get => GetPinFunctionName(10); set => SetPinFunctionByName(10, value); }
    public string Pin12Function { get => GetPinFunctionName(11); set => SetPinFunctionByName(11, value); }
    public string Pin13Function { get => GetPinFunctionName(12); set => SetPinFunctionByName(12, value); }
    public string Pin14Function { get => GetPinFunctionName(13); set => SetPinFunctionByName(13, value); }
    public string Pin15Function { get => GetPinFunctionName(14); set => SetPinFunctionByName(14, value); }
    public string Pin16Function { get => GetPinFunctionName(15); set => SetPinFunctionByName(15, value); }
    public string Pin17Function { get => GetPinFunctionName(16); set => SetPinFunctionByName(16, value); }
    public string Pin18Function { get => GetPinFunctionName(17); set => SetPinFunctionByName(17, value); }
    public string Pin19Function { get => GetPinFunctionName(18); set => SetPinFunctionByName(18, value); }
    public string Pin20Function { get => GetPinFunctionName(19); set => SetPinFunctionByName(19, value); }
    public string Pin21Function { get => GetPinFunctionName(20); set => SetPinFunctionByName(20, value); }
    public string Pin22Function { get => GetPinFunctionName(21); set => SetPinFunctionByName(21, value); }
    public string Pin23Function { get => GetPinFunctionName(22); set => SetPinFunctionByName(22, value); }
    public string Pin24Function { get => GetPinFunctionName(23); set => SetPinFunctionByName(23, value); }

    private string GetPinFunctionName(int pinIndex)
    {
        var func = Machine.GetPinAssignment(pinIndex);
        return func switch
        {
            PinFunction.None => "-",
            PinFunction.Section1 => "Section 1",
            PinFunction.Section2 => "Section 2",
            PinFunction.Section3 => "Section 3",
            PinFunction.Section4 => "Section 4",
            PinFunction.Section5 => "Section 5",
            PinFunction.Section6 => "Section 6",
            PinFunction.Section7 => "Section 7",
            PinFunction.Section8 => "Section 8",
            PinFunction.Section9 => "Section 9",
            PinFunction.Section10 => "Section 10",
            PinFunction.Section11 => "Section 11",
            PinFunction.Section12 => "Section 12",
            PinFunction.Section13 => "Section 13",
            PinFunction.Section14 => "Section 14",
            PinFunction.Section15 => "Section 15",
            PinFunction.Section16 => "Section 16",
            PinFunction.HydUp => "Hyd Up",
            PinFunction.HydDown => "Hyd Down",
            PinFunction.TramLeft => "Tram Left",
            PinFunction.TramRight => "Tram Right",
            PinFunction.GeoStop => "Geo Stop",
            _ => "-"
        };
    }

    private void SetPinFunctionByName(int pinIndex, string name)
    {
        var func = name switch
        {
            "Section 1" => PinFunction.Section1,
            "Section 2" => PinFunction.Section2,
            "Section 3" => PinFunction.Section3,
            "Section 4" => PinFunction.Section4,
            "Section 5" => PinFunction.Section5,
            "Section 6" => PinFunction.Section6,
            "Section 7" => PinFunction.Section7,
            "Section 8" => PinFunction.Section8,
            "Section 9" => PinFunction.Section9,
            "Section 10" => PinFunction.Section10,
            "Section 11" => PinFunction.Section11,
            "Section 12" => PinFunction.Section12,
            "Section 13" => PinFunction.Section13,
            "Section 14" => PinFunction.Section14,
            "Section 15" => PinFunction.Section15,
            "Section 16" => PinFunction.Section16,
            "Hyd Up" => PinFunction.HydUp,
            "Hyd Down" => PinFunction.HydDown,
            "Tram Left" => PinFunction.TramLeft,
            "Tram Right" => PinFunction.TramRight,
            "Geo Stop" => PinFunction.GeoStop,
            _ => PinFunction.None
        };
        Machine.SetPinAssignment(pinIndex, func);
        Config.MarkChanged();
    }

    private void RefreshAllPinProperties()
    {
        this.RaisePropertyChanged(nameof(Pin1Function));
        this.RaisePropertyChanged(nameof(Pin2Function));
        this.RaisePropertyChanged(nameof(Pin3Function));
        this.RaisePropertyChanged(nameof(Pin4Function));
        this.RaisePropertyChanged(nameof(Pin5Function));
        this.RaisePropertyChanged(nameof(Pin6Function));
        this.RaisePropertyChanged(nameof(Pin7Function));
        this.RaisePropertyChanged(nameof(Pin8Function));
        this.RaisePropertyChanged(nameof(Pin9Function));
        this.RaisePropertyChanged(nameof(Pin10Function));
        this.RaisePropertyChanged(nameof(Pin11Function));
        this.RaisePropertyChanged(nameof(Pin12Function));
        this.RaisePropertyChanged(nameof(Pin13Function));
        this.RaisePropertyChanged(nameof(Pin14Function));
        this.RaisePropertyChanged(nameof(Pin15Function));
        this.RaisePropertyChanged(nameof(Pin16Function));
        this.RaisePropertyChanged(nameof(Pin17Function));
        this.RaisePropertyChanged(nameof(Pin18Function));
        this.RaisePropertyChanged(nameof(Pin19Function));
        this.RaisePropertyChanged(nameof(Pin20Function));
        this.RaisePropertyChanged(nameof(Pin21Function));
        this.RaisePropertyChanged(nameof(Pin22Function));
        this.RaisePropertyChanged(nameof(Pin23Function));
        this.RaisePropertyChanged(nameof(Pin24Function));
    }

    /// <summary>
    /// Calculated total width from sections based on mode.
    /// In Individual mode: sum of first NumSections widths.
    /// In Zone mode: NumSections × DefaultSectionWidth.
    /// </summary>
    public double CalculatedSectionTotal
    {
        get
        {
            if (Tool.IsSectionsNotZones)
            {
                // Individual sections mode - sum actual widths
                double total = 0;
                for (int i = 0; i < Config.NumSections && i < 16; i++)
                    total += Tool.GetSectionWidth(i);
                return total / 100.0; // cm to meters
            }
            else
            {
                // Zones mode - all sections same width
                return Config.NumSections * Tool.DefaultSectionWidth / 100.0;
            }
        }
    }

    /// <summary>
    /// Gets the width of a specific section for display (1-based index).
    /// </summary>
    public double GetSectionWidthForDisplay(int sectionNumber)
    {
        if (sectionNumber < 1 || sectionNumber > 16) return 0;
        return Tool.GetSectionWidth(sectionNumber - 1);
    }

    /// <summary>
    /// Section width display properties for binding (1-based).
    /// </summary>
    public double Section1Width => Tool.GetSectionWidth(0);
    public double Section2Width => Tool.GetSectionWidth(1);
    public double Section3Width => Tool.GetSectionWidth(2);
    public double Section4Width => Tool.GetSectionWidth(3);
    public double Section5Width => Tool.GetSectionWidth(4);
    public double Section6Width => Tool.GetSectionWidth(5);
    public double Section7Width => Tool.GetSectionWidth(6);
    public double Section8Width => Tool.GetSectionWidth(7);
    public double Section9Width => Tool.GetSectionWidth(8);
    public double Section10Width => Tool.GetSectionWidth(9);
    public double Section11Width => Tool.GetSectionWidth(10);
    public double Section12Width => Tool.GetSectionWidth(11);
    public double Section13Width => Tool.GetSectionWidth(12);
    public double Section14Width => Tool.GetSectionWidth(13);
    public double Section15Width => Tool.GetSectionWidth(14);
    public double Section16Width => Tool.GetSectionWidth(15);

    /// <summary>
    /// Refreshes all section width properties after a change.
    /// </summary>
    private void RefreshSectionWidthProperties()
    {
        this.RaisePropertyChanged(nameof(Section1Width));
        this.RaisePropertyChanged(nameof(Section2Width));
        this.RaisePropertyChanged(nameof(Section3Width));
        this.RaisePropertyChanged(nameof(Section4Width));
        this.RaisePropertyChanged(nameof(Section5Width));
        this.RaisePropertyChanged(nameof(Section6Width));
        this.RaisePropertyChanged(nameof(Section7Width));
        this.RaisePropertyChanged(nameof(Section8Width));
        this.RaisePropertyChanged(nameof(Section9Width));
        this.RaisePropertyChanged(nameof(Section10Width));
        this.RaisePropertyChanged(nameof(Section11Width));
        this.RaisePropertyChanged(nameof(Section12Width));
        this.RaisePropertyChanged(nameof(Section13Width));
        this.RaisePropertyChanged(nameof(Section14Width));
        this.RaisePropertyChanged(nameof(Section15Width));
        this.RaisePropertyChanged(nameof(Section16Width));
        this.RaisePropertyChanged(nameof(CalculatedSectionTotal));
    }

    // Zone end section properties (for binding in zone mode)
    public int Zone1EndSection => Tool.GetZoneEndSection(1);
    public int Zone2EndSection => Tool.GetZoneEndSection(2);
    public int Zone3EndSection => Tool.GetZoneEndSection(3);
    public int Zone4EndSection => Tool.GetZoneEndSection(4);
    public int Zone5EndSection => Tool.GetZoneEndSection(5);
    public int Zone6EndSection => Tool.GetZoneEndSection(6);
    public int Zone7EndSection => Tool.GetZoneEndSection(7);
    public int Zone8EndSection => Tool.GetZoneEndSection(8);

    /// <summary>
    /// Refreshes all zone end section properties after a change.
    /// </summary>
    private void RefreshZoneEndProperties()
    {
        this.RaisePropertyChanged(nameof(Zone1EndSection));
        this.RaisePropertyChanged(nameof(Zone2EndSection));
        this.RaisePropertyChanged(nameof(Zone3EndSection));
        this.RaisePropertyChanged(nameof(Zone4EndSection));
        this.RaisePropertyChanged(nameof(Zone5EndSection));
        this.RaisePropertyChanged(nameof(Zone6EndSection));
        this.RaisePropertyChanged(nameof(Zone7EndSection));
        this.RaisePropertyChanged(nameof(Zone8EndSection));
    }

    #endregion

    #region Profile Management

    public ObservableCollection<string> AvailableProfiles { get; } = new();

    private string? _selectedProfileName;
    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
            if (value != null && value != Config.ActiveProfileName)
            {
                _configService.LoadProfile(value);
            }
        }
    }

    /// <summary>
    /// Whether there are unsaved changes (delegates to ConfigurationStore)
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => Config.HasUnsavedChanges;
        set => Config.HasUnsavedChanges = value;
    }

    #endregion

    #region Commands

    public ICommand LoadProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SetToolTypeCommand { get; }
    public ICommand SetVehicleTypeCommand { get; }

    // Vehicle Tab Edit Commands
    public ICommand EditWheelbaseCommand { get; private set; } = null!;
    public ICommand EditTrackWidthCommand { get; private set; } = null!;
    public ICommand EditHitchLengthCommand { get; private set; } = null!;
    public ICommand EditAntennaPivotCommand { get; private set; } = null!;
    public ICommand EditAntennaHeightCommand { get; private set; } = null!;
    public ICommand EditAntennaOffsetCommand { get; private set; } = null!;
    public ICommand SetAntennaOffsetLeftCommand { get; private set; } = null!;
    public ICommand SetAntennaOffsetCenterCommand { get; private set; } = null!;
    public ICommand SetAntennaOffsetRightCommand { get; private set; } = null!;

    // Tool Tab Edit Commands
    public ICommand EditToolWidthCommand { get; private set; } = null!;
    public ICommand EditToolOverlapCommand { get; private set; } = null!;
    public ICommand EditToolOffsetCommand { get; private set; } = null!;
    public ICommand EditToolHitchLengthCommand { get; private set; } = null!;
    public ICommand EditTrailingHitchLengthCommand { get; private set; } = null!;
    public ICommand EditTankHitchLengthCommand { get; private set; } = null!;
    public ICommand EditToolPivotCommand { get; private set; } = null!;
    public ICommand SetPivotBehindCommand { get; private set; } = null!;
    public ICommand SetPivotAheadCommand { get; private set; } = null!;
    public ICommand ZeroToolPivotCommand { get; private set; } = null!;

    // Sections Tab Edit Commands
    public ICommand EditNumSectionsCommand { get; private set; } = null!;
    public ICommand EditLookAheadOnCommand { get; private set; } = null!;
    public ICommand EditLookAheadOffCommand { get; private set; } = null!;
    public ICommand EditTurnOffDelayCommand { get; private set; } = null!;
    public ICommand EditDefaultSectionWidthCommand { get; private set; } = null!;
    public ICommand EditMinCoverageCommand { get; private set; } = null!;
    public ICommand EditCutoffSpeedCommand { get; private set; } = null!;

    // Individual Section Width Edit Commands (1-16)
    public ICommand EditSection1WidthCommand { get; private set; } = null!;
    public ICommand EditSection2WidthCommand { get; private set; } = null!;
    public ICommand EditSection3WidthCommand { get; private set; } = null!;
    public ICommand EditSection4WidthCommand { get; private set; } = null!;
    public ICommand EditSection5WidthCommand { get; private set; } = null!;
    public ICommand EditSection6WidthCommand { get; private set; } = null!;
    public ICommand EditSection7WidthCommand { get; private set; } = null!;
    public ICommand EditSection8WidthCommand { get; private set; } = null!;
    public ICommand EditSection9WidthCommand { get; private set; } = null!;
    public ICommand EditSection10WidthCommand { get; private set; } = null!;
    public ICommand EditSection11WidthCommand { get; private set; } = null!;
    public ICommand EditSection12WidthCommand { get; private set; } = null!;
    public ICommand EditSection13WidthCommand { get; private set; } = null!;
    public ICommand EditSection14WidthCommand { get; private set; } = null!;
    public ICommand EditSection15WidthCommand { get; private set; } = null!;
    public ICommand EditSection16WidthCommand { get; private set; } = null!;

    // Zone Edit Commands
    public ICommand EditNumZonesCommand { get; private set; } = null!;
    public ICommand EditZone1EndCommand { get; private set; } = null!;
    public ICommand EditZone2EndCommand { get; private set; } = null!;
    public ICommand EditZone3EndCommand { get; private set; } = null!;
    public ICommand EditZone4EndCommand { get; private set; } = null!;
    public ICommand EditZone5EndCommand { get; private set; } = null!;
    public ICommand EditZone6EndCommand { get; private set; } = null!;
    public ICommand EditZone7EndCommand { get; private set; } = null!;
    public ICommand EditZone8EndCommand { get; private set; } = null!;

    // U-Turn Tab Edit Commands
    public ICommand EditUTurnRadiusCommand { get; private set; } = null!;
    public ICommand EditUTurnExtensionCommand { get; private set; } = null!;
    public ICommand EditUTurnDistanceCommand { get; private set; } = null!;
    public ICommand EditUTurnSkipWidthCommand { get; private set; } = null!;
    public ICommand EditUTurnSmoothingCommand { get; private set; } = null!;

    // GPS Tab Commands
    public ICommand SetSingleGpsCommand { get; private set; } = null!;
    public ICommand SetDualGpsCommand { get; private set; } = null!;
    public ICommand SetHeadingSourceCommand { get; private set; } = null!;
    public ICommand EditFusionWeightCommand { get; private set; } = null!;
    public ICommand EditMinFixQualityCommand { get; private set; } = null!;
    public ICommand ToggleRtkAlarmCommand { get; private set; } = null!;
    public ICommand SetRtkLostActionCommand { get; private set; } = null!;
    public ICommand EditMaxDiffAgeCommand { get; private set; } = null!;
    public ICommand EditMaxHdopCommand { get; private set; } = null!;
    public ICommand SetGpsUpdateRateCommand { get; private set; } = null!;
    public ICommand ToggleUseRtkCommand { get; private set; } = null!;

    // Dual Antenna Settings Commands
    public ICommand EditDualHeadingOffsetCommand { get; private set; } = null!;
    public ICommand EditDualReverseDistanceCommand { get; private set; } = null!;
    public ICommand ToggleAutoDualFixCommand { get; private set; } = null!;
    public ICommand EditDualSwitchSpeedCommand { get; private set; } = null!;

    // Single Antenna Settings Commands
    public ICommand EditMinGpsStepCommand { get; private set; } = null!;
    public ICommand EditFixToFixDistanceCommand { get; private set; } = null!;
    public ICommand ToggleReverseDetectionCommand { get; private set; } = null!;
    public ICommand ToggleAlarmStopsAutosteerCommand { get; private set; } = null!;

    // Roll Tab Commands
    public ICommand EditRollZeroCommand { get; private set; } = null!;
    public ICommand EditRollFilterCommand { get; private set; } = null!;
    public ICommand ToggleRollInvertCommand { get; private set; } = null!;
    public ICommand SetRollZeroCommand { get; private set; } = null!;

    // Tram Lines Tab Commands
    public ICommand EditTramPassesCommand { get; private set; } = null!;
    public ICommand ToggleTramDisplayCommand { get; private set; } = null!;
    public ICommand EditTramLineCommand { get; private set; } = null!;

    // Machine Control Tab Commands
    public ICommand ToggleHydraulicLiftCommand { get; private set; } = null!;
    public ICommand EditRaiseTimeCommand { get; private set; } = null!;
    public ICommand EditLookAheadCommand { get; private set; } = null!;
    public ICommand EditLowerTimeCommand { get; private set; } = null!;
    public ICommand ToggleInvertRelayCommand { get; private set; } = null!;
    public ICommand EditUser1Command { get; private set; } = null!;
    public ICommand EditUser2Command { get; private set; } = null!;
    public ICommand EditUser3Command { get; private set; } = null!;
    public ICommand EditUser4Command { get; private set; } = null!;
    public ICommand ResetPinConfigCommand { get; private set; } = null!;
    public ICommand UploadPinConfigCommand { get; private set; } = null!;
    public ICommand SendAndSaveMachineConfigCommand { get; private set; } = null!;

    // Display Options Tab Commands
    public ICommand TogglePolygonsCommand { get; private set; } = null!;
    public ICommand ToggleSpeedometerCommand { get; private set; } = null!;
    public ICommand ToggleKeyboardCommand { get; private set; } = null!;
    public ICommand ToggleHeadlandDistanceCommand { get; private set; } = null!;
    public ICommand ToggleAutoDayNightCommand { get; private set; } = null!;
    public ICommand ToggleSvennArrowCommand { get; private set; } = null!;
    public ICommand ToggleStartFullscreenCommand { get; private set; } = null!;
    public ICommand ToggleElevationLogCommand { get; private set; } = null!;
    public ICommand ToggleFieldTextureCommand { get; private set; } = null!;
    public ICommand ToggleGridCommand { get; private set; } = null!;
    public ICommand ToggleExtraGuidelinesCommand { get; private set; } = null!;
    public ICommand EditExtraGuidelinesCountCommand { get; private set; } = null!;
    public ICommand ToggleLineSmoothCommand { get; private set; } = null!;
    public ICommand ToggleDirectionMarkersCommand { get; private set; } = null!;
    public ICommand ToggleSectionLinesCommand { get; private set; } = null!;
    public ICommand SetMetricUnitsCommand { get; private set; } = null!;
    public ICommand SetImperialUnitsCommand { get; private set; } = null!;

    // Additional Options Tab Commands
    public ICommand ToggleUTurnButtonCommand { get; private set; } = null!;
    public ICommand ToggleLateralButtonCommand { get; private set; } = null!;
    public ICommand ToggleAutoSteerSoundCommand { get; private set; } = null!;
    public ICommand ToggleUTurnSoundCommand { get; private set; } = null!;
    public ICommand ToggleHydraulicSoundCommand { get; private set; } = null!;
    public ICommand ToggleSectionsSoundCommand { get; private set; } = null!;
    public ICommand ToggleHardwareMessagesCommand { get; private set; } = null!;

    // NTRIP Tab Commands
    public ICommand EditNtripCasterHostCommand { get; private set; } = null!;
    public ICommand EditNtripCasterPortCommand { get; private set; } = null!;
    public ICommand EditNtripMountPointCommand { get; private set; } = null!;
    public ICommand EditNtripUsernameCommand { get; private set; } = null!;
    public ICommand EditNtripPasswordCommand { get; private set; } = null!;
    public ICommand ToggleNtripAutoConnectCommand { get; private set; } = null!;

    #endregion

    #region Events

    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ProfileSaved;

    #endregion

    public ConfigurationViewModel(IConfigurationService configService, INtripClientService? ntripService = null)
    {
        _configService = configService;
        _ntripService = ntripService;

        // Initialize commands
        LoadProfileCommand = new RelayCommand<string>(LoadProfile);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        NewProfileCommand = new RelayCommand<string>(CreateNewProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        ApplyCommand = new RelayCommand(ApplyChanges);
        CancelCommand = new RelayCommand(Cancel);
        SetToolTypeCommand = new RelayCommand<string>(SetToolType);
        SetVehicleTypeCommand = new RelayCommand<string>(SetVehicleType);

        // Initialize numeric input commands
        InitializeNumericInputCommands();
        InitializeTextInputCommands();

        // Initialize edit commands for all tabs
        InitializeVehicleEditCommands();
        InitializeToolEditCommands();
        InitializeSectionsEditCommands();
        InitializeUTurnEditCommands();
        InitializeGpsEditCommands();
        InitializeRollEditCommands();
        InitializeTramCommands();
        InitializeMachineCommands();
        InitializeDisplayCommands();
        InitializeAdditionalOptionsCommands();
        InitializeNtripCommands();
        InitializeNtripConnectionCommands();

        // Subscribe to config changes for HasUnsavedChanges notification
        Config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfigurationStore.HasUnsavedChanges))
            {
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));
            }
            // Update calculated section total when NumSections changes
            if (e.PropertyName == nameof(ConfigurationStore.NumSections))
            {
                this.RaisePropertyChanged(nameof(CalculatedSectionTotal));
            }
        };

        // Subscribe to tool changes for CalculatedSectionTotal
        Tool.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToolConfig.DefaultSectionWidth))
            {
                this.RaisePropertyChanged(nameof(CalculatedSectionTotal));
            }
        };

        // Load available profiles
        RefreshProfileList();

        // Set selected profile name to current
        _selectedProfileName = Config.ActiveProfileName;
    }

    private void InitializeVehicleEditCommands()
    {
        // Vehicle dimensions stored in meters, edit in meters
        EditWheelbaseCommand = new RelayCommand(() =>
            ShowNumericInput("Wheelbase", Vehicle.Wheelbase,
                v => Vehicle.Wheelbase = v,
                "m", integerOnly: false, allowNegative: false, min: 0.5, max: 10));

        EditTrackWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Track Width", Vehicle.TrackWidth,
                v => Vehicle.TrackWidth = v,
                "m", integerOnly: false, allowNegative: false, min: 0.5, max: 5));

        EditHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Hitch Length", Tool.HitchLength,
                v => Tool.HitchLength = v,
                "m", integerOnly: false, allowNegative: true, min: -15, max: 15));

        EditAntennaPivotCommand = new RelayCommand(() =>
            ShowNumericInput("Antenna Pivot", Vehicle.AntennaPivot,
                v => Vehicle.AntennaPivot = v,
                "m", integerOnly: false, allowNegative: true, min: -10, max: 10));

        EditAntennaHeightCommand = new RelayCommand(() =>
            ShowNumericInput("Antenna Height", Vehicle.AntennaHeight,
                v => Vehicle.AntennaHeight = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 10));

        EditAntennaOffsetCommand = new RelayCommand(() =>
            ShowNumericInput("Antenna Offset", Vehicle.AntennaOffset,
                v => Vehicle.AntennaOffset = v,
                "m", integerOnly: false, allowNegative: true, min: -5, max: 5));

        // Antenna offset direction buttons - set the SIGN of the current offset
        // Left = antenna is LEFT of tractor center = negative offset
        // Center = antenna is on centerline = zero offset
        // Right = antenna is RIGHT of tractor center = positive offset
        SetAntennaOffsetLeftCommand = new RelayCommand(() =>
        {
            Vehicle.AntennaOffset = -Math.Abs(Vehicle.AntennaOffset);
            if (Math.Abs(Vehicle.AntennaOffset) < 0.01)
                Vehicle.AntennaOffset = -0.5; // Default to 0.5m if currently zero
        });

        SetAntennaOffsetCenterCommand = new RelayCommand(() =>
        {
            Vehicle.AntennaOffset = 0;
        });

        SetAntennaOffsetRightCommand = new RelayCommand(() =>
        {
            Vehicle.AntennaOffset = Math.Abs(Vehicle.AntennaOffset);
            if (Math.Abs(Vehicle.AntennaOffset) < 0.01)
                Vehicle.AntennaOffset = 0.5; // Default to 0.5m if currently zero
        });
    }

    private void InitializeToolEditCommands()
    {
        // Tool dimensions stored in meters, edit in meters
        EditToolWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Width", Tool.Width,
                v => Tool.Width = v,
                "m", integerOnly: false, allowNegative: false, min: 0.5, max: 50));

        EditToolOverlapCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Overlap", Tool.Overlap,
                v => Tool.Overlap = v,
                "m", integerOnly: false, allowNegative: true, min: -2, max: 2));

        EditToolOffsetCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Offset", Tool.Offset,
                v => Tool.Offset = v,
                "m", integerOnly: false, allowNegative: true, min: -5, max: 5));

        EditToolHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Hitch Length", Tool.HitchLength,
                v => Tool.HitchLength = v,
                "m", integerOnly: false, allowNegative: true, min: -15, max: 15));

        EditTrailingHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Trailing Hitch Length", Tool.TrailingHitchLength,
                v => Tool.TrailingHitchLength = v,
                "m", integerOnly: false, allowNegative: true, min: -15, max: 15));

        EditTankHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Tank Hitch Length", Tool.TankTrailingHitchLength,
                v => Tool.TankTrailingHitchLength = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 15));

        EditToolPivotCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Pivot Distance", Tool.TrailingToolToPivotLength,
                v => Tool.TrailingToolToPivotLength = v,
                "m", integerOnly: false, allowNegative: true, min: -10, max: 10));

        SetPivotBehindCommand = new RelayCommand(() =>
        {
            // If current value is negative, make it positive (behind pivot)
            if (Tool.TrailingToolToPivotLength < 0)
                Tool.TrailingToolToPivotLength = Math.Abs(Tool.TrailingToolToPivotLength);
        });

        SetPivotAheadCommand = new RelayCommand(() =>
        {
            // If current value is positive, make it negative (ahead of pivot)
            if (Tool.TrailingToolToPivotLength > 0)
                Tool.TrailingToolToPivotLength = -Math.Abs(Tool.TrailingToolToPivotLength);
        });

        ZeroToolPivotCommand = new RelayCommand(() =>
        {
            Tool.TrailingToolToPivotLength = 0;
        });
    }

    private void InitializeSectionsEditCommands()
    {
        EditNumSectionsCommand = new RelayCommand(() =>
            ShowNumericInput("Number of Sections", Config.NumSections,
                v => Config.NumSections = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 16));

        EditLookAheadOnCommand = new RelayCommand(() =>
            ShowNumericInput("Look Ahead On", Tool.LookAheadOnSetting,
                v => Tool.LookAheadOnSetting = v,
                "s", integerOnly: false, allowNegative: false, min: 0, max: 5));

        EditLookAheadOffCommand = new RelayCommand(() =>
            ShowNumericInput("Look Ahead Off", Tool.LookAheadOffSetting,
                v => Tool.LookAheadOffSetting = v,
                "s", integerOnly: false, allowNegative: false, min: 0, max: 5));

        EditTurnOffDelayCommand = new RelayCommand(() =>
            ShowNumericInput("Turn Off Delay", Tool.TurnOffDelay,
                v => Tool.TurnOffDelay = v,
                "s", integerOnly: false, allowNegative: false, min: 0, max: 5));

        EditDefaultSectionWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Default Section Width", Tool.DefaultSectionWidth,
                v => Tool.DefaultSectionWidth = v,
                "cm", integerOnly: false, allowNegative: false, min: 10, max: 500));

        EditMinCoverageCommand = new RelayCommand(() =>
            ShowNumericInput("Minimum Coverage", Tool.MinCoverage,
                v => Tool.MinCoverage = (int)v,
                "%", integerOnly: true, allowNegative: false, min: 0, max: 100));

        EditCutoffSpeedCommand = new RelayCommand(() =>
            ShowNumericInput("Slow Speed Cutoff", Tool.SlowSpeedCutoff,
                v => Tool.SlowSpeedCutoff = v,
                "km/h", integerOnly: false, allowNegative: false, min: 0, max: 10));

        // Individual section width commands
        EditSection1WidthCommand = new RelayCommand(() => EditSectionWidth(1));
        EditSection2WidthCommand = new RelayCommand(() => EditSectionWidth(2));
        EditSection3WidthCommand = new RelayCommand(() => EditSectionWidth(3));
        EditSection4WidthCommand = new RelayCommand(() => EditSectionWidth(4));
        EditSection5WidthCommand = new RelayCommand(() => EditSectionWidth(5));
        EditSection6WidthCommand = new RelayCommand(() => EditSectionWidth(6));
        EditSection7WidthCommand = new RelayCommand(() => EditSectionWidth(7));
        EditSection8WidthCommand = new RelayCommand(() => EditSectionWidth(8));
        EditSection9WidthCommand = new RelayCommand(() => EditSectionWidth(9));
        EditSection10WidthCommand = new RelayCommand(() => EditSectionWidth(10));
        EditSection11WidthCommand = new RelayCommand(() => EditSectionWidth(11));
        EditSection12WidthCommand = new RelayCommand(() => EditSectionWidth(12));
        EditSection13WidthCommand = new RelayCommand(() => EditSectionWidth(13));
        EditSection14WidthCommand = new RelayCommand(() => EditSectionWidth(14));
        EditSection15WidthCommand = new RelayCommand(() => EditSectionWidth(15));
        EditSection16WidthCommand = new RelayCommand(() => EditSectionWidth(16));

        // Zone edit command
        EditNumZonesCommand = new RelayCommand(() =>
            ShowNumericInput("Number of Zones", Tool.Zones,
                v => { Tool.Zones = (int)v; RefreshZoneEndProperties(); },
                "", integerOnly: true, allowNegative: false, min: 2, max: 8));

        // Zone end section commands
        EditZone1EndCommand = new RelayCommand(() => EditZoneEndSection(1));
        EditZone2EndCommand = new RelayCommand(() => EditZoneEndSection(2));
        EditZone3EndCommand = new RelayCommand(() => EditZoneEndSection(3));
        EditZone4EndCommand = new RelayCommand(() => EditZoneEndSection(4));
        EditZone5EndCommand = new RelayCommand(() => EditZoneEndSection(5));
        EditZone6EndCommand = new RelayCommand(() => EditZoneEndSection(6));
        EditZone7EndCommand = new RelayCommand(() => EditZoneEndSection(7));
        EditZone8EndCommand = new RelayCommand(() => EditZoneEndSection(8));
    }

    /// <summary>
    /// Edit an individual section width (1-based section number).
    /// </summary>
    private void EditSectionWidth(int sectionNumber)
    {
        int index = sectionNumber - 1;
        double currentWidth = Tool.GetSectionWidth(index);
        ShowNumericInput($"Section {sectionNumber} Width", currentWidth,
            v =>
            {
                Tool.SetSectionWidth(index, v);
                RefreshSectionWidthProperties();
            },
            "cm", integerOnly: false, allowNegative: false, min: 1, max: 500);
    }

    /// <summary>
    /// Edit a zone's end section (1-based zone number).
    /// Zone N contains sections from the previous zone's end + 1 to this zone's end.
    /// </summary>
    private void EditZoneEndSection(int zoneNumber)
    {
        int currentEnd = Tool.GetZoneEndSection(zoneNumber);
        int minSection = zoneNumber; // Must have at least one section per zone
        if (zoneNumber > 1)
            minSection = Tool.GetZoneEndSection(zoneNumber - 1) + 1;

        ShowNumericInput($"Zone {zoneNumber} End Section", currentEnd,
            v =>
            {
                Tool.SetZoneEndSection(zoneNumber, (int)v);
                RefreshZoneEndProperties();
            },
            "", integerOnly: true, allowNegative: false, min: minSection, max: Config.NumSections);
    }

    private void InitializeUTurnEditCommands()
    {
        EditUTurnRadiusCommand = new RelayCommand(() =>
            ShowNumericInput("U-Turn Radius", Guidance.UTurnRadius,
                v => Guidance.UTurnRadius = v,
                "m", integerOnly: false, allowNegative: false, min: 2, max: 30));

        EditUTurnExtensionCommand = new RelayCommand(() =>
            ShowNumericInput("U-Turn Extension", Guidance.UTurnExtension,
                v => Guidance.UTurnExtension = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 50));

        EditUTurnDistanceCommand = new RelayCommand(() =>
            ShowNumericInput("Distance from Boundary", Guidance.UTurnDistanceFromBoundary,
                v => Guidance.UTurnDistanceFromBoundary = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 10));

        EditUTurnSkipWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Skip Width", Guidance.UTurnSkipWidth,
                v => Guidance.UTurnSkipWidth = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 10));

        EditUTurnSmoothingCommand = new RelayCommand(() =>
            ShowNumericInput("Smoothing", Guidance.UTurnSmoothing,
                v => Guidance.UTurnSmoothing = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 50));
    }

    private void InitializeGpsEditCommands()
    {
        // GPS Mode commands
        SetSingleGpsCommand = new RelayCommand(() =>
        {
            Connections.IsDualGps = false;
            Config.MarkChanged();
        });

        SetDualGpsCommand = new RelayCommand(() =>
        {
            Connections.IsDualGps = true;
            Config.MarkChanged();
        });

        // Heading source command (parameter is source index as string)
        SetHeadingSourceCommand = new RelayCommand<string>(source =>
        {
            if (int.TryParse(source, out var sourceIndex))
            {
                Connections.HeadingSource = sourceIndex;
                Config.MarkChanged();
            }
        });

        EditFusionWeightCommand = new RelayCommand(() =>
            ShowNumericInput("Heading Fusion Weight", Connections.HeadingFusionWeight,
                v => Connections.HeadingFusionWeight = v,
                "", integerOnly: false, allowNegative: false, min: 0, max: 1));

        EditMinFixQualityCommand = new RelayCommand(() =>
            ShowNumericInput("Minimum Fix Quality", Connections.MinFixQuality,
                v => Connections.MinFixQuality = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 5));

        ToggleRtkAlarmCommand = new RelayCommand(() =>
        {
            Connections.RtkLostAlarm = !Connections.RtkLostAlarm;
            Config.MarkChanged();
        });

        SetRtkLostActionCommand = new RelayCommand<string>(action =>
        {
            if (int.TryParse(action, out var actionIndex))
            {
                Connections.RtkLostAction = actionIndex;
                Config.MarkChanged();
            }
        });

        EditMaxDiffAgeCommand = new RelayCommand(() =>
            ShowNumericInput("Max Differential Age", Connections.MaxDifferentialAge,
                v => Connections.MaxDifferentialAge = v,
                "sec", integerOnly: false, allowNegative: false, min: 1, max: 30));

        EditMaxHdopCommand = new RelayCommand(() =>
            ShowNumericInput("Max HDOP", Connections.MaxHdop,
                v => Connections.MaxHdop = v,
                "", integerOnly: false, allowNegative: false, min: 0.5, max: 10));

        SetGpsUpdateRateCommand = new RelayCommand<string>(rate =>
        {
            if (int.TryParse(rate, out var rateHz))
            {
                Connections.GpsUpdateRate = rateHz;
                Config.MarkChanged();
            }
        });

        ToggleUseRtkCommand = new RelayCommand(() =>
        {
            Connections.UseRtk = !Connections.UseRtk;
            Config.MarkChanged();
        });

        // Dual Antenna Settings
        EditDualHeadingOffsetCommand = new RelayCommand(() =>
            ShowNumericInput("Heading Offset", Connections.DualHeadingOffset,
                v => Connections.DualHeadingOffset = v,
                "°", integerOnly: false, allowNegative: false, min: 0, max: 360));

        EditDualReverseDistanceCommand = new RelayCommand(() =>
            ShowNumericInput("Reverse Distance", Connections.DualReverseDistance,
                v => Connections.DualReverseDistance = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 5));

        ToggleAutoDualFixCommand = new RelayCommand(() =>
        {
            Connections.AutoDualFix = !Connections.AutoDualFix;
            Config.MarkChanged();
        });

        EditDualSwitchSpeedCommand = new RelayCommand(() =>
            ShowNumericInput("Switch Speed", Connections.DualSwitchSpeed,
                v => Connections.DualSwitchSpeed = v,
                "km/h", integerOnly: false, allowNegative: false, min: 0, max: 10));

        // Single Antenna Settings
        EditMinGpsStepCommand = new RelayCommand(() =>
            ShowNumericInput("Minimum GPS Step", Connections.MinGpsStep,
                v => Connections.MinGpsStep = v,
                "m", integerOnly: false, allowNegative: false, min: 0.01, max: 1));

        EditFixToFixDistanceCommand = new RelayCommand(() =>
            ShowNumericInput("Fix to Fix Distance", Connections.FixToFixDistance,
                v => Connections.FixToFixDistance = v,
                "m", integerOnly: false, allowNegative: false, min: 0.1, max: 5));

        ToggleReverseDetectionCommand = new RelayCommand(() =>
        {
            Connections.ReverseDetection = !Connections.ReverseDetection;
            Config.MarkChanged();
        });

        ToggleAlarmStopsAutosteerCommand = new RelayCommand(() =>
        {
            // Toggle between RtkLostAction 0 (Warn) and 1 (Pause AutoSteer)
            Connections.RtkLostAction = Connections.RtkLostAction == 1 ? 0 : 1;
            Config.MarkChanged();
        });
    }

    private void InitializeRollEditCommands()
    {
        EditRollZeroCommand = new RelayCommand(() =>
            ShowNumericInput("Roll Zero Offset", Ahrs.RollZero,
                v => Ahrs.RollZero = v,
                "°", integerOnly: false, allowNegative: true, min: -20, max: 20));

        EditRollFilterCommand = new RelayCommand(() =>
            ShowNumericInput("Roll Filter", Ahrs.RollFilter,
                v => Ahrs.RollFilter = v,
                "", integerOnly: false, allowNegative: false, min: 0, max: 1));

        ToggleRollInvertCommand = new RelayCommand(() =>
        {
            Ahrs.IsRollInvert = !Ahrs.IsRollInvert;
            Config.MarkChanged();
        });

        // Set roll zero to current roll value (would need access to current sensor data)
        // For now, this just resets to 0
        SetRollZeroCommand = new RelayCommand(() =>
        {
            Ahrs.RollZero = 0;
            Config.MarkChanged();
        });
    }

    private void InitializeTramCommands()
    {
        EditTramPassesCommand = new RelayCommand(() =>
            ShowNumericInput("Tram Passes", Guidance.TramPasses,
                v => Guidance.TramPasses = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 20));

        ToggleTramDisplayCommand = new RelayCommand(() =>
        {
            Guidance.TramDisplay = !Guidance.TramDisplay;
            Config.MarkChanged();
        });

        EditTramLineCommand = new RelayCommand(() =>
            ShowNumericInput("Tram Line", Guidance.TramLine,
                v => Guidance.TramLine = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 100));
    }

    private void InitializeMachineCommands()
    {
        // Machine Module Commands
        ToggleHydraulicLiftCommand = new RelayCommand(() =>
        {
            Machine.HydraulicLiftEnabled = !Machine.HydraulicLiftEnabled;
            Config.MarkChanged();
        });

        EditRaiseTimeCommand = new RelayCommand(() =>
            ShowNumericInput("Raise Time", Machine.RaiseTime,
                v => Machine.RaiseTime = (int)v,
                "sec", integerOnly: true, allowNegative: false, min: 0, max: 20));

        EditLookAheadCommand = new RelayCommand(() =>
            ShowNumericInput("Look Ahead", Machine.LookAhead,
                v => Machine.LookAhead = v,
                "sec", integerOnly: false, allowNegative: false, min: 0, max: 10));

        EditLowerTimeCommand = new RelayCommand(() =>
            ShowNumericInput("Lower Time", Machine.LowerTime,
                v => Machine.LowerTime = (int)v,
                "sec", integerOnly: true, allowNegative: false, min: 0, max: 20));

        ToggleInvertRelayCommand = new RelayCommand(() =>
        {
            Machine.InvertRelay = !Machine.InvertRelay;
            Config.MarkChanged();
        });

        // User Value Commands
        EditUser1Command = new RelayCommand(() =>
            ShowNumericInput("User 1", Machine.User1Value,
                v => Machine.User1Value = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 255));

        EditUser2Command = new RelayCommand(() =>
            ShowNumericInput("User 2", Machine.User2Value,
                v => Machine.User2Value = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 255));

        EditUser3Command = new RelayCommand(() =>
            ShowNumericInput("User 3", Machine.User3Value,
                v => Machine.User3Value = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 255));

        EditUser4Command = new RelayCommand(() =>
            ShowNumericInput("User 4", Machine.User4Value,
                v => Machine.User4Value = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 255));

        // Pin Config Commands
        ResetPinConfigCommand = new RelayCommand(() =>
        {
            Machine.ResetPinAssignments();
            RefreshAllPinProperties();
            Config.MarkChanged();
        });

        UploadPinConfigCommand = new RelayCommand(() =>
        {
            // TODO: Implement upload from hardware
            // This would typically read the current pin config from the machine module
        });

        SendAndSaveMachineConfigCommand = new RelayCommand(() =>
        {
            // TODO: Implement send to hardware
            // This would send the current config to the machine module via UDP
            // For now, just mark as saved
            Config.MarkChanged();
        });
    }

    private void InitializeDisplayCommands()
    {
        TogglePolygonsCommand = new RelayCommand(() =>
        {
            Display.PolygonsVisible = !Display.PolygonsVisible;
            Config.MarkChanged();
        });

        ToggleSpeedometerCommand = new RelayCommand(() =>
        {
            Display.SpeedometerVisible = !Display.SpeedometerVisible;
            Config.MarkChanged();
        });

        ToggleKeyboardCommand = new RelayCommand(() =>
        {
            Display.KeyboardEnabled = !Display.KeyboardEnabled;
            Config.MarkChanged();
        });

        ToggleHeadlandDistanceCommand = new RelayCommand(() =>
        {
            Display.HeadlandDistanceVisible = !Display.HeadlandDistanceVisible;
            Config.MarkChanged();
        });

        ToggleAutoDayNightCommand = new RelayCommand(() =>
        {
            Display.AutoDayNight = !Display.AutoDayNight;
            Config.MarkChanged();
        });

        ToggleSvennArrowCommand = new RelayCommand(() =>
        {
            Display.SvennArrowVisible = !Display.SvennArrowVisible;
            Config.MarkChanged();
        });

        ToggleStartFullscreenCommand = new RelayCommand(() =>
        {
            Display.StartFullscreen = !Display.StartFullscreen;
            Config.MarkChanged();
        });

        ToggleElevationLogCommand = new RelayCommand(() =>
        {
            Display.ElevationLogEnabled = !Display.ElevationLogEnabled;
            Config.MarkChanged();
        });

        ToggleFieldTextureCommand = new RelayCommand(() =>
        {
            Display.FieldTextureVisible = !Display.FieldTextureVisible;
            Config.MarkChanged();
        });

        ToggleGridCommand = new RelayCommand(() =>
        {
            Display.GridVisible = !Display.GridVisible;
            Config.MarkChanged();
        });

        ToggleExtraGuidelinesCommand = new RelayCommand(() =>
        {
            Display.ExtraGuidelines = !Display.ExtraGuidelines;
            Config.MarkChanged();
        });

        EditExtraGuidelinesCountCommand = new RelayCommand(() =>
            ShowNumericInput("Extra Guidelines Count", Display.ExtraGuidelinesCount,
                v => Display.ExtraGuidelinesCount = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 50));

        ToggleLineSmoothCommand = new RelayCommand(() =>
        {
            Display.LineSmoothEnabled = !Display.LineSmoothEnabled;
            Config.MarkChanged();
        });

        ToggleDirectionMarkersCommand = new RelayCommand(() =>
        {
            Display.DirectionMarkersVisible = !Display.DirectionMarkersVisible;
            Config.MarkChanged();
        });

        ToggleSectionLinesCommand = new RelayCommand(() =>
        {
            Display.SectionLinesVisible = !Display.SectionLinesVisible;
            Config.MarkChanged();
        });

        SetMetricUnitsCommand = new RelayCommand(() =>
        {
            Config.IsMetric = true;
            Config.MarkChanged();
        });

        SetImperialUnitsCommand = new RelayCommand(() =>
        {
            Config.IsMetric = false;
            Config.MarkChanged();
        });
    }

    private void InitializeAdditionalOptionsCommands()
    {
        // Screen Buttons
        ToggleUTurnButtonCommand = new RelayCommand(() =>
        {
            Display.UTurnButtonVisible = !Display.UTurnButtonVisible;
            Config.MarkChanged();
        });

        ToggleLateralButtonCommand = new RelayCommand(() =>
        {
            Display.LateralButtonVisible = !Display.LateralButtonVisible;
            Config.MarkChanged();
        });

        // Sounds
        ToggleAutoSteerSoundCommand = new RelayCommand(() =>
        {
            Display.AutoSteerSound = !Display.AutoSteerSound;
            Config.MarkChanged();
        });

        ToggleUTurnSoundCommand = new RelayCommand(() =>
        {
            Display.UTurnSound = !Display.UTurnSound;
            Config.MarkChanged();
        });

        ToggleHydraulicSoundCommand = new RelayCommand(() =>
        {
            Display.HydraulicSound = !Display.HydraulicSound;
            Config.MarkChanged();
        });

        ToggleSectionsSoundCommand = new RelayCommand(() =>
        {
            Display.SectionsSound = !Display.SectionsSound;
            Config.MarkChanged();
        });

        // Hardware Messages
        ToggleHardwareMessagesCommand = new RelayCommand(() =>
        {
            Display.HardwareMessagesEnabled = !Display.HardwareMessagesEnabled;
            Config.MarkChanged();
        });
    }

    private void InitializeNtripCommands()
    {
        EditNtripCasterHostCommand = new RelayCommand(() =>
        {
            ShowTextInput("Caster Host", Connections.NtripCasterHost, value =>
            {
                Connections.NtripCasterHost = value;
                Config.MarkChanged();
            });
        });

        EditNtripCasterPortCommand = new RelayCommand(() =>
        {
            ShowNumericInput("Caster Port", Connections.NtripCasterPort,
                value =>
                {
                    Connections.NtripCasterPort = (int)value;
                    Config.MarkChanged();
                }, "", true, false, 1, 65535);
        });

        EditNtripMountPointCommand = new RelayCommand(() =>
        {
            ShowTextInput("Mount Point", Connections.NtripMountPoint, value =>
            {
                Connections.NtripMountPoint = value;
                Config.MarkChanged();
            });
        });

        EditNtripUsernameCommand = new RelayCommand(() =>
        {
            ShowTextInput("Username", Connections.NtripUsername, value =>
            {
                Connections.NtripUsername = value;
                Config.MarkChanged();
            });
        });

        EditNtripPasswordCommand = new RelayCommand(() =>
        {
            ShowTextInput("Password", Connections.NtripPassword, value =>
            {
                Connections.NtripPassword = value;
                Config.MarkChanged();
            }, isPassword: true);
        });

        ToggleNtripAutoConnectCommand = new RelayCommand(() =>
        {
            Connections.NtripAutoConnect = !Connections.NtripAutoConnect;
            Config.MarkChanged();
        });
    }

    private void RefreshProfileList()
    {
        AvailableProfiles.Clear();
        foreach (var profileName in _configService.GetAvailableProfiles())
        {
            AvailableProfiles.Add(profileName);
        }
    }

    private void LoadProfile(string? profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return;
        _configService.LoadProfile(profileName);
        _selectedProfileName = profileName;
        this.RaisePropertyChanged(nameof(SelectedProfileName));
    }

    private void SaveProfile()
    {
        _configService.SaveProfile(Config.ActiveProfileName);
        ProfileSaved?.Invoke(this, Config.ActiveProfileName);
    }

    private void CreateNewProfile(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;

        _configService.CreateProfile(profileName);
        RefreshProfileList();
        _selectedProfileName = profileName;
        this.RaisePropertyChanged(nameof(SelectedProfileName));
    }

    private void DeleteProfile()
    {
        if (string.IsNullOrEmpty(SelectedProfileName)) return;
        if (SelectedProfileName == Config.ActiveProfileName) return; // Can't delete active

        _configService.DeleteProfile(SelectedProfileName);
        RefreshProfileList();
    }

    private void ApplyChanges()
    {
        _configService.SaveProfile(Config.ActiveProfileName);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        _configService.ReloadCurrentProfile();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetToolType(string? toolType)
    {
        if (string.IsNullOrEmpty(toolType)) return;
        Tool.SetToolType(toolType);
        Config.MarkChanged();
    }

    private void SetVehicleType(string? vehicleType)
    {
        if (string.IsNullOrEmpty(vehicleType)) return;

        Vehicle.Type = vehicleType.ToLowerInvariant() switch
        {
            "tractor" => VehicleType.Tractor,
            "harvester" => VehicleType.Harvester,
            "fourwd" or "4wd" or "articulated" => VehicleType.FourWD,
            _ => VehicleType.Tractor
        };
        Config.MarkChanged();
    }
}
