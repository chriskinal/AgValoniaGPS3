using System;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for managing display and navigation settings.
/// Delegates to ConfigurationStore.Instance.Display for state.
/// </summary>
public class DisplaySettingsService : IDisplaySettingsService
{
    private const double CameraPitchStep = 5.0;
    private const int BrightnessStep = 5;

    // Access display config directly from the store
    private static DisplayConfig Display => ConfigurationStore.Instance.Display;

    // Grid display - delegates to DisplayConfig
    public bool IsGridOn
    {
        get => Display.GridVisible;
        set
        {
            if (Display.GridVisible != value)
            {
                Display.GridVisible = value;
                GridVisibilityChanged?.Invoke(this, value);
            }
        }
    }
    public event EventHandler<bool>? GridVisibilityChanged;

    // Day/Night mode - delegates to DisplayConfig
    public bool IsDayMode
    {
        get => Display.IsDayMode;
        set
        {
            if (Display.IsDayMode != value)
            {
                Display.IsDayMode = value;
                DayNightModeChanged?.Invoke(this, value);
            }
        }
    }
    public event EventHandler<bool>? DayNightModeChanged;

    // Camera settings - delegates to DisplayConfig
    public double CameraPitch
    {
        get => Display.CameraPitch;
        set
        {
            // Clamp pitch between -90 and -10 degrees
            var clampedValue = Math.Max(-90, Math.Min(-10, value));
            if (Math.Abs(Display.CameraPitch - clampedValue) > 0.01)
            {
                Display.CameraPitch = clampedValue;
                CameraPitchChanged?.Invoke(this, clampedValue);
            }
        }
    }
    public event EventHandler<double>? CameraPitchChanged;

    public bool Is2DMode
    {
        get => Display.Is2DMode;
        set
        {
            if (Display.Is2DMode != value)
            {
                Display.Is2DMode = value;
                // When switching to 2D, set pitch to -90 (straight down)
                // When switching to 3D, restore previous pitch or default
                if (value)
                {
                    CameraPitch = -90.0;
                }
                else
                {
                    CameraPitch = -62.0; // Default 3D pitch
                }
                ViewModeChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsNorthUp
    {
        get => Display.IsNorthUp;
        set
        {
            if (Display.IsNorthUp != value)
            {
                Display.IsNorthUp = value;
                ViewModeChanged?.Invoke(this, value);
            }
        }
    }
    public event EventHandler<bool>? ViewModeChanged;

    // Brightness control - local state (platform-specific, not persisted)
    private int _brightness = 50;
    public int Brightness
    {
        get => _brightness;
        set
        {
            // Clamp between 0 and 100
            var clampedValue = Math.Max(0, Math.Min(100, value));
            if (_brightness != clampedValue)
            {
                _brightness = clampedValue;
                BrightnessChanged?.Invoke(this, clampedValue);
            }
        }
    }
    public event EventHandler<int>? BrightnessChanged;

    // Brightness support depends on platform
    // For now, we'll stub this - can implement platform-specific later
    public bool IsBrightnessSupported => false;

    public void IncreaseCameraPitch()
    {
        CameraPitch += CameraPitchStep;
    }

    public void DecreaseCameraPitch()
    {
        CameraPitch -= CameraPitchStep;
    }

    public void IncreaseBrightness()
    {
        Brightness += BrightnessStep;
    }

    public void DecreaseBrightness()
    {
        Brightness -= BrightnessStep;
    }

    public void ToggleGrid()
    {
        IsGridOn = !IsGridOn;
    }

    public void ToggleDayNight()
    {
        IsDayMode = !IsDayMode;
    }

    public void Toggle2D3D()
    {
        Is2DMode = !Is2DMode;
    }

    public void ToggleNorthUp()
    {
        IsNorthUp = !IsNorthUp;
    }

    public void LoadSettings()
    {
        // Settings are now loaded via ConfigurationService.LoadAppSettings()
        // This method exists for interface compatibility but doesn't need to do anything
        // since DisplayConfig is populated when app settings are loaded.

        // Fire events to notify UI of current values
        GridVisibilityChanged?.Invoke(this, Display.GridVisible);
        DayNightModeChanged?.Invoke(this, Display.IsDayMode);
        CameraPitchChanged?.Invoke(this, Display.CameraPitch);
        ViewModeChanged?.Invoke(this, Display.Is2DMode);
    }

    public void SaveSettings()
    {
        // Settings are now saved via ConfigurationService.SaveAppSettings()
        // This method exists for interface compatibility
    }
}
