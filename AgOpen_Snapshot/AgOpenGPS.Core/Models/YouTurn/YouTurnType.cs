namespace AgOpenGPS.Core.Models.YouTurn
{
    /// <summary>
    /// Type of U-turn pattern to create.
    /// </summary>
    public enum YouTurnType
    {
        /// <summary>
        /// Omega or Wide turn (based on offset width).
        /// Uses Dubins paths for omega, semicircles for wide.
        /// </summary>
        AlbinStyle = 0,

        /// <summary>
        /// K-style turn.
        /// Creates a more squared-off turn pattern.
        /// </summary>
        KStyle = 1
    }

    /// <summary>
    /// Skip mode for determining next guidance line.
    /// </summary>
    public enum SkipMode
    {
        /// <summary>
        /// Normal skip - use configured skip width.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Alternate between different skip widths.
        /// </summary>
        Alternative = 1,

        /// <summary>
        /// Skip worked tracks - find next unworked track.
        /// </summary>
        IgnoreWorkedTracks = 2
    }

    /// <summary>
    /// Guidance line type for U-turn.
    /// </summary>
    public enum GuidanceLineType
    {
        /// <summary>
        /// AB straight line.
        /// </summary>
        ABLine = 0,

        /// <summary>
        /// Curved guidance line.
        /// </summary>
        Curve = 1
    }
}
