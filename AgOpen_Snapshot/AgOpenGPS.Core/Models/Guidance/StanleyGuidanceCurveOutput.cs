namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Output data from Stanley guidance algorithm for curved paths.
    /// Extends base output with curve-specific data.
    /// </summary>
    public class StanleyGuidanceCurveOutput : StanleyGuidanceOutput
    {
        // Curve-specific outputs
        public int CurrentLocationIndex { get; set; } // Current position index on curve
        public double ManualUturnHeading { get; set; } // Heading for manual U-turn
    }
}
