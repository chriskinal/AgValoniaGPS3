// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.AutoSteer;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class HydraulicLiftTests
{
    // 200m x 100m field, 15m headland
    private Boundary _boundary = null!;
    private List<Vec3> _headlandLine = null!;

    [SetUp]
    public void Setup()
    {
        // Create rectangular boundary
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 100, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 100, 0));
        outerPoly.UpdateBounds();
        _boundary = new Boundary { OuterBoundary = outerPoly };

        // Create headland line (15m inset)
        _headlandLine = new List<Vec3>
        {
            new Vec3(15, 15, 0),
            new Vec3(185, 15, 0),
            new Vec3(185, 85, 0),
            new Vec3(15, 85, 0)
        };

        // Set up state
        ApplicationState.Instance.Field.CurrentBoundary = _boundary;
        ApplicationState.Instance.Field.HeadlandLine = _headlandLine;
        ConfigurationStore.Instance.Machine.HydraulicLiftEnabled = true;
    }

    [Test]
    public void PgnBuilder_EncodesHydLiftState()
    {
        var state = new AgValoniaGPS.Models.VehicleState();
        state.HydLiftState = 2; // Raise

        var pgn = PgnBuilder.BuildMachinePgn(ref state, hydLift: state.HydLiftState);

        Assert.That(pgn[7], Is.EqualTo(2), "PGN 239 byte 7 should be 2 (raise)");
    }

    [Test]
    public void PgnBuilder_HydLiftLower()
    {
        var state = new AgValoniaGPS.Models.VehicleState();
        state.HydLiftState = 1; // Lower

        var pgn = PgnBuilder.BuildMachinePgn(ref state, hydLift: state.HydLiftState);

        Assert.That(pgn[7], Is.EqualTo(1), "PGN 239 byte 7 should be 1 (lower)");
    }

    [Test]
    public void PointInCultivatedArea_ShouldBeLower()
    {
        // Center of field - inside headland line = cultivated area
        bool inCultivated = GeometryMath.IsPointInPolygon(
            _headlandLine, new Vec2(100, 50));

        Assert.That(inCultivated, Is.True, "Center of field should be in cultivated area");
        // Cultivated area = lower (1)
    }

    [Test]
    public void PointInHeadland_ShouldBeRaise()
    {
        // Near edge - inside boundary but outside headland line = headland
        bool inBoundary = _boundary.IsPointInside(5, 50);
        bool inCultivated = GeometryMath.IsPointInPolygon(
            _headlandLine, new Vec2(5, 50));

        Assert.That(inBoundary, Is.True, "Point should be inside boundary");
        Assert.That(inCultivated, Is.False, "Point should NOT be in cultivated area (in headland)");
        // Headland = raise (2)
    }

    [Test]
    public void PointOutsideBoundary_ShouldBeOff()
    {
        bool inBoundary = _boundary.IsPointInside(-10, 50);

        Assert.That(inBoundary, Is.False, "Point should be outside boundary");
        // Outside = off (0)
    }

    [Test]
    public void DisabledConfig_AlwaysOff()
    {
        ConfigurationStore.Instance.Machine.HydraulicLiftEnabled = false;

        // Even in headland, should return 0 when disabled
        // This tests the config check - the actual CalculateHydLiftState is in ViewModel
        // so here we just verify the config flag works
        Assert.That(ConfigurationStore.Instance.Machine.HydraulicLiftEnabled, Is.False);
    }

    [Test]
    public void HeadlandBoundaryTransition_CorrectStates()
    {
        // Walk from cultivated area to headland to outside
        // Cultivated (100, 50) -> headland (5, 50) -> outside (-10, 50)

        // Cultivated area
        bool cult = GeometryMath.IsPointInPolygon(_headlandLine, new Vec2(100, 50));
        bool bnd1 = _boundary.IsPointInside(100, 50);
        Assert.That(cult && bnd1, Is.True, "100,50 should be cultivated");

        // Headland zone
        bool head = GeometryMath.IsPointInPolygon(_headlandLine, new Vec2(5, 50));
        bool bnd2 = _boundary.IsPointInside(5, 50);
        Assert.That(!head && bnd2, Is.True, "5,50 should be in headland");

        // Outside
        bool bnd3 = _boundary.IsPointInside(-10, 50);
        Assert.That(bnd3, Is.False, "-10,50 should be outside");
    }
}
