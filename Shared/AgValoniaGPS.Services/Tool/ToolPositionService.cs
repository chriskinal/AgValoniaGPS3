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
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Tool;

/// <summary>
/// Calculates tool/implement position relative to vehicle pivot point.
/// Implements Torriem's algorithm for trailing tool heading calculation.
///
/// Tool Types:
/// - Fixed Front: Tool rigidly attached in front of pivot
/// - Fixed Rear: Tool rigidly attached behind pivot
/// - Trailing: Tool trails behind on a hitch, swings during turns
/// - TBT (Tow-Between-Tractor): Two-stage trailing with tank trailer
/// </summary>
public class ToolPositionService : IToolPositionService
{
    // Current state
    private Vec3 _toolPosition;
    private Vec3 _toolPivotPosition;
    private Vec3 _tankPosition;
    private Vec3 _hitchPosition;
    private double _toolHeading;

    // State for trailing calculations (Torriem algorithm)
    private Vec3 _lastToolPivotPos;
    private Vec3 _lastTankPos;
    private int _startCounter;
    private const int STARTUP_FRAMES = 50;

    // Jackknife protection threshold (~115 degrees)
    private const double JACKKNIFE_THRESHOLD = 2.0;

    public Vec3 ToolPosition => _toolPosition;
    public Vec3 ToolPivotPosition => _toolPivotPosition;
    public double ToolHeading => _toolHeading;
    public Vec3 TankPosition => _tankPosition;
    public Vec3 HitchPosition => _hitchPosition;

    public event EventHandler<ToolPositionUpdatedEventArgs>? PositionUpdated;

    public void Update(Vec3 vehiclePivot, double vehicleHeading)
    {
        var tool = ConfigurationStore.Instance.Tool;

        // Calculate hitch point on vehicle
        // HitchLength is stored as positive distance; sign determined by tool type
        // Front tools: positive direction (ahead of pivot)
        // Rear tools: negative direction (behind pivot)
        double hitchDistance = Math.Abs(tool.HitchLength);
        if (tool.IsToolRearFixed || tool.IsToolTrailing || tool.IsToolTBT)
        {
            hitchDistance = -hitchDistance; // Behind the vehicle
        }
        // Front fixed keeps positive (ahead of vehicle)

        _hitchPosition = new Vec3(
            vehiclePivot.Easting + Math.Sin(vehicleHeading) * hitchDistance,
            vehiclePivot.Northing + Math.Cos(vehicleHeading) * hitchDistance,
            vehicleHeading
        );

        if (tool.IsToolFrontFixed || tool.IsToolRearFixed)
        {
            CalculateFixedToolPosition(vehiclePivot, vehicleHeading, tool);
        }
        else if (tool.IsToolTBT)
        {
            CalculateTBTToolPosition(vehicleHeading, tool);
        }
        else if (tool.IsToolTrailing)
        {
            CalculateTrailingToolPosition(vehicleHeading, tool);
        }
        else
        {
            // Default: treat as fixed rear
            CalculateFixedToolPosition(vehiclePivot, vehicleHeading, tool);
        }

        // Apply lateral offset
        ApplyLateralOffset(tool.Offset);

        // Fire event
        PositionUpdated?.Invoke(this, new ToolPositionUpdatedEventArgs
        {
            ToolPosition = _toolPosition,
            ToolHeading = _toolHeading,
            VehicleHeading = vehicleHeading,
            TankPosition = _tankPosition,
            IsTBT = tool.IsToolTBT
        });
    }

    /// <summary>
    /// Fixed tool position - tool follows vehicle heading exactly
    /// </summary>
    private void CalculateFixedToolPosition(Vec3 vehiclePivot, double vehicleHeading, ToolConfig tool)
    {
        _toolHeading = vehicleHeading;

        // For fixed tools, the tool center is at the hitch point
        // The hitch length already accounts for front vs rear
        _toolPosition = _hitchPosition;
        _toolPivotPosition = _hitchPosition;
        _tankPosition = new Vec3(0, 0, 0);
    }

