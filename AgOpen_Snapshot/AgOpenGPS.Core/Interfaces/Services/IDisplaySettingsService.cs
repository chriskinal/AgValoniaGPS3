using System;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for managing display and navigation settings
    /// </summary>
    public interface IDisplaySettingsService
    {
        // Grid display
        bool IsGridOn { get; set; }
        event EventHandler<bool>? GridVisibilityChanged;

        // Day/Night mode
        bool IsDayMode { get; set; }
        event EventHandler<bool>? DayNightModeChanged;

        // Camera settings
        double CameraPitch { get; set; }
        bool Is2DMode { get; set; }
        bool IsNorthUp { get; set; }
        event EventHandler<double>? CameraPitchChanged;
        event EventHandler<bool>? ViewModeChanged;

        // Brightness control
        int Brightness { get; set; }
        bool IsBrightnessSupported { get; }
        event EventHandler<int>? BrightnessChanged;

        // Methods
        void IncreaseCameraPitch();
        void DecreaseCameraPitch();
        void IncreaseBrightness();
        void DecreaseBrightness();
        void ToggleGrid();
        void ToggleDayNight();
        void Toggle2D3D();
        void ToggleNorthUp();

        // Settings persistence
        void LoadSettings();
        void SaveSettings();
    }
}
