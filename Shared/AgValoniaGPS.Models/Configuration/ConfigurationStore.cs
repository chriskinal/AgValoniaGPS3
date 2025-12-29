using System;
using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Central configuration store - the ONLY place configuration lives.
/// All components access this directly; no private copies.
/// Implements INotifyPropertyChanged via ReactiveObject for UI binding.
/// </summary>
public class ConfigurationStore : ReactiveObject
{
    private static ConfigurationStore? _instance;

    /// <summary>
    /// Singleton instance. Use DI where possible, but this provides
    /// easy access for services that need configuration.
    /// </summary>
    public static ConfigurationStore Instance => _instance ??= new ConfigurationStore();

    /// <summary>
    /// For testing - allows replacing the singleton instance
    /// </summary>
    public static void SetInstance(ConfigurationStore store) => _instance = store;

    // Sub-configurations (each is a ReactiveObject for binding)
    public VehicleConfig Vehicle { get; } = new();
    public ToolConfig Tool { get; } = new();
    public GuidanceConfig Guidance { get; } = new();
    public DisplayConfig Display { get; } = new();
    public SimulatorConfig Simulator { get; } = new();
    public ConnectionConfig Connections { get; } = new();
    public AhrsConfig Ahrs { get; } = new();
    public MachineConfig Machine { get; } = new();
    public TramConfig Tram { get; } = new();

    // Profile management
    private string _activeProfileName = "Default";
    public string ActiveProfileName
    {
        get => _activeProfileName;
        set => this.RaiseAndSetIfChanged(ref _activeProfileName, value);
    }

    private string _activeProfilePath = string.Empty;
    public string ActiveProfilePath
    {
        get => _activeProfilePath;
        set => this.RaiseAndSetIfChanged(ref _activeProfilePath, value);
    }

    // Dirty tracking for save prompts
    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    // Unit settings
    private bool _isMetric = true;
    public bool IsMetric
    {
        get => _isMetric;
        set => this.RaiseAndSetIfChanged(ref _isMetric, value);
    }

    // Section configuration
    private int _numSections = 1;
    public int NumSections
    {
        get => _numSections;
        set => this.RaiseAndSetIfChanged(ref _numSections, Math.Clamp(value, 1, 16));
    }

    /// <summary>
    /// Calculates actual tool width from active sections (in meters).
    /// Use this for guidance calculations instead of Tool.Width.
    /// </summary>
    public double ActualToolWidth
    {
        get
        {
            double total = 0;
            for (int i = 0; i < _numSections && i < 16; i++)
            {
                total += Tool.GetSectionWidth(i) / 100.0; // cm to meters
            }
            return total > 0 ? total : Tool.Width; // Fallback to stored width if no sections
        }
    }

    private double[] _sectionPositions = new double[17];
    public double[] SectionPositions
    {
        get => _sectionPositions;
        set => this.RaiseAndSetIfChanged(ref _sectionPositions, value);
    }

    // Events for significant changes
    public event EventHandler? ProfileLoaded;
    public event EventHandler? ProfileSaved;

    /// <summary>
    /// Raises the ProfileLoaded event
    /// </summary>
    public void OnProfileLoaded() => ProfileLoaded?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Raises the ProfileSaved event
    /// </summary>
    public void OnProfileSaved() => ProfileSaved?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Marks the configuration as having unsaved changes
    /// </summary>
    public void MarkChanged() => HasUnsavedChanges = true;
}