    /// <summary>
    /// Trailing tool position - implements Torriem's algorithm.
    /// Tool heading calculated from movement vector.
    /// </summary>
    private void CalculateTrailingToolPosition(double vehicleHeading, ToolConfig tool)
    {
        _startCounter++;

        // During startup, snap tool behind vehicle to prevent jackknife
        if (_startCounter < STARTUP_FRAMES)
        {
            SnapToolBehindVehicle(vehicleHeading, tool);
            return;
        }

        // Torriem's algorithm: calculate heading from movement
        // Tool heading = direction from current position toward hitch
        double dx = _hitchPosition.Easting - _lastToolPivotPos.Easting;
        double dy = _hitchPosition.Northing - _lastToolPivotPos.Northing;

        // Only update heading if we've moved enough
        if (Math.Abs(dx) > 0.001 || Math.Abs(dy) > 0.001)
        {
            _toolHeading = Math.Atan2(dx, dy);
        }

        // Check for jackknife condition
        if (IsJackknifed(vehicleHeading))
        {
            SnapToolBehindVehicle(vehicleHeading, tool);
            return;
        }

        // Calculate tool pivot position - trails behind hitch
        _toolPivotPosition = new Vec3(
            _hitchPosition.Easting - Math.Sin(_toolHeading) * tool.TrailingHitchLength,
            _hitchPosition.Northing - Math.Cos(_toolHeading) * tool.TrailingHitchLength,
            _toolHeading
        );

        // Calculate tool center position (offset from pivot by TrailingToolToPivotLength)
        double pivotOffset = tool.TrailingHitchLength - tool.TrailingToolToPivotLength;
        _toolPosition = new Vec3(
            _hitchPosition.Easting - Math.Sin(_toolHeading) * pivotOffset,
            _hitchPosition.Northing - Math.Cos(_toolHeading) * pivotOffset,
            _toolHeading
        );

        _tankPosition = new Vec3(0, 0, 0);

        // Save for next frame
        _lastToolPivotPos = _toolPivotPosition;
    }

    /// <summary>
    /// TBT (Tow-Between-Tractor) position - two-stage trailing.
    /// Tank trailer follows vehicle, tool follows tank.
    /// </summary>
    private void CalculateTBTToolPosition(double vehicleHeading, ToolConfig tool)
    {
        _startCounter++;

        // During startup, snap everything behind vehicle
        if (_startCounter < STARTUP_FRAMES)
        {
            SnapTBTBehindVehicle(vehicleHeading, tool);
            return;
        }

        // Stage 1: Tank follows hitch (Torriem's algorithm)
        double tankDx = _hitchPosition.Easting - _lastTankPos.Easting;
        double tankDy = _hitchPosition.Northing - _lastTankPos.Northing;

        double tankHeading = _tankPosition.Heading;
        if (Math.Abs(tankDx) > 0.001 || Math.Abs(tankDy) > 0.001)
        {
            tankHeading = Math.Atan2(tankDx, tankDy);
        }

        // Tank position trails behind hitch
        _tankPosition = new Vec3(
            _hitchPosition.Easting - Math.Sin(tankHeading) * tool.TankTrailingHitchLength,
            _hitchPosition.Northing - Math.Cos(tankHeading) * tool.TankTrailingHitchLength,
            tankHeading
        );

        // Stage 2: Tool follows tank (Torriem's algorithm)
        double toolDx = _tankPosition.Easting - _lastToolPivotPos.Easting;
        double toolDy = _tankPosition.Northing - _lastToolPivotPos.Northing;

        if (Math.Abs(toolDx) > 0.001 || Math.Abs(toolDy) > 0.001)
        {
            _toolHeading = Math.Atan2(toolDx, toolDy);
        }

        // Check for jackknife
        if (IsJackknifed(vehicleHeading))
        {
            SnapTBTBehindVehicle(vehicleHeading, tool);
            return;
        }

        // Tool pivot trails behind tank
        _toolPivotPosition = new Vec3(
            _tankPosition.Easting - Math.Sin(_toolHeading) * tool.TrailingHitchLength,
            _tankPosition.Northing - Math.Cos(_toolHeading) * tool.TrailingHitchLength,
            _toolHeading
        );

        // Tool center position
        double pivotOffset = tool.TrailingHitchLength - tool.TrailingToolToPivotLength;
        _toolPosition = new Vec3(
            _tankPosition.Easting - Math.Sin(_toolHeading) * pivotOffset,
            _tankPosition.Northing - Math.Cos(_toolHeading) * pivotOffset,
            _toolHeading
        );

        // Save for next frame
        _lastTankPos = _tankPosition;
        _lastToolPivotPos = _toolPivotPosition;
    }

    /// <summary>
    /// Check if tool angle relative to vehicle exceeds jackknife threshold
    /// </summary>
    private bool IsJackknifed(double vehicleHeading)
    {
        double angleDiff = Math.Abs(Math.PI - Math.Abs(Math.Abs(_toolHeading - vehicleHeading) - Math.PI));
        return angleDiff > JACKKNIFE_THRESHOLD;
    }

