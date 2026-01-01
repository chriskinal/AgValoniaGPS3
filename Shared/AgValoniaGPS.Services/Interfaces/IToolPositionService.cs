using System;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for calculating tool/implement position relative to vehicle pivot point.
/// Handles fixed (front/rear), trailing, and TBT (Tow-Between-Tractor) configurations.
/// </summary>
public interface IToolPositionService
{
    /// <summary>
    /// Current tool center position in world coordinates
    /// </summary>
    Vec3 ToolPosition { get; }

    /// <summary>
    /// Tool pivot/hitch position in world coordinates
    /// </summary>
    Vec3 ToolPivotPosition { get; }

    /// <summary>
    /// Current tool heading in radians
    /// </summary>
    double ToolHeading { get; }

    /// <summary>
    /// Tank trailer position for TBT mode (zero if not TBT)
    /// </summary>
    Vec3 TankPosition { get; }

    /// <summary>
    /// Hitch point position on vehicle
    /// </summary>
    Vec3 HitchPosition { get; }

    /// <summary>
    /// Update tool position based on vehicle position.
    /// Should be called every GPS update for smooth trailing behavior.
    /// </summary>
    /// <param name="vehiclePivot">Vehicle pivot point position</param>
    /// <param name="vehicleHeading">Vehicle heading in radians</param>
    void Update(Vec3 vehiclePivot, double vehicleHeading);

    /// <summary>
    /// Get tool edge positions in world coordinates
    /// </summary>
    /// <returns>Left and right edge positions</returns>
    (Vec3 left, Vec3 right) GetToolEdgePositions();

    /// <summary>
    /// Get a specific section's center position in world coordinates
    /// </summary>
    /// <param name="sectionIndex">Section index (0-based)</param>
    /// <param name="sectionLeft">Section left edge offset from tool center (negative)</param>
    /// <param name="sectionRight">Section right edge offset from tool center (positive)</param>
    /// <returns>Section center position</returns>
    Vec3 GetSectionPosition(int sectionIndex, double sectionLeft, double sectionRight);

    /// <summary>
    /// Get section edge positions in world coordinates
    /// </summary>
    /// <param name="sectionLeft">Section left edge offset from tool center</param>
    /// <param name="sectionRight">Section right edge offset from tool center</param>
    /// <returns>Left and right edge positions</returns>
    (Vec3 left, Vec3 right) GetSectionEdgePositions(double sectionLeft, double sectionRight);

    /// <summary>
    /// Reset trailing state (e.g., when starting new field or after pause).
    /// Snaps tool directly behind vehicle to prevent jackknife on startup.
    /// </summary>
    /// <param name="vehiclePivot">Vehicle pivot point position</param>
    /// <param name="vehicleHeading">Vehicle heading in radians</param>
    void ResetTrailingState(Vec3 vehiclePivot, double vehicleHeading);

    /// <summary>
    /// Event fired when tool position is updated
    /// </summary>
    event EventHandler<ToolPositionUpdatedEventArgs>? PositionUpdated;
}

/// <summary>
/// Event arguments for tool position updates
/// </summary>
public class ToolPositionUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Current tool center position
    /// </summary>
    public Vec3 ToolPosition { get; init; }

    /// <summary>
    /// Current tool heading in radians
    /// </summary>
    public double ToolHeading { get; init; }

    /// <summary>
    /// Current vehicle heading in radians (for detecting tool catch-up on trailed implements)
    /// </summary>
    public double VehicleHeading { get; init; }

    /// <summary>
    /// Tank trailer position (for TBT mode)
    /// </summary>
    public Vec3 TankPosition { get; init; }

    /// <summary>
    /// Whether tool is in TBT mode
    /// </summary>
    public bool IsTBT { get; init; }
}
