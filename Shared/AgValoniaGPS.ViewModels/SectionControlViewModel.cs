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
/// </summary>
public class SectionControlViewModel : ReactiveObject
{
    private readonly IModuleCommunicationService _moduleCommunicationService;
    private readonly ApplicationState _appState;

    // Events for MainViewModel to subscribe to
    public event EventHandler<string>? StatusMessageChanged;

    public SectionControlViewModel(
        IModuleCommunicationService moduleCommunicationService,
        ApplicationState appState)
    {
        _moduleCommunicationService = moduleCommunicationService;
        _appState = appState;

        // Subscribe to module communication events
        _moduleCommunicationService.SectionMasterToggleRequested += OnSectionMasterToggleRequested;

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

    #endregion
}
