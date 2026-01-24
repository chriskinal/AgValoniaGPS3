using System;
using AgOpenGPS.Core.Interfaces.Services;

namespace AgOpenGPS.Core.Services
{
    /// <summary>
    /// Service for managing display and navigation settings
    /// </summary>
    public class DisplaySettingsService : IDisplaySettingsService
    {
        private const double CameraPitchStep = 5.0;
        private const int BrightnessStep = 5;

        // Grid display
        private bool _isGridOn = false;
        public bool IsGridOn
        {
            get => _isGridOn;
            set
            {
                if (_isGridOn != value)
                {
                    _isGridOn = value;
                    GridVisibilityChanged?.Invoke(this, value);
                }
            }
        }
        public event EventHandler<bool>? GridVisibilityChanged;

        // Day/Night mode
        private bool _isDayMode = true;
        public bool IsDayMode
        {
            get => _isDayMode;
            set
            {
                if (_isDayMode != value)
                {
                    _isDayMode = value;
                    DayNightModeChanged?.Invoke(this, value);
                }
            }
        }
        public event EventHandler<bool>? DayNightModeChanged;

        // Camera settings
        private double _cameraPitch = -62.0;
        public double CameraPitch
        {
            get => _cameraPitch;
            set
            {
                // Clamp pitch between -90 and -10 degrees
                var clampedValue = Math.Max(-90, Math.Min(-10, value));
                if (Math.Abs(_cameraPitch - clampedValue) > 0.01)
                {
                    _cameraPitch = clampedValue;
                    CameraPitchChanged?.Invoke(this, clampedValue);
                }
            }
        }
        public event EventHandler<double>? CameraPitchChanged;

        private bool _is2DMode = false;
        public bool Is2DMode
        {
            get => _is2DMode;
            set
            {
                if (_is2DMode != value)
                {
                    _is2DMode = value;
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

        private bool _isNorthUp = true;
        public bool IsNorthUp
        {
            get => _isNorthUp;
            set
            {
                if (_isNorthUp != value)
                {
                    _isNorthUp = value;
                    ViewModeChanged?.Invoke(this, value);
                }
            }
        }
        public event EventHandler<bool>? ViewModeChanged;

        // Brightness control
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
            // TODO: Implement settings persistence
            // For now, use defaults - set fields directly without raising events
            _isGridOn = false;
            _isDayMode = true;
            _cameraPitch = -62.0;
            _is2DMode = false;
            _isNorthUp = true;
            _brightness = 50;

            // No events raised - these are just initial values
        }

        public void SaveSettings()
        {
            // TODO: Implement settings persistence
            // Will need to add settings file or database
        }
    }
}
