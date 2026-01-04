namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing View Settings and Panel Visibility.
/// Manages UI state for panels, display settings, and camera/brightness controls.
/// </summary>
public partial class MainViewModel
{
    #region Panel Visibility Fields

    private bool _isViewSettingsPanelVisible;
    private bool _isFileMenuPanelVisible;
    private bool _isToolsPanelVisible;
    private bool _isConfigurationPanelVisible;
    private bool _isJobMenuPanelVisible;
    private bool _isFieldToolsPanelVisible;
    private bool _isSimulatorPanelVisible;

    #endregion

    #region Panel Visibility Properties

    public bool IsViewSettingsPanelVisible
    {
        get => _isViewSettingsPanelVisible;
        set => SetProperty(ref _isViewSettingsPanelVisible, value);
    }

    public bool IsFileMenuPanelVisible
    {
        get => _isFileMenuPanelVisible;
        set => SetProperty(ref _isFileMenuPanelVisible, value);
    }

    public bool IsToolsPanelVisible
    {
        get => _isToolsPanelVisible;
        set => SetProperty(ref _isToolsPanelVisible, value);
    }

    public bool IsConfigurationPanelVisible
    {
        get => _isConfigurationPanelVisible;
        set => SetProperty(ref _isConfigurationPanelVisible, value);
    }

    public bool IsJobMenuPanelVisible
    {
        get => _isJobMenuPanelVisible;
        set => SetProperty(ref _isJobMenuPanelVisible, value);
    }

    public bool IsFieldToolsPanelVisible
    {
        get => _isFieldToolsPanelVisible;
        set => SetProperty(ref _isFieldToolsPanelVisible, value);
    }

    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => SetProperty(ref _isSimulatorPanelVisible, value);
    }

    #endregion

    #region Display Settings Properties

    // Navigation settings properties (forwarded from service)
    public bool IsGridOn
    {
        get => _displaySettings.IsGridOn;
        set
        {
            _displaySettings.IsGridOn = value;
            OnPropertyChanged();
        }
    }

    public bool IsDayMode
    {
        get => _displaySettings.IsDayMode;
        set
        {
            _displaySettings.IsDayMode = value;
            OnPropertyChanged();
        }
    }

    public double CameraPitch
    {
        get => _displaySettings.CameraPitch;
        set
        {
            _displaySettings.CameraPitch = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Is2DMode));
        }
    }

    public bool Is2DMode
    {
        get => _displaySettings.Is2DMode;
        set
        {
            _displaySettings.Is2DMode = value;
            OnPropertyChanged();
        }
    }

    public bool IsNorthUp
    {
        get => _displaySettings.IsNorthUp;
        set
        {
            _displaySettings.IsNorthUp = value;
            OnPropertyChanged();
        }
    }

    public int Brightness
    {
        get => _displaySettings.Brightness;
        set
        {
            _displaySettings.Brightness = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BrightnessDisplay));
        }
    }

    public string BrightnessDisplay => _displaySettings.IsBrightnessSupported
        ? $"{_displaySettings.Brightness}%"
        : "??";

    #endregion
}
