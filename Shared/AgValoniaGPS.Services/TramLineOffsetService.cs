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
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services;

/// <summary>
/// Core tramline offset generation service.
/// Generates inner and outer tramline offset paths from boundary fence lines.
/// Used internally by TramLineService.
/// </summary>
public class TramLineOffsetService : ITramLineOffsetService
{
    private const double PIBy2 = Math.PI / 2.0;
    private const double MinSpacingSquared = 2.0; // Minimum distance squared between consecutive points

    /// <summary>
    /// Generate inner tramline offset from boundary fence line.
    /// Inner tramline is offset inward by (tramWidth * 0.5) + halfWheelTrack.
    /// </summary>
    public List<Vec2> GenerateInnerTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
    {
        double offset = (tramWidth * 0.5) + halfWheelTrack;
        return GenerateTramlineOffset(fenceLine, offset);
    }

    /// <summary>
    /// Generate outer tramline offset from boundary fence line.
    /// Outer tramline is offset inward by (tramWidth * 0.5) - halfWheelTrack.
    /// </summary>
    public List<Vec2> GenerateOuterTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
    {
        double offset = (tramWidth * 0.5) - halfWheelTrack;
        return GenerateTramlineOffset(fenceLine, offset);
    }

    /// <summary>
    /// Core algorithm to generate tramline offset from boundary fence line.
    /// Uses edge-based offset: shift each edge perpendicular, then intersect
    /// consecutive offset edges. This avoids corner overshoot from per-point
    /// normal averaging.
    /// </summary>
    private List<Vec2> GenerateTramlineOffset(List<Vec3> fenceLine, double offset)
    {
        if (fenceLine == null || fenceLine.Count < 2)
            return new List<Vec2>();

        var tramline = new List<Vec2>();
        int ptCount = fenceLine.Count;

        // Build offset edges: shift each edge perpendicular by offset distance
        var offEdges = new List<(double ax, double ay, double bx, double by)>();
        for (int i = 0; i < ptCount - 1; i++)
        {
            double dx = fenceLine[i + 1].Easting - fenceLine[i].Easting;
            double dy = fenceLine[i + 1].Northing - fenceLine[i].Northing;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) continue;

            // Perpendicular normal (inward)
            double nx = -dy / len * offset;
            double ny = dx / len * offset;
            offEdges.Add((
                fenceLine[i].Easting + nx, fenceLine[i].Northing + ny,
                fenceLine[i + 1].Easting + nx, fenceLine[i + 1].Northing + ny));
        }

        if (offEdges.Count == 0) return tramline;

        // For closed polygons, intersect consecutive offset edges including
        // the wrap-around (last edge -> first edge) to close corners properly
        bool isClosed = fenceLine.Count >= 3 &&
            Math.Pow(fenceLine[0].Easting - fenceLine[^1].Easting, 2) +
            Math.Pow(fenceLine[0].Northing - fenceLine[^1].Northing, 2) < 1.0;

        int edgeCount = offEdges.Count;
        for (int i = 0; i < edgeCount; i++)
        {
            int next = (i + 1) % edgeCount;
            if (!isClosed && i == edgeCount - 1) break; // Open: don't wrap

            var e1 = offEdges[i];
            var e2 = offEdges[next];
            double denom = (e1.bx - e1.ax) * (e2.by - e2.ay) - (e1.by - e1.ay) * (e2.bx - e2.ax);
            if (Math.Abs(denom) > 1e-10)
            {
                double t = ((e2.ax - e1.ax) * (e2.by - e2.ay) - (e2.ay - e1.ay) * (e2.bx - e2.ax)) / denom;
                double ix = e1.ax + t * (e1.bx - e1.ax);
                double iy = e1.ay + t * (e1.by - e1.ay);
                tramline.Add(new Vec2(ix, iy));
            }
            else
            {
                tramline.Add(new Vec2(e1.bx, e1.by));
            }
        }

        if (!isClosed && offEdges.Count > 0)
        {
            // Open polygon: add start and end points
            tramline.Insert(0, new Vec2(offEdges[0].ax, offEdges[0].ay));
            var last = offEdges[^1];
            tramline.Add(new Vec2(last.bx, last.by));
        }

        return tramline;
    }
}
