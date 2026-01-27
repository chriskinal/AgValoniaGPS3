// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Models.Headland;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Section;

/// <summary>
/// Manages automatic section on/off based on coverage, boundaries, headlands,
/// and look-ahead calculations.
///
/// Based on AgOpenGPS section control logic from Sections.Designer.cs and CSection.cs.
/// </summary>
public class SectionControlService : ISectionControlService
{
    private readonly IToolPositionService _toolPositionService;
    private readonly ICoverageMapService _coverageMapService;
    private readonly ApplicationState _state;

    private readonly SectionControlState[] _sectionStates;
    private SectionMasterState _masterState = SectionMasterState.Off;

    // Timing thresholds (in update cycles, typically 10Hz = 100ms per cycle)
    private const int SECTION_ON_DELAY = 2;   // ~200ms delay before turning on
    private const int MAPPING_ON_DELAY = 2;   // ~200ms delay before recording coverage
    private const int MAPPING_OFF_DELAY = 2;  // ~200ms delay before stopping coverage

    // Default coverage overlap threshold (used if MinCoverage is 0)
    private const double DEFAULT_COVERAGE_THRESHOLD = 0.70; // 70%

    // Minimum distance (squared) between coverage points to reduce edge jaggedness
    // At 10Hz and 10 kph (2.78 m/s), vehicle moves ~0.28m per update
    // Using 0.12m threshold ensures we add points frequently enough for accuracy
    // but filter out GPS jitter that causes jagged edges
    private const double MIN_COVERAGE_POINT_DISTANCE_SQ = 0.12 * 0.12; // 0.0144 m²

    // Last coverage point position per zone (for minimum distance filtering)
    private readonly Dictionary<int, Vec2> _lastCoveragePosition = new();

    // Yaw rate tracking for curve-following coverage margin
    private double _previousHeading = double.NaN;
    private double _previousVehicleHeading = double.NaN;
    private double _yawRate = 0; // smoothed yaw rate in radians per update cycle (positive = turning right)
    private double _instantYawRate = 0; // instantaneous (unsmoothed) yaw rate for threshold checks
    private double _vehicleYawRate = 0; // vehicle (tractor) yaw rate
    private double _toolVehicleHeadingDiff = 0; // difference between tool and vehicle heading (for trailed implements)
    private const double YAW_RATE_SMOOTHING = 0.3; // Smoothing factor (0-1, lower = smoother)

    // Performance timing (exposed for consolidated logging)
    private static readonly Stopwatch _sectionSw = new();
    private static double _totalBoundaryMs;
    private static double _totalHeadlandMs;
    private static double _totalCoverageMs;
    private static int _sectionUpdateCounter;

    // Public accessors for timing (read from MainViewModel)
    public static double LastBoundaryMs => _totalBoundaryMs;
    public static double LastHeadlandMs => _totalHeadlandMs;
    public static double LastCoverageCheckMs => _totalCoverageMs;

    public IReadOnlyList<SectionControlState> SectionStates => _sectionStates;
    public SectionMasterState MasterState
    {
        get => _masterState;
        set
        {
            if (_masterState != value)
            {
                _masterState = value;
                if (value == SectionMasterState.Off)
                {
                    TurnAllOff();
                }
            }
        }
    }

    public bool IsAnySectionOn
    {
        get
        {
            for (int i = 0; i < NumSections; i++)
            {
                if (_sectionStates[i].IsOn) return true;
            }
            return false;
        }
    }

    public int NumSections => ConfigurationStore.Instance.NumSections;

    public event EventHandler<SectionStateChangedEventArgs>? SectionStateChanged;

