using System;
using System.Windows.Input;
using ReactiveUI;
using Avalonia.Threading;
using AgValoniaGPS.Models.Communication;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for section control functionality.
/// Handles section master state, individual section states, and section-related settings.
/// Integrates with ISectionControlService for 3-state section control (Off/Auto/On).
/// </summary>
public class SectionControlViewModel : ReactiveObject
{
    private readonly IModuleCommunicationService _moduleCommunicationService;
    private readonly ISectionControlService _sectionControlService;
    private readonly ApplicationState _appState;

    // Events for MainViewModel to subscribe to
    public event EventHandler<string>? StatusMessageChanged;

    public SectionControlViewModel(
        IModuleCommunicationService moduleCommunicationService,
        ISectionControlService sectionControlService,
        ApplicationState appState)
    {
        _moduleCommunicationService = moduleCommunicationService;
        _sectionControlService = sectionControlService;
        _appState = appState;

        // Subscribe to module communication events
        _moduleCommunicationService.SectionMasterToggleRequested += OnSectionMasterToggleRequested;

        // Subscribe to section control service events
        _sectionControlService.SectionStateChanged += OnSectionStateChanged;

        InitializeCommands();
    }

    #region Section Master Properties

    private bool _isMasterOn;
    /// <summary>
    /// Whether section master is on (enables section control).
    /// </summary>
    public bool IsMasterOn
    {
        get => _isMasterOn;
        set
        {
            if (_isMasterOn != value)
            {
                this.RaiseAndSetIfChanged(ref _isMasterOn, value);
                _appState.Sections.IsMasterOn = value;
            }
        }
    }

    private bool _isManualMode;
    /// <summary>
    /// Whether in manual section mode (vs auto).
    /// </summary>
    public bool IsManualMode
    {
        get => _isManualMode;
        set
        {
            if (_isManualMode != value)
            {
                this.RaiseAndSetIfChanged(ref _isManualMode, value);
                _appState.Sections.IsManualMode = value;
            }
        }
    }

    private int _activeCount;
    /// <summary>
    /// Number of active sections.
    /// </summary>
    public int ActiveCount
    {
        get => _activeCount;
        set => this.RaiseAndSetIfChanged(ref _activeCount, value);
    }

    #endregion

    #region Individual Section States

    private bool _section1Active;
    public bool Section1Active
    {
        get => _section1Active;
        set => this.RaiseAndSetIfChanged(ref _section1Active, value);
    }

    private bool _section2Active;
    public bool Section2Active
    {
        get => _section2Active;
        set => this.RaiseAndSetIfChanged(ref _section2Active, value);
    }

    private bool _section3Active;
    public bool Section3Active
    {
        get => _section3Active;
        set => this.RaiseAndSetIfChanged(ref _section3Active, value);
    }

    private bool _section4Active;
    public bool Section4Active
    {
        get => _section4Active;
        set => this.RaiseAndSetIfChanged(ref _section4Active, value);
    }

    private bool _section5Active;
    public bool Section5Active
    {
        get => _section5Active;
        set => this.RaiseAndSetIfChanged(ref _section5Active, value);
    }

    private bool _section6Active;
    public bool Section6Active
    {
        get => _section6Active;
        set => this.RaiseAndSetIfChanged(ref _section6Active, value);
    }

    private bool _section7Active;
    public bool Section7Active
    {
        get => _section7Active;
        set => this.RaiseAndSetIfChanged(ref _section7Active, value);
    }

    #endregion

    #region Section Color Codes (0=Red/Off, 1=Yellow/ManualOn, 2=Green/AutoOn, 3=Gray/AutoOff)

