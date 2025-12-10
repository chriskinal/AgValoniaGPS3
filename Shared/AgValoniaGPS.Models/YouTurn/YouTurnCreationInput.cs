using AgValoniaGPS.Models.Base;
using System;
using System.Collections.Generic;

namespace AgValoniaGPS.Models.YouTurn
{
    /// <summary>
    /// Input data for U-turn path creation.
    /// </summary>
    public class YouTurnCreationInput
    {
        // Turn style and direction
        public YouTurnType TurnType { get; set; }
        public bool IsTurnLeft { get; set; }
        public GuidanceLineType GuidanceType { get; set; }

        // Boundary data
        public List<BoundaryTurnLine> BoundaryTurnLines { get; set; } = new List<BoundaryTurnLine>();

        /// <summary>
        /// Function to check if a point is inside the turn area.
        /// Returns: 0 if inside field, non-zero boundary index if inside turn area.
        /// </summary>
        public Func<Vec3, int> IsPointInsideTurnArea { get; set; }

        // Guidance line data
        /// <summary>
        /// For curves: List of curve points.
        /// For AB lines: Can be empty (AB line defined by heading and reference point).
        /// </summary>
        public List<Vec3> GuidancePoints { get; set; } = new List<Vec3>();

        /// <summary>
        /// Current location index in the guidance points (for curves).
        /// </summary>
        public int CurrentLocationIndex { get; set; }

        /// <summary>
        /// For curves: Is vehicle heading same way as curve direction?
        /// For AB: Is vehicle heading same way as AB line direction?
        /// </summary>
        public bool IsHeadingSameWay { get; set; }

        /// <summary>
        /// For AB lines: AB line heading in radians.
        /// </summary>
        public double ABHeading { get; set; }

        /// <summary>
        /// For AB lines: Reference point on AB line (rEast, rNorth).
        /// </summary>
        public Vec2 ABReferencePoint { get; set; }

        // Vehicle position and configuration
        public Vec3 PivotPosition { get; set; }
        public double ToolWidth { get; set; }
        public double ToolOverlap { get; set; }
        public double ToolOffset { get; set; }
        public double TurnRadius { get; set; }

        // Turn parameters
        public int RowSkipsWidth { get; set; }
        public int TurnStartOffset { get; set; }  // How far before/after boundary to start turn

        /// <summary>
        /// How many paths away from current line (negative or positive).
        /// </summary>
        public int HowManyPathsAway { get; set; }

        /// <summary>
        /// Nudge distance for track mode.
        /// </summary>
        public double NudgeDistance { get; set; }

        /// <summary>
        /// Track mode (for determining if this is a special mode like waterPivot).
        /// </summary>
        public int TrackMode { get; set; }

        // State machine
        /// <summary>
        /// Counter to prevent making turns too frequently (must be >= 4).
        /// </summary>
        public int MakeUTurnCounter { get; set; }

        /// <summary>
        /// List of worked tracks (for IgnoreWorkedTracks mode).
        /// </summary>
        public HashSet<int> WorkedTracks { get; set; } = new HashSet<int>();

        /// <summary>
        /// U-turn leg extension multiplier. The straight legs before/after the turn arc
        /// are extended by this factor times the turn diameter.
        /// Typical values are 2-3 (default 2.5).
        /// </summary>
        public double YouTurnLegExtensionMultiplier { get; set; } = 2.5;

        /// <summary>
        /// The headland width in meters. Used to ensure U-turn legs extend
        /// far enough to guide the tractor through the entire headland area.
        /// </summary>
        public double HeadlandWidth { get; set; } = 20.0;
    }
}
