using System;
using System.Collections.Generic;
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

    // Turn detection for coverage margin
    private double _previousHeading = double.NaN;
    private const double TURN_THRESHOLD_RAD = 0.05; // ~3 degrees per update = turning

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

    public void Update(Vec3 toolPosition, double toolHeading, double speed)
    {
        var tool = ConfigurationStore.Instance.Tool;
        int numSections = NumSections;

        // Detect if we're turning (heading changing rapidly)
        bool isTurning = false;
        if (!double.IsNaN(_previousHeading))
        {
            double headingDelta = Math.Abs(toolHeading - _previousHeading);
            // Handle wrap-around at ±π
            if (headingDelta > Math.PI)
                headingDelta = 2 * Math.PI - headingDelta;
            isTurning = headingDelta > TURN_THRESHOLD_RAD;
        }
        _previousHeading = toolHeading;

        // Check if speed is below cutoff
        if (speed < tool.SlowSpeedCutoff)
        {
            for (int i = 0; i < numSections; i++)
            {
                UpdateSectionOff(i);
            }
            return;
        }

        // Update each section
        for (int i = 0; i < numSections; i++)
        {
            UpdateSection(i, toolPosition, toolHeading, speed, isTurning);
        }

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
    private void UpdateSection(int index, Vec3 toolPosition, double toolHeading, double speed, bool isTurning)
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
            UpdateSectionOn(index, leftEdge, rightEdge, toolHeading, isTurning);
            return;
        }

        // Auto mode - check boundary/overlap conditions
        // Calculate look-ahead distances
        double lookAheadOnDist = speed * tool.LookAheadOnSetting;
        double lookAheadOffDist = speed * tool.LookAheadOffSetting;

        // Calculate section half-width for segment-based checks
        double halfWidth = (section.PositionRight - section.PositionLeft) / 2.0;

        // Project forward for ON check
        var onCheckPoint = ProjectForward(sectionCenter, toolHeading, lookAheadOnDist);

        // Project forward for OFF check
        var offCheckPoint = ProjectForward(sectionCenter, toolHeading, lookAheadOffDist);

        // Check boundary conditions using segment-based detection
        var currentBoundaryResult = GetSegmentBoundaryStatus(sectionCenter, toolHeading, halfWidth);
        var lookOnBoundaryResult = GetSegmentBoundaryStatus(onCheckPoint, toolHeading, halfWidth);
        var lookOffBoundaryResult = GetSegmentBoundaryStatus(offCheckPoint, toolHeading, halfWidth);

        // Section is "in boundary" if majority of segment is inside
        const double BOUNDARY_THRESHOLD = 0.50; // 50% inside = in boundary
        bool isInBoundary = currentBoundaryResult.InsidePercent >= BOUNDARY_THRESHOLD;
        bool lookOnInBoundary = lookOnBoundaryResult.InsidePercent >= BOUNDARY_THRESHOLD;
        bool lookOffInBoundary = lookOffBoundaryResult.InsidePercent >= BOUNDARY_THRESHOLD;

        // Check headland conditions (still point-based for now)
        bool isInHeadland = IsPointInHeadland(sectionCenter);
        bool lookOnInHeadland = IsPointInHeadland(onCheckPoint);
        bool lookOffInHeadland = IsPointInHeadland(offCheckPoint);

        // Check coverage using segment-based detection
        // This checks the entire section width, not just center point
        var (currentCoverage, lookOnCoverage, lookOffCoverage) = _coverageMapService.GetSegmentCoverageMulti(
            sectionCenter,
            toolHeading,
            halfWidth,
            lookAheadOnDist,
            lookAheadOffDist);

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

        // Determine if section should be on
        bool shouldBeOn = !lookOnCovered      // Not already covered at look-ahead point
                       && lookOnInBoundary    // Inside boundary
                       && !lookOnInHeadland;  // Not in headland

        // Determine if section should be off
        bool shouldBeOff = lookOffCovered     // Already covered
                        || !lookOffInBoundary // Outside boundary
                        || lookOffInHeadland; // In headland

        // Apply turn off when outside boundary setting
        if (tool.IsSectionOffWhenOut && !isInBoundary)
        {
            shouldBeOff = true;
            shouldBeOn = false;
        }

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
                StartMapping(index, leftEdge, rightEdge, toolHeading, isTurning);
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
            UpdateMapping(index, leftEdge, rightEdge, toolHeading, isTurning);
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
    private void UpdateSectionOn(int index, Vec2 leftEdge, Vec2 rightEdge, double toolHeading, bool isTurning)
    {
        var section = _sectionStates[index];
        if (!section.IsOn)
        {
            section.IsOn = true;
            StartMapping(index, leftEdge, rightEdge, toolHeading, isTurning);
        }
        else
        {
            UpdateMapping(index, leftEdge, rightEdge, toolHeading, isTurning);
        }
        section.SectionOnTimer = 0;
        section.SectionOffTimer = 0;
    }

    /// <summary>
    /// Start coverage mapping for a section
    /// </summary>
    private void StartMapping(int index, Vec2 leftEdge, Vec2 rightEdge, double toolHeading, bool isTurning)
    {
        var section = _sectionStates[index];
        section.MappingOnTimer++;

        if (section.MappingOnTimer > MAPPING_ON_DELAY && !section.IsMappingOn)
        {
            section.IsMappingOn = true;
            section.MappingOnTimer = 0;

            // Apply coverage margin to expand edges outward (disabled during turns)
            var (expandedLeft, expandedRight) = ApplyCoverageMargin(leftEdge, rightEdge, toolHeading, isTurning);

            // Get zone index (for multi-colored sections or zones)
            int zoneIndex = GetZoneIndex(index);
            _coverageMapService.StartMapping(zoneIndex, expandedLeft, expandedRight);
        }
    }

    /// <summary>
    /// Update coverage mapping point
    /// </summary>
    private void UpdateMapping(int index, Vec2 leftEdge, Vec2 rightEdge, double toolHeading, bool isTurning)
    {
        var section = _sectionStates[index];
        if (!section.IsMappingOn)
        {
            // Mapping hasn't started yet - continue the startup timer
            StartMapping(index, leftEdge, rightEdge, toolHeading, isTurning);
        }
        else
        {
            // Apply coverage margin to expand edges outward (disabled during turns)
            var (expandedLeft, expandedRight) = ApplyCoverageMargin(leftEdge, rightEdge, toolHeading, isTurning);

            int zoneIndex = GetZoneIndex(index);
            _coverageMapService.AddCoveragePoint(zoneIndex, expandedLeft, expandedRight);
        }
    }

    /// <summary>
    /// Apply coverage margin to expand section edges outward.
    /// This creates slight overlap between passes to prevent gaps from GPS drift.
    /// Margin is disabled during turns to prevent spiky triangle artifacts.
    /// </summary>
    private (Vec2 left, Vec2 right) ApplyCoverageMargin(Vec2 leftEdge, Vec2 rightEdge, double toolHeading, bool isTurning)
    {
        var tool = ConfigurationStore.Instance.Tool;
        double margin = tool.CoverageMarginMeters;

        // Skip margin when turning or margin disabled
        if (margin <= 0 || isTurning)
            return (leftEdge, rightEdge);

        // Perpendicular direction (same as section edge calculation)
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
    /// Project a point forward along a heading
    /// </summary>
    private Vec2 ProjectForward(Vec2 point, double heading, double distance)
    {
        return new Vec2(
            point.Easting + Math.Sin(heading) * distance,
            point.Northing + Math.Cos(heading) * distance
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