    private int _section1ColorCode;
    public int Section1ColorCode
    {
        get => _section1ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section1ColorCode, value);
    }

    private int _section2ColorCode;
    public int Section2ColorCode
    {
        get => _section2ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section2ColorCode, value);
    }

    private int _section3ColorCode;
    public int Section3ColorCode
    {
        get => _section3ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section3ColorCode, value);
    }

    private int _section4ColorCode;
    public int Section4ColorCode
    {
        get => _section4ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section4ColorCode, value);
    }

    private int _section5ColorCode;
    public int Section5ColorCode
    {
        get => _section5ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section5ColorCode, value);
    }

    private int _section6ColorCode;
    public int Section6ColorCode
    {
        get => _section6ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section6ColorCode, value);
    }

    private int _section7ColorCode;
    public int Section7ColorCode
    {
        get => _section7ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section7ColorCode, value);
    }

    #endregion

    #region Headland Section Control

    private bool _isControlInHeadland;
    /// <summary>
    /// When true, section control remains active in headland area.
    /// </summary>
    public bool IsControlInHeadland
    {
        get => _isControlInHeadland;
        set => this.RaiseAndSetIfChanged(ref _isControlInHeadland, value);
    }

    private bool _isHeadlandControlled = true;
    /// <summary>
    /// Whether headland controls sections.
    /// </summary>
    public bool IsHeadlandControlled
    {
        get => _isHeadlandControlled;
        set => this.RaiseAndSetIfChanged(ref _isHeadlandControlled, value);
    }

    #endregion

    #region Commands

    public ICommand? ToggleMasterCommand { get; private set; }
    public ICommand? ToggleManualModeCommand { get; private set; }
    public ICommand? ToggleHeadlandControlCommand { get; private set; }
    public ICommand? SetAllSectionsCommand { get; private set; }
    public ICommand? ClearAllSectionsCommand { get; private set; }
    public ICommand? ToggleSectionCommand { get; private set; }

    private void InitializeCommands()
    {
        ToggleMasterCommand = new RelayCommand(() =>
        {
            IsMasterOn = !IsMasterOn;
            StatusMessageChanged?.Invoke(this, IsMasterOn
                ? "Section Master: ON"
                : "Section Master: OFF");
        });

        ToggleManualModeCommand = new RelayCommand(() =>
        {
            IsManualMode = !IsManualMode;
            StatusMessageChanged?.Invoke(this, IsManualMode
                ? "Manual Section Mode: ON"
                : "Auto Section Mode");
        });

        ToggleHeadlandControlCommand = new RelayCommand(() =>
        {
            IsControlInHeadland = !IsControlInHeadland;
            StatusMessageChanged?.Invoke(this, IsControlInHeadland
                ? "Section control in headland: ON"
                : "Section control in headland: OFF");
        });

        SetAllSectionsCommand = new RelayCommand(() =>
        {
            Section1Active = true;
            Section2Active = true;
            Section3Active = true;
            Section4Active = true;
            Section5Active = true;
            Section6Active = true;
            Section7Active = true;
            UpdateActiveCount();
            StatusMessageChanged?.Invoke(this, "All sections ON");
        });

        ClearAllSectionsCommand = new RelayCommand(() =>
        {
            Section1Active = false;
            Section2Active = false;
            Section3Active = false;
            Section4Active = false;
            Section5Active = false;
            Section6Active = false;
            Section7Active = false;
            UpdateActiveCount();
            StatusMessageChanged?.Invoke(this, "All sections OFF");
        });

        // Toggle section through 3-state cycle: Off -> Auto -> On -> Off
        ToggleSectionCommand = new RelayCommand<object>(param =>
        {
            if (param is string indexStr && int.TryParse(indexStr, out int sectionIndex))
            {
                CycleSectionState(sectionIndex);
            }
            else if (param is int index)
            {
                CycleSectionState(index);
            }
        });
    }

    /// <summary>
    /// Cycle section state through Off -> Auto -> On -> Off
    /// </summary>
    private void CycleSectionState(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _sectionControlService.NumSections)
            return;

        var currentState = _sectionControlService.SectionStates[sectionIndex].ButtonState;
        var newState = currentState switch
        {
            SectionButtonState.Off => SectionButtonState.Auto,
            SectionButtonState.Auto => SectionButtonState.On,
            SectionButtonState.On => SectionButtonState.Off,
            _ => SectionButtonState.Auto
        };

        _sectionControlService.SetSectionState(sectionIndex, newState);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Toggle a specific section by index (1-7).
    /// </summary>
    public void ToggleSection(int sectionIndex)
    {
        switch (sectionIndex)
        {
            case 1: Section1Active = !Section1Active; break;
            case 2: Section2Active = !Section2Active; break;
            case 3: Section3Active = !Section3Active; break;
            case 4: Section4Active = !Section4Active; break;
            case 5: Section5Active = !Section5Active; break;
            case 6: Section6Active = !Section6Active; break;
            case 7: Section7Active = !Section7Active; break;
        }
        UpdateActiveCount();
    }

    /// <summary>
    /// Set a specific section state by index (1-7).
    /// </summary>
    public void SetSection(int sectionIndex, bool active)
    {
        switch (sectionIndex)
        {
            case 1: Section1Active = active; break;
            case 2: Section2Active = active; break;
            case 3: Section3Active = active; break;
            case 4: Section4Active = active; break;
            case 5: Section5Active = active; break;
            case 6: Section6Active = active; break;
            case 7: Section7Active = active; break;
        }
        UpdateActiveCount();
    }

    /// <summary>
    /// Get section state by index (1-7).
    /// </summary>
    public bool GetSection(int sectionIndex)
    {
        return sectionIndex switch
        {
            1 => Section1Active,
            2 => Section2Active,
            3 => Section3Active,
            4 => Section4Active,
            5 => Section5Active,
            6 => Section6Active,
            7 => Section7Active,
            _ => false
        };
    }

    /// <summary>
    /// Update the active section count.
    /// </summary>
    public void UpdateActiveCount()
    {
        int count = 0;
        if (Section1Active) count++;
        if (Section2Active) count++;
        if (Section3Active) count++;
        if (Section4Active) count++;
        if (Section5Active) count++;
        if (Section6Active) count++;
        if (Section7Active) count++;
        ActiveCount = count;
    }

    #endregion

    #region Event Handlers

    private void OnSectionMasterToggleRequested(object? sender, SectionMasterToggleEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Toggle section master when requested by module communication service
            ToggleMasterCommand?.Execute(null);
        });
    }

    private void OnSectionStateChanged(object? sender, SectionStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update section active states from service
            var states = _sectionControlService.SectionStates;
            if (states.Count > 0) Section1Active = states[0].IsOn;
            if (states.Count > 1) Section2Active = states[1].IsOn;
            if (states.Count > 2) Section3Active = states[2].IsOn;
            if (states.Count > 3) Section4Active = states[3].IsOn;
            if (states.Count > 4) Section5Active = states[4].IsOn;
            if (states.Count > 5) Section6Active = states[5].IsOn;
            if (states.Count > 6) Section7Active = states[6].IsOn;

            // Update color codes
            Section1ColorCode = states.Count > 0 ? GetSectionColorCode(states[0]) : 0;
            Section2ColorCode = states.Count > 1 ? GetSectionColorCode(states[1]) : 0;
            Section3ColorCode = states.Count > 2 ? GetSectionColorCode(states[2]) : 0;
            Section4ColorCode = states.Count > 3 ? GetSectionColorCode(states[3]) : 0;
            Section5ColorCode = states.Count > 4 ? GetSectionColorCode(states[4]) : 0;
            Section6ColorCode = states.Count > 5 ? GetSectionColorCode(states[5]) : 0;
            Section7ColorCode = states.Count > 6 ? GetSectionColorCode(states[6]) : 0;

            UpdateActiveCount();
        });
    }

    /// <summary>
    /// Get color code for section state.
    /// 0=Red (Off), 1=Yellow (Manual On), 2=Green (Auto On), 3=Gray (Auto Off)
    /// </summary>
    private static int GetSectionColorCode(SectionControlState state)
    {
        return state.ButtonState switch
        {
            SectionButtonState.Off => 0,  // Red - manually off
            SectionButtonState.On => 1,   // Yellow - manually on
            SectionButtonState.Auto => state.IsOn ? 2 : 3,  // Green if on, Gray if off
            _ => 0
        };
    }

    /// <summary>
    /// Get section on/off states for map rendering.
    /// </summary>
    public bool[] GetSectionStates()
    {
        var states = _sectionControlService.SectionStates;
        var result = new bool[16];
        for (int i = 0; i < Math.Min(states.Count, 16); i++)
        {
            result[i] = states[i].IsOn;
        }
        return result;
    }

    /// <summary>
    /// Get section button states for map rendering (0=Off, 1=Auto, 2=On).
    /// </summary>
    public int[] GetSectionButtonStates()
    {
        var states = _sectionControlService.SectionStates;
        var result = new int[16];
        for (int i = 0; i < Math.Min(states.Count, 16); i++)
        {
            result[i] = (int)states[i].ButtonState;
        }
        return result;
    }

    /// <summary>
    /// Get section widths for map rendering.
    /// </summary>
    public double[] GetSectionWidths()
    {
        var states = _sectionControlService.SectionStates;
        var result = new double[16];
        for (int i = 0; i < Math.Min(states.Count, 16); i++)
        {
            result[i] = states[i].Width;
        }
        return result;
    }

    /// <summary>
    /// Number of configured sections.
    /// </summary>
    public int NumSections => _sectionControlService.NumSections;

    #endregion
}
