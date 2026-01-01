using System;
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for managing automatic section on/off based on coverage, boundaries, headlands,
/// and look-ahead calculations. Core functionality for sprayers, planters, and other implements.
/// </summary>
public interface ISectionControlService
{
    /// <summary>
    /// Section states array (one per section, up to 16)
    /// </summary>
    IReadOnlyList<SectionControlState> SectionStates { get; }

    /// <summary>
    /// Master control state
    /// </summary>
    SectionMasterState MasterState { get; set; }

    /// <summary>
    /// Whether any section is currently on
    /// </summary>
    bool IsAnySectionOn { get; }

    /// <summary>
    /// Number of configured sections
    /// </summary>
    int NumSections { get; }

    /// <summary>
    /// Update section states based on current tool position.
    /// Should be called each GPS update.
    /// </summary>
    /// <param name="toolPosition">Tool center position in world coordinates</param>
    /// <param name="toolHeading">Tool heading in radians</param>
    /// <param name="vehicleHeading">Vehicle heading in radians (for detecting tool catch-up)</param>
    /// <param name="speed">Vehicle speed in m/s</param>
    void Update(Vec3 toolPosition, double toolHeading, double vehicleHeading, double speed);

    /// <summary>
    /// Get section edge positions in world coordinates
    /// </summary>
    /// <param name="sectionIndex">Section index (0-based)</param>
    /// <param name="toolPosition">Tool center position</param>
    /// <param name="toolHeading">Tool heading in radians</param>
    /// <returns>Left and right edge positions</returns>
    (Vec2 left, Vec2 right) GetSectionWorldPosition(int sectionIndex, Vec3 toolPosition, double toolHeading);

    /// <summary>
    /// Set manual section state
    /// </summary>
    /// <param name="sectionIndex">Section index (0-based)</param>
    /// <param name="state">Button state (Off, Auto, On)</param>
    void SetSectionState(int sectionIndex, SectionButtonState state);

    /// <summary>
    /// Set all sections to the same state
    /// </summary>
    /// <param name="state">Button state to apply to all sections</param>
    void SetAllSections(SectionButtonState state);

    /// <summary>
    /// Turn all sections off (master off)
    /// </summary>
    void TurnAllOff();

    /// <summary>
    /// Set all sections to auto mode
    /// </summary>
    void SetAllAuto();

    /// <summary>
    /// Calculate section positions from configuration.
    /// Call when tool config changes.
    /// </summary>
    void RecalculateSectionPositions();

    /// <summary>
    /// Get section bits as a 16-bit mask (for hardware communication)
    /// </summary>
    /// <returns>Bitmask where bit N = section N state</returns>
    ushort GetSectionBits();

    /// <summary>
    /// Event fired when any section state changes
    /// </summary>
    event EventHandler<SectionStateChangedEventArgs>? SectionStateChanged;
}

/// <summary>
/// Master section control state
/// </summary>
public enum SectionMasterState
{
    /// <summary>All sections off</summary>
    Off,
    /// <summary>Sections controlled automatically by coverage/boundary</summary>
    Auto,
    /// <summary>Sections controlled manually</summary>
    Manual
}

/// <summary>
/// Individual section button state (3-way toggle)
/// </summary>
public enum SectionButtonState
{
    /// <summary>Section always off</summary>
    Off,
    /// <summary>Section controlled automatically</summary>
    Auto,
    /// <summary>Section always on (manual override)</summary>
    On
}

/// <summary>
/// Per-section control state
/// </summary>
public class SectionControlState
{
    /// <summary>Section index (0-based)</summary>
    public int Index { get; set; }

    /// <summary>Whether section is currently on (output state)</summary>
    public bool IsOn { get; set; }

    /// <summary>Whether section is required to be on by automatic logic</summary>
    public bool IsRequiredOn { get; set; }

    /// <summary>Section is requesting to turn on (pending timer)</summary>
    public bool SectionOnRequest { get; set; }

    /// <summary>Section is requesting to turn off (pending timer)</summary>
    public bool SectionOffRequest { get; set; }

    /// <summary>Timer counter for turning on</summary>
    public int SectionOnTimer { get; set; }

    /// <summary>Timer counter for turning off</summary>
    public int SectionOffTimer { get; set; }

    /// <summary>Whether mapping (coverage recording) is active for this section</summary>
    public bool IsMappingOn { get; set; }

    /// <summary>Timer for mapping on delay</summary>
    public int MappingOnTimer { get; set; }

    /// <summary>Timer for mapping off delay</summary>
    public int MappingOffTimer { get; set; }

    /// <summary>Manual button state (Off, Auto, On)</summary>
    public SectionButtonState ButtonState { get; set; } = SectionButtonState.Off;

    /// <summary>Section left edge position relative to tool center (negative = left)</summary>
    public double PositionLeft { get; set; }

    /// <summary>Section right edge position relative to tool center (positive = right)</summary>
    public double PositionRight { get; set; }

    /// <summary>Section width in meters</summary>
    public double Width => PositionRight - PositionLeft;

    /// <summary>Section center position relative to tool center</summary>
    public double PositionCenter => (PositionLeft + PositionRight) / 2.0;

    /// <summary>Whether section center is inside field boundary</summary>
    public bool IsInBoundary { get; set; }

    /// <summary>Whether section center is in headland area</summary>
    public bool IsInHeadland { get; set; }

    /// <summary>Whether look-ahead point is in headland</summary>
    public bool IsLookOnInHeadland { get; set; }

    /// <summary>Current coverage percentage at section position (0.0 to 1.0)</summary>
    public double CoveragePercent { get; set; }

    /// <summary>Reset timers and request flags</summary>
    public void Reset()
    {
        IsOn = false;
        IsRequiredOn = false;
        SectionOnRequest = false;
        SectionOffRequest = false;
        SectionOnTimer = 0;
        SectionOffTimer = 0;
        IsMappingOn = false;
        MappingOnTimer = 0;
        MappingOffTimer = 0;
        IsInBoundary = true;
        IsInHeadland = false;
        IsLookOnInHeadland = false;
        CoveragePercent = 0;
    }
}

/// <summary>
/// Event arguments for section state changes
/// </summary>
public class SectionStateChangedEventArgs : EventArgs
{
    /// <summary>Index of the section that changed (-1 for all sections)</summary>
    public int SectionIndex { get; init; }

    /// <summary>New state of the section</summary>
    public bool IsOn { get; init; }

    /// <summary>Whether mapping is active</summary>
    public bool IsMappingOn { get; init; }

    /// <summary>All section bits as a bitmask</summary>
    public ushort SectionBits { get; init; }
}
