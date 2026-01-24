using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Sections
{
    /// <summary>
    /// Button state for manual section control
    /// </summary>
    public enum SectionButtonState
    {
        Off,
        Auto,
        On
    }

    /// <summary>
    /// Core model for individual section state and control
    /// Each section represents a portion of the implement that can be turned on/off independently
    /// </summary>
    public class SectionControl
    {
        // Section state flags
        public bool IsSectionOn { get; set; } = false;
        public bool IsSectionRequiredOn { get; set; } = false;
        public bool SectionOnRequest { get; set; } = false;
        public bool SectionOffRequest { get; set; } = false;
        public bool SectionOnOffCycle { get; set; } = false;

        // Timers (in frames or milliseconds)
        public int SectionOnTimer { get; set; } = 0;
        public int SectionOffTimer { get; set; } = 0;

        // Mapping state
        public bool IsMappingOn { get; set; } = false;
        public int MappingOnTimer { get; set; } = 0;
        public int MappingOffTimer { get; set; } = 0;

        // Speed calculation
        public double SpeedPixels { get; set; } = 0;

        // Section position (meters)
        // The left side is always negative, right side is positive
        // Example: center section would be -4 to 4 meters
        public double PositionLeft { get; set; } = -4;
        public double PositionRight { get; set; } = 4;
        public double SectionWidth { get; set; } = 0;

        // Read pixel parameters for color detection
        public int RpSectionWidth { get; set; } = 0;
        public int RpSectionPosition { get; set; } = 0;

        // World space points (start and end of section)
        public Vec2 LeftPoint { get; set; }
        public Vec2 RightPoint { get; set; }

        // Previous points (used to determine left and right speed of section)
        public Vec2 LastLeftPoint { get; set; }
        public Vec2 LastRightPoint { get; set; }

        // Boundary and headland detection
        public bool IsInBoundary { get; set; } = true;
        public bool IsInHeadlandArea { get; set; } = true;
        public bool IsLookOnInHeadland { get; set; } = true;

        // Manual section button state (Off, Auto, On)
        public SectionButtonState SectionButtonState { get; set; } = SectionButtonState.Off;
    }
}
