using AgOpenGPS.Core.Models.Base;
using System.Collections.Generic;

namespace AgOpenGPS.Core.Models.YouTurn
{
    /// <summary>
    /// Output data from U-turn path creation.
    /// </summary>
    public class YouTurnCreationOutput
    {
        /// <summary>
        /// True if turn was successfully created.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Reason for failure (if Success = false).
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Generated U-turn path points.
        /// </summary>
        public List<Vec3> TurnPath { get; set; } = new List<Vec3>();

        /// <summary>
        /// True if turn goes to same curve/line (doubling back).
        /// False if turn goes to next offset line.
        /// </summary>
        public bool IsOutSameCurve { get; set; }

        /// <summary>
        /// True if vehicle continues same direction (straight through headland).
        /// </summary>
        public bool IsGoingStraightThrough { get; set; }

        /// <summary>
        /// True if turn is out of bounds (intersects boundaries).
        /// </summary>
        public bool IsOutOfBounds { get; set; }

        /// <summary>
        /// Distance from pivot to turn line at start of turn.
        /// </summary>
        public double DistancePivotToTurnLine { get; set; }

        /// <summary>
        /// Closest point on turn line where turn starts.
        /// </summary>
        public Vec3 InClosestTurnPoint { get; set; }

        /// <summary>
        /// Closest point on turn line where turn ends.
        /// </summary>
        public Vec3 OutClosestTurnPoint { get; set; }
    }
}
