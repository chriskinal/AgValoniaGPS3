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
using System.Linq;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services;

/// <summary>
/// Core tramline offset generation service.
/// Uses Clipper2 (via PolygonOffsetService) for proper polygon inset
/// that handles concave boundaries without self-intersections.
/// </summary>
public class TramLineOffsetService : ITramLineOffsetService
{
    private readonly PolygonOffsetService _clipperOffset = new();

    /// <summary>
    /// Generate inner tramline offset from boundary fence line.
    /// Inner tramline is offset inward by (tramWidth * 0.5) + halfWheelTrack.
    /// </summary>
    public List<Vec2> GenerateInnerTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
    {
        double offset = (tramWidth * 0.5) + halfWheelTrack;
        return GenerateClipperOffset(fenceLine, offset);
    }

    /// <summary>
    /// Generate outer tramline offset from boundary fence line.
    /// Outer tramline is offset inward by (tramWidth * 0.5) - halfWheelTrack.
    /// </summary>
    public List<Vec2> GenerateOuterTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
    {
        double offset = (tramWidth * 0.5) - halfWheelTrack;
        return GenerateClipperOffset(fenceLine, offset);
    }

    /// <summary>
    /// Generate an inward offset at a specific distance from the fence line.
    /// </summary>
    public List<Vec2> GenerateClipperOffsetPublic(List<Vec3> fenceLine, double offset)
        => GenerateClipperOffset(fenceLine, offset);

    /// <summary>
    /// Use Clipper2 for proper polygon inset that handles concave shapes.
    /// </summary>
    private List<Vec2> GenerateClipperOffset(List<Vec3> fenceLine, double offset)
    {
        if (fenceLine == null || fenceLine.Count < 3)
            return new List<Vec2>();

        var boundaryVec2 = fenceLine.Select(p => new Vec2(p.Easting, p.Northing)).ToList();
        var result = _clipperOffset.CreateInwardOffset(boundaryVec2, offset);
        return result ?? new List<Vec2>();
    }
}
