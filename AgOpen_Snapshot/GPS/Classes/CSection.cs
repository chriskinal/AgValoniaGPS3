using AgOpenGPS.Core.Models.Sections;

//Please, if you use this, share the improvements

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for SectionControl from AgOpenGPS.Core
    /// Delegates all operations to Core SectionControl instance
    ///
    /// Each section is composed of a patchlist and triangle list.
    /// The triangle list makes up the individual triangles that make the block or patch of applied (green spot).
    /// The patch list is a list of the list of triangles.
    /// </summary>
    public class CSection
    {
        private readonly SectionControl _core;

        /// <summary>
        /// Is this section on or off - delegates to Core
        /// </summary>
        public bool isSectionOn
        {
            get => _core.IsSectionOn;
            set => _core.IsSectionOn = value;
        }

        public bool isSectionRequiredOn
        {
            get => _core.IsSectionRequiredOn;
            set => _core.IsSectionRequiredOn = value;
        }

        public bool sectionOnRequest
        {
            get => _core.SectionOnRequest;
            set => _core.SectionOnRequest = value;
        }

        public bool sectionOffRequest
        {
            get => _core.SectionOffRequest;
            set => _core.SectionOffRequest = value;
        }

        public bool sectionOnOffCycle
        {
            get => _core.SectionOnOffCycle;
            set => _core.SectionOnOffCycle = value;
        }

        /// <summary>
        /// Timers - delegate to Core
        /// </summary>
        public int sectionOnTimer
        {
            get => _core.SectionOnTimer;
            set => _core.SectionOnTimer = value;
        }

        public int sectionOffTimer
        {
            get => _core.SectionOffTimer;
            set => _core.SectionOffTimer = value;
        }

        /// <summary>
        /// Mapping state - delegates to Core
        /// </summary>
        public bool isMappingOn
        {
            get => _core.IsMappingOn;
            set => _core.IsMappingOn = value;
        }

        public int mappingOnTimer
        {
            get => _core.MappingOnTimer;
            set => _core.MappingOnTimer = value;
        }

        public int mappingOffTimer
        {
            get => _core.MappingOffTimer;
            set => _core.MappingOffTimer = value;
        }

        public double speedPixels
        {
            get => _core.SpeedPixels;
            set => _core.SpeedPixels = value;
        }

        /// <summary>
        /// Section position in meters - delegates to Core
        /// The left side is always negative, right side is positive.
        /// Example: section on left would be -8 to -4, center -4 to 4, right 4 to 8
        /// </summary>
        public double positionLeft
        {
            get => _core.PositionLeft;
            set => _core.PositionLeft = value;
        }

        public double positionRight
        {
            get => _core.PositionRight;
            set => _core.PositionRight = value;
        }

        public double sectionWidth
        {
            get => _core.SectionWidth;
            set => _core.SectionWidth = value;
        }

        /// <summary>
        /// Read pixel parameters - delegates to Core
        /// </summary>
        public int rpSectionWidth
        {
            get => _core.RpSectionWidth;
            set => _core.RpSectionWidth = value;
        }

        public int rpSectionPosition
        {
            get => _core.RpSectionPosition;
            set => _core.RpSectionPosition = value;
        }

        /// <summary>
        /// Points in world space (start and end of section) - delegates to Core with vec2/Vec2 conversion
        /// </summary>
        public vec2 leftPoint
        {
            get => _core.LeftPoint;  // Implicit conversion from Vec2 to vec2
            set => _core.LeftPoint = value;  // Implicit conversion from vec2 to Vec2
        }

        public vec2 rightPoint
        {
            get => _core.RightPoint;
            set => _core.RightPoint = value;
        }

        /// <summary>
        /// Previous points (used to determine left and right speed of section) - delegates to Core
        /// </summary>
        public vec2 lastLeftPoint
        {
            get => _core.LastLeftPoint;
            set => _core.LastLeftPoint = value;
        }

        public vec2 lastRightPoint
        {
            get => _core.LastRightPoint;
            set => _core.LastRightPoint = value;
        }

        /// <summary>
        /// Boundary and headland detection - delegates to Core
        /// </summary>
        public bool isInBoundary
        {
            get => _core.IsInBoundary;
            set => _core.IsInBoundary = value;
        }

        public bool isInHeadlandArea
        {
            get => _core.IsInHeadlandArea;
            set => _core.IsInHeadlandArea = value;
        }

        public bool isLookOnInHeadland
        {
            get => _core.IsLookOnInHeadland;
            set => _core.IsLookOnInHeadland = value;
        }

        /// <summary>
        /// Manual section button state (Off, Auto, On) - delegates to Core
        /// </summary>
        public btnStates sectionBtnState
        {
            get => (btnStates)(int)_core.SectionButtonState;  // Cast between enums
            set => _core.SectionButtonState = (SectionButtonState)(int)value;
        }

        /// <summary>
        /// Constructor - initializes Core instance
        /// </summary>
        public CSection()
        {
            _core = new SectionControl();
        }

        /// <summary>
        /// Get the underlying Core SectionControl instance
        /// </summary>
        public SectionControl CoreSectionControl => _core;
    }
}