    public SectionControlService(
        IToolPositionService toolPositionService,
        ICoverageMapService coverageMapService,
        ApplicationState state)
    {
        _toolPositionService = toolPositionService;
        _coverageMapService = coverageMapService;
        _state = state;

        // Initialize section states
        _sectionStates = new SectionControlState[16];
        for (int i = 0; i < 16; i++)
        {
            _sectionStates[i] = new SectionControlState { Index = i };
        }

        // Calculate initial section positions
        RecalculateSectionPositions();

        // Listen for configuration changes to recalculate section positions
        ConfigurationStore.Instance.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ConfigurationStore.NumSections) ||
                e.PropertyName == nameof(ConfigurationStore.Tool))
            {
                RecalculateSectionPositions();
            }
        };
    }

    public void Update(Vec3 toolPosition, double toolHeading, double vehicleHeading, double speed)
    {
        var tool = ConfigurationStore.Instance.Tool;
        int numSections = NumSections;

        // Calculate tool vs vehicle heading difference (for trailed implements)
        // When tool is "catching up" after a turn, this difference is large
        double headingDiff = toolHeading - vehicleHeading;
        // Handle wrap-around at ±π
        if (headingDiff > Math.PI)
            headingDiff -= 2 * Math.PI;
        else if (headingDiff < -Math.PI)
            headingDiff += 2 * Math.PI;
        _toolVehicleHeadingDiff = headingDiff;

        // Calculate vehicle yaw rate (to distinguish turns from catch-up)
        if (!double.IsNaN(_previousVehicleHeading))
        {
            double vehicleHeadingDelta = vehicleHeading - _previousVehicleHeading;
            if (vehicleHeadingDelta > Math.PI)
                vehicleHeadingDelta -= 2 * Math.PI;
            else if (vehicleHeadingDelta < -Math.PI)
                vehicleHeadingDelta += 2 * Math.PI;
            _vehicleYawRate = vehicleHeadingDelta;
        }
        _previousVehicleHeading = vehicleHeading;

        // Calculate yaw rate (both instantaneous and smoothed)
        // Instantaneous is used for threshold checks (especially for trailed implements)
        // Smoothed is used for curve-following adjustments
        if (!double.IsNaN(_previousHeading))
        {
            double headingDelta = toolHeading - _previousHeading;
            // Handle wrap-around at ±π
            if (headingDelta > Math.PI)
                headingDelta -= 2 * Math.PI;
            else if (headingDelta < -Math.PI)
                headingDelta += 2 * Math.PI;

            // Store instantaneous rate for threshold checks
            _instantYawRate = headingDelta;

            // Exponential smoothing for curve-following: new = old * (1-α) + measured * α
            _yawRate = _yawRate * (1 - YAW_RATE_SMOOTHING) + headingDelta * YAW_RATE_SMOOTHING;
        }
        _previousHeading = toolHeading;

        // Check if speed is below cutoff
        if (speed < tool.SlowSpeedCutoff)
        {
            for (int i = 0; i < numSections; i++)
            {
                UpdateSectionOff(i);
            }
            _yawRate = 0; // Reset when stopped
            _instantYawRate = 0;
            _vehicleYawRate = 0;
            return;
        }

        // Reset timing accumulators
        _totalBoundaryMs = 0;
        _totalHeadlandMs = 0;
        _totalCoverageMs = 0;

        // Update each section
        for (int i = 0; i < numSections; i++)
        {
            UpdateSection(i, toolPosition, toolHeading, speed);
        }

        // Flush coverage updates after all sections processed (fires event once, not 16 times)
        _coverageMapService.FlushCoverageUpdate();

        // Timing logged from MainViewModel
        _sectionUpdateCounter++;

        // Fire state changed event
        SectionStateChanged?.Invoke(this, new SectionStateChangedEventArgs
        {
            SectionIndex = -1, // All sections
            IsOn = IsAnySectionOn,
            IsMappingOn = IsAnyMappingOn(),
            SectionBits = GetSectionBits()
        });
    }

    /// <summary>
    /// Update a single section's state
    /// </summary>
    private void UpdateSection(int index, Vec3 toolPosition, double toolHeading, double speed)
    {
        var section = _sectionStates[index];
        var tool = ConfigurationStore.Instance.Tool;

        // Get section world position
        var (leftEdge, rightEdge) = GetSectionWorldPosition(index, toolPosition, toolHeading);
        var sectionCenter = new Vec2(
            (leftEdge.Easting + rightEdge.Easting) / 2,
            (leftEdge.Northing + rightEdge.Northing) / 2
        );

        // Check manual override states
        if (section.ButtonState == SectionButtonState.Off)
        {
            UpdateSectionOff(index);
            return;
        }

        if (section.ButtonState == SectionButtonState.On)
        {
            UpdateSectionOn(index, leftEdge, rightEdge, toolHeading);
            return;
        }

        // Auto mode - check boundary/overlap conditions
        // Calculate look-ahead distances
        double lookAheadOnDist = speed * tool.LookAheadOnSetting;
        double lookAheadOffDist = speed * tool.LookAheadOffSetting;

        // Calculate section half-width for segment-based checks
        double halfWidth = (section.PositionRight - section.PositionLeft) / 2.0;

        // Include coverage margin in boundary check - coverage is recorded at expanded positions,
        // so we must check the ACTUAL coverage area, not just the section width
        double coverageMargin = tool.CoverageMarginMeters > 0 ? tool.CoverageMarginMeters : 0;
        double halfWidthWithMargin = halfWidth + coverageMargin;

        // Project forward for ON check - use curved projection to "look around the corner"
        var onCheckPoint = ProjectForwardCurved(sectionCenter, toolHeading, lookAheadOnDist, speed);

        // Project forward for OFF check - use curved projection
        var offCheckPoint = ProjectForwardCurved(sectionCenter, toolHeading, lookAheadOffDist, speed);

        // Check boundary conditions using segment-based detection
        // Use halfWidthWithMargin for current position to prevent coverage outside boundary
        _sectionSw.Restart();
        var currentBoundaryResult = GetSegmentBoundaryStatus(sectionCenter, toolHeading, halfWidthWithMargin);
        var lookOnBoundaryResult = GetSegmentBoundaryStatus(onCheckPoint, toolHeading, halfWidth);
        var lookOffBoundaryResult = GetSegmentBoundaryStatus(offCheckPoint, toolHeading, halfWidth);
        _totalBoundaryMs += _sectionSw.Elapsed.TotalMilliseconds;

        // Use strict threshold for current position - section must be fully inside to spray
        // This prevents spraying outside boundary when implement swings during turns
        const double BOUNDARY_THRESHOLD_STRICT = 0.95; // 95% inside required to be "in boundary"
        const double BOUNDARY_THRESHOLD_LOOKAHEAD = 0.50; // 50% for look-ahead anticipation
        bool isInBoundary = currentBoundaryResult.InsidePercent >= BOUNDARY_THRESHOLD_STRICT;
        bool lookOnInBoundary = lookOnBoundaryResult.InsidePercent >= BOUNDARY_THRESHOLD_LOOKAHEAD;
        bool lookOffInBoundary = lookOffBoundaryResult.InsidePercent >= BOUNDARY_THRESHOLD_LOOKAHEAD;

        // Check headland conditions
        // Use speed-dependent look-ahead so coverage triangles extend INTO headland consistently
        // The look-ahead compensates for MAPPING_ON_DELAY (vehicle travels during the delay)
        // Formula: lookAhead = targetPenetration + speed * delayTime
        const double TARGET_PENETRATION = 0.30;  // Target: first coverage point 30cm into headland
        const double MAPPING_DELAY_SECONDS = 0.2; // MAPPING_ON_DELAY = 2 cycles at 10Hz
        double headlandOnLookAhead = TARGET_PENETRATION + speed * MAPPING_DELAY_SECONDS;
        var headlandOnCheckPoint = ProjectForwardCurved(sectionCenter, toolHeading, headlandOnLookAhead, speed);

        _sectionSw.Restart();
        bool isInHeadland = IsPointInHeadland(sectionCenter);
        bool lookAheadInHeadland = IsPointInHeadland(headlandOnCheckPoint);
        _totalHeadlandMs += _sectionSw.Elapsed.TotalMilliseconds;

        // For ON: use speed-adjusted look-ahead so triangle extends ~30cm into headland at any speed
        // For OFF: use current position so we stop AFTER entering headland (last point in headland)
        bool lookOnInHeadland = lookAheadInHeadland;  // Turn ON when look-ahead exits headland
        bool lookOffInHeadland = isInHeadland;        // Turn OFF when current pos enters headland

        // Check coverage using segment-based detection
        // This checks the entire section width, not just center point
        _sectionSw.Restart();
        var (currentCoverage, lookOnCoverage, lookOffCoverage) = _coverageMapService.GetSegmentCoverageMulti(
            sectionCenter,
            toolHeading,
            halfWidth,
            lookAheadOnDist,
            lookAheadOffDist);
        _totalCoverageMs += _sectionSw.Elapsed.TotalMilliseconds;

        // Section is "covered" if coverage exceeds threshold
        // Use MinCoverage setting from config (0-100), default to 70% if not set
        double coverageThreshold = tool.MinCoverage > 0 ? tool.MinCoverage / 100.0 : DEFAULT_COVERAGE_THRESHOLD;
        bool lookOnCovered = lookOnCoverage.CoveragePercent >= coverageThreshold;
        bool lookOffCovered = lookOffCoverage.CoveragePercent >= coverageThreshold;

        // Store coverage percentage for potential UI display
        section.CoveragePercent = currentCoverage.CoveragePercent;

        // Update section state tracking
        section.IsInBoundary = isInBoundary;
        section.IsInHeadland = isInHeadland;
        section.IsLookOnInHeadland = lookOnInHeadland;

        // CRITICAL: If current position is outside boundary, section must be OFF immediately.
        // This prevents spraying outside the field when trailing implements swing out during turns.
        // Look-ahead is for anticipating boundaries, not overriding physical position.
        if (!isInBoundary)
        {
            UpdateSectionOff(index);
            return;
        }

        // ADDITIONAL CHECK: Verify both expanded edge points are inside boundary.
        // The segment-based check can sometimes pass when edges are outside,
        // especially when tool heading is perpendicular to boundary.
        if (coverageMargin > 0)
        {
            double perpHeading = toolHeading + Math.PI / 2.0;
            var expandedLeftEdge = new Vec2(
                sectionCenter.Easting + Math.Sin(perpHeading) * (-halfWidthWithMargin),
                sectionCenter.Northing + Math.Cos(perpHeading) * (-halfWidthWithMargin));
            var expandedRightEdge = new Vec2(
                sectionCenter.Easting + Math.Sin(perpHeading) * halfWidthWithMargin,
                sectionCenter.Northing + Math.Cos(perpHeading) * halfWidthWithMargin);

            if (!IsPointInBoundary(expandedLeftEdge) || !IsPointInBoundary(expandedRightEdge))
            {
                UpdateSectionOff(index);
                return;
            }
        }

        // Determine if section should be on
        bool shouldBeOn = !lookOnCovered      // Not already covered at look-ahead point
                       && lookOnInBoundary    // Inside boundary at look-ahead
                       && !lookOnInHeadland;  // Not in headland

        // Determine if section should be off
        bool shouldBeOff = lookOffCovered     // Already covered
                        || !lookOffInBoundary // Outside boundary at look-ahead
                        || lookOffInHeadland; // In headland

        // Apply state transitions with timing
        if (shouldBeOn && !section.IsOn)
        {
            section.SectionOnRequest = true;
            section.SectionOffRequest = false;
            section.SectionOnTimer++;
            section.SectionOffTimer = 0;

            if (section.SectionOnTimer > SECTION_ON_DELAY)
            {
                section.IsOn = true;
                section.SectionOnRequest = false;
                StartMapping(index, leftEdge, rightEdge, toolHeading);
            }
        }
        else if (shouldBeOff && section.IsOn)
        {
            section.SectionOffRequest = true;
            section.SectionOnRequest = false;
            section.SectionOffTimer++;
            section.SectionOnTimer = 0;

            // Use configured turn-off delay
            int turnOffDelay = (int)(tool.TurnOffDelay * 10); // Convert seconds to cycles at 10Hz
            if (turnOffDelay < 1) turnOffDelay = 1;

            if (section.SectionOffTimer > turnOffDelay)
            {
                section.IsOn = false;
                section.SectionOffRequest = false;
                StopMapping(index);
            }
        }
        else if (section.IsOn)
        {
            // Section is on and should stay on - update mapping
            section.SectionOnTimer = 0;
            section.SectionOffTimer = 0;
            UpdateMapping(index, leftEdge, rightEdge, toolHeading);
        }
        else
        {
            // Section is off and should stay off
            section.SectionOnTimer = 0;
            section.SectionOffTimer = 0;
        }
    }

    /// <summary>
    /// Turn a section off
    /// </summary>
    private void UpdateSectionOff(int index)
    {
        var section = _sectionStates[index];
        if (section.IsOn)
        {
            section.IsOn = false;
            StopMapping(index);
        }
        section.SectionOnTimer = 0;
        section.SectionOffTimer = 0;
        section.SectionOnRequest = false;
        section.SectionOffRequest = false;
    }

    /// <summary>
    /// Force a section on (manual override)
    /// </summary>
    private void UpdateSectionOn(int index, Vec2 leftEdge, Vec2 rightEdge, double toolHeading)
    {
        var section = _sectionStates[index];
        if (!section.IsOn)
        {
            section.IsOn = true;
            StartMapping(index, leftEdge, rightEdge, toolHeading);
        }
        else
        {
            UpdateMapping(index, leftEdge, rightEdge, toolHeading);
        }
        section.SectionOnTimer = 0;
        section.SectionOffTimer = 0;
    }

    /// <summary>
    /// Start coverage mapping for a section
    /// </summary>
    private void StartMapping(int index, Vec2 leftEdge, Vec2 rightEdge, double toolHeading)
    {
        var section = _sectionStates[index];
        section.MappingOnTimer++;

        if (section.MappingOnTimer > MAPPING_ON_DELAY && !section.IsMappingOn)
        {
            section.IsMappingOn = true;
            section.MappingOnTimer = 0;

            // Reset yaw rates when starting a new patch to prevent turn influence
            _yawRate = 0;
            _instantYawRate = 0;

            // For the FIRST point of a new patch, use straight perpendicular (no yaw adjustment).
            var (expandedLeft, expandedRight) = ApplyCoverageMarginStraight(leftEdge, rightEdge, toolHeading);

            // Get zone index (for multi-colored sections or zones)
            int zoneIndex = GetZoneIndex(index);
            _coverageMapService.StartMapping(zoneIndex, expandedLeft, expandedRight);

            // Record initial position for minimum distance filtering
            var center = new Vec2(
                (leftEdge.Easting + rightEdge.Easting) / 2,
                (leftEdge.Northing + rightEdge.Northing) / 2);
            _lastCoveragePosition[zoneIndex] = center;
        }
    }

    /// <summary>
    /// Update coverage mapping point
    /// </summary>
    private void UpdateMapping(int index, Vec2 leftEdge, Vec2 rightEdge, double toolHeading)
    {
        var section = _sectionStates[index];

        if (!section.IsMappingOn)
        {
            // Mapping hasn't started yet - continue the startup timer
            StartMapping(index, leftEdge, rightEdge, toolHeading);
        }
        else
        {
            // Skip this point if tool is yawing too fast - would create distorted triangle
            // Use a high threshold to only catch extreme cases (spikes), not normal curves
            const double MAX_YAW_FOR_POINT = 0.08; // ~4.5 degrees per update
            if (Math.Abs(_instantYawRate) > MAX_YAW_FOR_POINT)
            {
                // Skip this point, but keep patch active - we'll record the next good point
                return;
            }

            int zoneIndex = GetZoneIndex(index);

            // Check minimum distance from last coverage point to reduce edge jaggedness
            // This filters out GPS jitter while maintaining coverage accuracy
            var currentCenter = new Vec2(
                (leftEdge.Easting + rightEdge.Easting) / 2,
                (leftEdge.Northing + rightEdge.Northing) / 2);

            if (_lastCoveragePosition.TryGetValue(zoneIndex, out var lastPos))
            {
                double dx = currentCenter.Easting - lastPos.Easting;
                double dy = currentCenter.Northing - lastPos.Northing;
                double distSq = dx * dx + dy * dy;

                if (distSq < MIN_COVERAGE_POINT_DISTANCE_SQ)
                {
                    // Too close to last point - skip to reduce jagged edges
                    return;
                }
            }

            // Apply coverage margin with curve-following adjustment
            var (expandedLeft, expandedRight) = ApplyCoverageMargin(leftEdge, rightEdge, toolHeading);

            _coverageMapService.AddCoveragePoint(zoneIndex, expandedLeft, expandedRight);

            // Update last position for this zone
            _lastCoveragePosition[zoneIndex] = currentCenter;
        }
    }

    /// <summary>
    /// Apply coverage margin with straight perpendicular (no yaw adjustment).
    /// Used for the first point of a new coverage patch.
    /// </summary>
    private (Vec2 left, Vec2 right) ApplyCoverageMarginStraight(Vec2 leftEdge, Vec2 rightEdge, double toolHeading)
    {
        var tool = ConfigurationStore.Instance.Tool;
        double margin = tool.CoverageMarginMeters;

        if (margin <= 0)
            return (leftEdge, rightEdge);

        // Straight perpendicular direction (no yaw adjustment)
        double perpHeading = toolHeading + Math.PI / 2.0;
        double perpSin = Math.Sin(perpHeading);
        double perpCos = Math.Cos(perpHeading);

        // Expand left edge outward (negative direction)
        var expandedLeft = new Vec2(
            leftEdge.Easting - perpSin * margin,
            leftEdge.Northing - perpCos * margin);

        // Expand right edge outward (positive direction)
        var expandedRight = new Vec2(
            rightEdge.Easting + perpSin * margin,
            rightEdge.Northing + perpCos * margin);

        return (expandedLeft, expandedRight);
    }

    /// <summary>
    /// Apply coverage margin to expand section edges outward.
    /// This creates slight overlap between passes to prevent gaps from GPS drift.
    /// Only applies margin during straight driving to avoid spikes during turns.
    /// </summary>
    private (Vec2 left, Vec2 right) ApplyCoverageMargin(Vec2 leftEdge, Vec2 rightEdge, double toolHeading)
    {
        var tool = ConfigurationStore.Instance.Tool;
        double margin = tool.CoverageMarginMeters;

        if (margin <= 0)
            return (leftEdge, rightEdge);

        // Only apply margin when tool is aligned with vehicle and not yawing.
        // Skip margin when:
        // 1. Tool is catching up to vehicle (large heading difference) - common with trailed implements
        // 2. Tool is actively yawing (high instantaneous yaw rate)
        // The margin is only needed for straight parallel passes where gaps can occur.
        const double MAX_HEADING_DIFF = 0.05; // ~3 degrees - tool vs vehicle alignment
        const double MAX_YAW_FOR_MARGIN = 0.02; // ~1.1 degrees per update
        if (Math.Abs(_toolVehicleHeadingDiff) > MAX_HEADING_DIFF || Math.Abs(_instantYawRate) > MAX_YAW_FOR_MARGIN)
            return (leftEdge, rightEdge);

        // For straight/gentle curves, use slight yaw adjustment for smoother alignment
        double curveAdjustedHeading = toolHeading + _yawRate * 0.5;

        // Perpendicular direction (rotated 90° from adjusted heading)
        double perpHeading = curveAdjustedHeading + Math.PI / 2.0;
        double perpSin = Math.Sin(perpHeading);
        double perpCos = Math.Cos(perpHeading);

        // Expand left edge outward (negative direction)
        var expandedLeft = new Vec2(
            leftEdge.Easting - perpSin * margin,
            leftEdge.Northing - perpCos * margin);

        // Expand right edge outward (positive direction)
        var expandedRight = new Vec2(
            rightEdge.Easting + perpSin * margin,
            rightEdge.Northing + perpCos * margin);

        return (expandedLeft, expandedRight);
    }

    /// <summary>
    /// Stop coverage mapping for a section
    /// </summary>
    private void StopMapping(int index)
    {
        var section = _sectionStates[index];
        section.MappingOffTimer++;

        if (section.MappingOffTimer > MAPPING_OFF_DELAY && section.IsMappingOn)
        {
            section.IsMappingOn = false;
            section.MappingOffTimer = 0;

            int zoneIndex = GetZoneIndex(index);
            _coverageMapService.StopMapping(zoneIndex);

            // Clear last position so next patch starts fresh
            _lastCoveragePosition.Remove(zoneIndex);
        }
    }

    /// <summary>
    /// Get zone index for a section (handles zones vs individual sections)
    /// </summary>
    private int GetZoneIndex(int sectionIndex)
    {
        var tool = ConfigurationStore.Instance.Tool;
        if (tool.IsSectionsNotZones)
        {
            return sectionIndex;
        }

        // Find which zone this section belongs to
        for (int z = 1; z <= tool.Zones; z++)
        {
            if (sectionIndex < tool.GetZoneEndSection(z))
            {
                return z - 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// Project a point forward along a heading (straight line)
    /// </summary>
    private Vec2 ProjectForward(Vec2 point, double heading, double distance)
    {
        return new Vec2(
            point.Easting + Math.Sin(heading) * distance,
            point.Northing + Math.Cos(heading) * distance
        );
    }

    /// <summary>
    /// Project a point forward along a curved path using yaw rate.
    /// This "looks around the corner" by predicting where we'll actually be
    /// based on our current turn rate, rather than projecting straight ahead.
    /// </summary>
    /// <param name="point">Starting point</param>
    /// <param name="heading">Current heading in radians</param>
    /// <param name="distance">Distance to project forward</param>
    /// <param name="speed">Current speed in m/s</param>
    /// <returns>Projected point along the curved path</returns>
    private Vec2 ProjectForwardCurved(Vec2 point, double heading, double distance, double speed)
    {
        // For very slow speeds or no turn, use straight projection
        if (speed < 0.1 || Math.Abs(_yawRate) < 0.001)
        {
            return ProjectForward(point, heading, distance);
        }

        // Calculate how much heading will change over the lookahead distance
        // yawRate is rad/update, at 10Hz that's rad per 0.1 seconds
        // Time to travel distance: t = distance / speed
        // Number of updates in that time: n = t * 10 = 10 * distance / speed
        // Total heading change: Δθ = yawRate * n
        double updateRate = 10.0; // Hz
        double timeToTravel = distance / speed;
        double numUpdates = timeToTravel * updateRate;
        double headingChange = _yawRate * numUpdates;

        // For small heading changes, project along the average heading
        // This is a good approximation for typical lookahead distances
        if (Math.Abs(headingChange) < 0.5) // Less than ~30 degrees
        {
            double avgHeading = heading + headingChange * 0.5;
            return new Vec2(
                point.Easting + Math.Sin(avgHeading) * distance,
                point.Northing + Math.Cos(avgHeading) * distance
            );
        }

        // For larger turns, use proper arc math
        // Turn radius: R = speed / (yawRate * updateRate) = speed / angular_velocity
        double angularVelocity = _yawRate * updateRate; // rad/s
        double turnRadius = speed / Math.Abs(angularVelocity);

        // Arc endpoint calculation
        // The center of the turn circle is perpendicular to heading at distance R
        double turnSign = Math.Sign(_yawRate);
        double centerHeading = heading + turnSign * Math.PI / 2.0;
        double centerEasting = point.Easting + Math.Sin(centerHeading) * turnRadius;
        double centerNorthing = point.Northing + Math.Cos(centerHeading) * turnRadius;

        // Final heading after traveling the arc
        double finalHeading = heading + headingChange;

        // Position on arc at final heading (opposite side from center)
        double finalCenterHeading = finalHeading + turnSign * Math.PI / 2.0;
        return new Vec2(
            centerEasting - Math.Sin(finalCenterHeading) * turnRadius,
            centerNorthing - Math.Cos(finalCenterHeading) * turnRadius
        );
    }

    /// <summary>
    /// Check if a point is inside the field boundary
    /// </summary>
    private bool IsPointInBoundary(Vec2 point)
    {
        var boundary = _state.Field.CurrentBoundary;
        if (boundary == null || !boundary.IsValid)
            return true; // No boundary = always in

        return boundary.IsPointInside(point.Easting, point.Northing);
    }

    /// <summary>
    /// Get segment-based boundary status for a section
    /// </summary>
    private BoundaryResult GetSegmentBoundaryStatus(Vec2 sectionCenter, double heading, double halfWidth)
    {
        var boundary = _state.Field.CurrentBoundary;
        if (boundary == null || !boundary.IsValid)
            return BoundaryResult.FullyInside; // No boundary = always in

        return boundary.GetSegmentBoundaryStatus(sectionCenter, heading, halfWidth);
    }

    /// <summary>
    /// Check if a point is in the headland area
    /// </summary>
    private bool IsPointInHeadland(Vec2 point)
    {
        var tool = ConfigurationStore.Instance.Tool;

        // Check if headland section control is enabled
        if (!tool.IsHeadlandSectionControl)
            return false; // Headland control disabled

        var headlandLine = _state.Field.HeadlandLine;
        if (headlandLine == null || headlandLine.Count < 3)
            return false; // No headland = never in headland

        // Point is in headland if it's inside boundary but outside headland line
        var boundary = _state.Field.CurrentBoundary;
        if (boundary == null || !boundary.IsValid)
            return false;

        bool inBoundary = boundary.IsPointInside(point.Easting, point.Northing);
        bool insideHeadlandLine = GeometryMath.IsPointInPolygon(headlandLine, point);

        // Headland zone is BETWEEN outer boundary and headland line
        // If inside boundary but outside headland line = in headland zone
        return inBoundary && !insideHeadlandLine;
    }

    public (Vec2 left, Vec2 right) GetSectionWorldPosition(int sectionIndex, Vec3 toolPosition, double toolHeading)
    {
        if (sectionIndex < 0 || sectionIndex >= 16)
            return (new Vec2(0, 0), new Vec2(0, 0));

        var section = _sectionStates[sectionIndex];

        // Perpendicular to tool heading (right is positive)
        double perpHeading = toolHeading + Math.PI / 2.0;

        var left = new Vec2(
            toolPosition.Easting + Math.Sin(perpHeading) * section.PositionLeft,
            toolPosition.Northing + Math.Cos(perpHeading) * section.PositionLeft
        );

        var right = new Vec2(
            toolPosition.Easting + Math.Sin(perpHeading) * section.PositionRight,
            toolPosition.Northing + Math.Cos(perpHeading) * section.PositionRight
        );

        return (left, right);
    }

    public void SetSectionState(int sectionIndex, SectionButtonState state)
    {
        if (sectionIndex < 0 || sectionIndex >= 16) return;

        _sectionStates[sectionIndex].ButtonState = state;

        // For immediate UI feedback, set IsOn directly for manual states
        if (state == SectionButtonState.On)
            _sectionStates[sectionIndex].IsOn = true;
        else if (state == SectionButtonState.Off)
            _sectionStates[sectionIndex].IsOn = false;
        // Auto state will be determined by Update() based on boundaries

        SectionStateChanged?.Invoke(this, new SectionStateChangedEventArgs
        {
            SectionIndex = sectionIndex,
            IsOn = _sectionStates[sectionIndex].IsOn,
            IsMappingOn = _sectionStates[sectionIndex].IsMappingOn,
            SectionBits = GetSectionBits()
        });
    }

    public void SetAllSections(SectionButtonState state)
    {
        for (int i = 0; i < 16; i++)
        {
            _sectionStates[i].ButtonState = state;
        }

        SectionStateChanged?.Invoke(this, new SectionStateChangedEventArgs
        {
            SectionIndex = -1,
            IsOn = IsAnySectionOn,
            IsMappingOn = IsAnyMappingOn(),
            SectionBits = GetSectionBits()
        });
    }

    public void TurnAllOff()
    {
        for (int i = 0; i < 16; i++)
        {
            UpdateSectionOff(i);
        }

        // Clear all last coverage positions
        _lastCoveragePosition.Clear();

        SectionStateChanged?.Invoke(this, new SectionStateChangedEventArgs
        {
            SectionIndex = -1,
            IsOn = false,
            IsMappingOn = false,
            SectionBits = 0
        });
    }

    public void SetAllAuto()
    {
        SetAllSections(SectionButtonState.Auto);
        _masterState = SectionMasterState.Auto;
    }

    public void RecalculateSectionPositions()
    {
        var tool = ConfigurationStore.Instance.Tool;
        int numSections = NumSections;

        // Calculate total width from section widths
        double totalWidth = 0;
        for (int i = 0; i < numSections; i++)
        {
            totalWidth += tool.GetSectionWidth(i) / 100.0; // Convert cm to meters
        }

        // Position sections from left to right, centered on tool
        double currentPos = -totalWidth / 2.0 + tool.Offset;

        for (int i = 0; i < numSections; i++)
        {
            double sectionWidth = tool.GetSectionWidth(i) / 100.0;
            _sectionStates[i].PositionLeft = currentPos;
            _sectionStates[i].PositionRight = currentPos + sectionWidth;
            currentPos += sectionWidth;
        }

        // Clear positions for unused sections
        for (int i = numSections; i < 16; i++)
        {
            _sectionStates[i].PositionLeft = 0;
            _sectionStates[i].PositionRight = 0;
        }
    }

    public ushort GetSectionBits()
    {
        ushort bits = 0;
        for (int i = 0; i < 16; i++)
        {
            if (_sectionStates[i].IsOn)
            {
                bits |= (ushort)(1 << i);
            }
        }
        return bits;
    }

    private bool IsAnyMappingOn()
    {
        for (int i = 0; i < NumSections; i++)
        {
            if (_sectionStates[i].IsMappingOn) return true;
        }
        return false;
    }
}