    /// <summary>
    /// Snap trailing tool directly behind vehicle (for startup or jackknife recovery)
    /// </summary>
    private void SnapToolBehindVehicle(double vehicleHeading, ToolConfig tool)
    {
        _toolHeading = vehicleHeading;

        _toolPivotPosition = new Vec3(
            _hitchPosition.Easting - Math.Sin(vehicleHeading) * tool.TrailingHitchLength,
            _hitchPosition.Northing - Math.Cos(vehicleHeading) * tool.TrailingHitchLength,
            vehicleHeading
        );

        double pivotOffset = tool.TrailingHitchLength - tool.TrailingToolToPivotLength;
        _toolPosition = new Vec3(
            _hitchPosition.Easting - Math.Sin(vehicleHeading) * pivotOffset,
            _hitchPosition.Northing - Math.Cos(vehicleHeading) * pivotOffset,
            vehicleHeading
        );

        _tankPosition = new Vec3(0, 0, 0);
        _lastToolPivotPos = _toolPivotPosition;
    }

    /// <summary>
    /// Snap TBT (tank + tool) directly behind vehicle
    /// </summary>
    private void SnapTBTBehindVehicle(double vehicleHeading, ToolConfig tool)
    {
        _toolHeading = vehicleHeading;

        // Tank behind hitch
        _tankPosition = new Vec3(
            _hitchPosition.Easting - Math.Sin(vehicleHeading) * tool.TankTrailingHitchLength,
            _hitchPosition.Northing - Math.Cos(vehicleHeading) * tool.TankTrailingHitchLength,
            vehicleHeading
        );

        // Tool behind tank
        _toolPivotPosition = new Vec3(
            _tankPosition.Easting - Math.Sin(vehicleHeading) * tool.TrailingHitchLength,
            _tankPosition.Northing - Math.Cos(vehicleHeading) * tool.TrailingHitchLength,
            vehicleHeading
        );

        double pivotOffset = tool.TrailingHitchLength - tool.TrailingToolToPivotLength;
        _toolPosition = new Vec3(
            _tankPosition.Easting - Math.Sin(vehicleHeading) * pivotOffset,
            _tankPosition.Northing - Math.Cos(vehicleHeading) * pivotOffset,
            vehicleHeading
        );

        _lastTankPos = _tankPosition;
        _lastToolPivotPos = _toolPivotPosition;
    }

    /// <summary>
    /// Apply lateral offset to tool position
    /// </summary>
    private void ApplyLateralOffset(double offset)
    {
        if (Math.Abs(offset) < 0.001) return;

        // Perpendicular to tool heading (right is positive)
        double perpHeading = _toolHeading + Math.PI / 2.0;

        _toolPosition = new Vec3(
            _toolPosition.Easting + Math.Sin(perpHeading) * offset,
            _toolPosition.Northing + Math.Cos(perpHeading) * offset,
            _toolPosition.Heading
        );
    }

    public (Vec3 left, Vec3 right) GetToolEdgePositions()
    {
        var tool = ConfigurationStore.Instance.Tool;
        double halfWidth = tool.Width / 2.0;

        return GetSectionEdgePositions(-halfWidth, halfWidth);
    }

    public Vec3 GetSectionPosition(int sectionIndex, double sectionLeft, double sectionRight)
    {
        double sectionCenter = (sectionLeft + sectionRight) / 2.0;

        // Perpendicular to tool heading
        double perpHeading = _toolHeading + Math.PI / 2.0;

        return new Vec3(
            _toolPosition.Easting + Math.Sin(perpHeading) * sectionCenter,
            _toolPosition.Northing + Math.Cos(perpHeading) * sectionCenter,
            _toolHeading
        );
    }

    public (Vec3 left, Vec3 right) GetSectionEdgePositions(double sectionLeft, double sectionRight)
    {
        // Perpendicular to tool heading (right is positive)
        double perpHeading = _toolHeading + Math.PI / 2.0;

        var left = new Vec3(
            _toolPosition.Easting + Math.Sin(perpHeading) * sectionLeft,
            _toolPosition.Northing + Math.Cos(perpHeading) * sectionLeft,
            _toolHeading
        );

        var right = new Vec3(
            _toolPosition.Easting + Math.Sin(perpHeading) * sectionRight,
            _toolPosition.Northing + Math.Cos(perpHeading) * sectionRight,
            _toolHeading
        );

        return (left, right);
    }

    public void ResetTrailingState(Vec3 vehiclePivot, double vehicleHeading)
    {
        _startCounter = 0;

        var tool = ConfigurationStore.Instance.Tool;

        // Calculate hitch position
        _hitchPosition = new Vec3(
            vehiclePivot.Easting + Math.Sin(vehicleHeading) * tool.HitchLength,
            vehiclePivot.Northing + Math.Cos(vehicleHeading) * tool.HitchLength,
            vehicleHeading
        );

        if (tool.IsToolTBT)
        {
            SnapTBTBehindVehicle(vehicleHeading, tool);
        }
        else
        {
            SnapToolBehindVehicle(vehicleHeading, tool);
        }
    }
}
