// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Handler for <see cref="Services.Interfaces.IGpsPipelineService.CycleCompleted"/>.
    /// Marshals the result from the background thread to the UI thread for property updates.
    /// </summary>
    private void OnGpsCycleCompleted(GpsCycleResult result)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyGpsCycleResult(result));
    }

    /// <summary>
    /// Apply a GPS cycle result snapshot to all bound ViewModel properties.
    /// Called on the UI thread after the service pipeline computes results on a background thread.
    /// This is the ONLY place where GPS-derived properties are set during normal operation.
    /// </summary>
    public void ApplyGpsCycleResult(GpsCycleResult result)
    {
        // GPS position
        Latitude = result.Latitude;
        Longitude = result.Longitude;
        Easting = result.Easting;
        Northing = result.Northing;
        Heading = result.Heading;
        _speed = result.Speed;
        OnPropertyChanged(nameof(SpeedKmh));
        RollDegrees = result.RollDegrees;

        // GPS status — set FixQuality on VehicleState (FixQualityText is computed from it)
        State.Vehicle.FixQuality = result.FixQuality;
        FixQuality = GetFixQualityString(result.FixQuality);

        // Tool position — set ToolEasting LAST to trigger map update
        ToolNorthing = result.ToolNorthing;
        ToolHeadingRadians = result.ToolHeadingRadians;
        ToolWidth = result.ToolWidth;
        HitchEasting = result.HitchEasting;
        HitchNorthing = result.HitchNorthing;
        // IsToolPositionReady is a computed property from _toolPositionService — just notify
        OnPropertyChanged(nameof(IsToolPositionReady));
        ToolEasting = result.ToolEasting;

        // Guidance
        if (result.HasGuidance)
        {
            SimulatorSteerAngle = result.SteerAngle;
            CrossTrackError = result.CrossTrackError * 100; // meters to cm

            _mapService.SetGuidancePoints(
                result.GoalPointEasting, result.GoalPointNorthing,
                isActive: true);
        }

        // Update display track from pipeline (the offset line being followed)
        if (result.DisplayTrack != null)
        {
            _mapService.SetActiveTrack(result.DisplayTrack);
            _mapService.SetBaseTrack(result.BaseTrack);
        }

        // Auto-detect nearest pass when autosteer is not engaged
        if (result.NearestPassNumber.HasValue && !_isAutoSteerEngaged)
        {
            _howManyPathsAway = result.NearestPassNumber.Value;
            SyncGuidanceStateToPipeline();
        }

        // Autosteer state
        if (result.AutoSteerDisengagedThisCycle)
        {
            IsAutoSteerEngaged = false;
            StatusMessage = result.DisengageReason ?? "AutoSteer disengaged";
        }

        // Headland proximity
        State.Field.HeadlandProximityDistance = result.HeadlandProximityDistance;
        State.Field.HeadlandProximityWarning = result.HeadlandProximityWarning;

        // Section states
        if (result.SectionStates != null)
        {
            UpdateSectionPropertiesFromResult(result.SectionStates, result.SectionColorCodes);
        }

        // Status message (only if set — don't overwrite existing)
        if (result.StatusMessage != null)
            StatusMessage = result.StatusMessage;

        // Map service position update (single atomic call)
        _mapService.SetAllPositions(
            result.Easting, result.Northing, result.Heading * Math.PI / 180.0,
            result.ToolEasting, result.ToolNorthing, result.ToolHeadingRadians,
            result.ToolWidth, result.HitchEasting, result.HitchNorthing,
            result.IsToolPositionReady);
    }

    private void UpdateSectionPropertiesFromResult(bool[] states, int[]? colorCodes)
    {
        int count = Math.Min(states.Length, 16);
        for (int i = 0; i < count; i++)
        {
            switch (i)
            {
                case 0: Section1Active = states[0]; break;
                case 1: Section2Active = states[1]; break;
                case 2: Section3Active = states[2]; break;
                case 3: Section4Active = states[3]; break;
                case 4: Section5Active = states[4]; break;
                case 5: Section6Active = states[5]; break;
                case 6: Section7Active = states[6]; break;
                case 7: Section8Active = states[7]; break;
                case 8: Section9Active = states[8]; break;
                case 9: Section10Active = states[9]; break;
                case 10: Section11Active = states[10]; break;
                case 11: Section12Active = states[11]; break;
                case 12: Section13Active = states[12]; break;
                case 13: Section14Active = states[13]; break;
                case 14: Section15Active = states[14]; break;
                case 15: Section16Active = states[15]; break;
            }
        }

        if (colorCodes != null)
        {
            for (int i = 0; i < Math.Min(colorCodes.Length, count); i++)
            {
                switch (i)
                {
                    case 0: Section1ColorCode = colorCodes[0]; break;
                    case 1: Section2ColorCode = colorCodes[1]; break;
                    case 2: Section3ColorCode = colorCodes[2]; break;
                    case 3: Section4ColorCode = colorCodes[3]; break;
                    case 4: Section5ColorCode = colorCodes[4]; break;
                    case 5: Section6ColorCode = colorCodes[5]; break;
                    case 6: Section7ColorCode = colorCodes[6]; break;
                    case 7: Section8ColorCode = colorCodes[7]; break;
                    case 8: Section9ColorCode = colorCodes[8]; break;
                    case 9: Section10ColorCode = colorCodes[9]; break;
                    case 10: Section11ColorCode = colorCodes[10]; break;
                    case 11: Section12ColorCode = colorCodes[11]; break;
                    case 12: Section13ColorCode = colorCodes[12]; break;
                    case 13: Section14ColorCode = colorCodes[13]; break;
                    case 14: Section15ColorCode = colorCodes[14]; break;
                    case 15: Section16ColorCode = colorCodes[15]; break;
                }
            }
        }
    }
}
