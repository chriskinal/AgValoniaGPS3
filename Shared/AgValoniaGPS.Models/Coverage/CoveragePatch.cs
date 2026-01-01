using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Coverage;

/// <summary>
/// Represents a coverage patch as a triangle strip.
/// Triangle strips efficiently render quads (2 triangles per segment).
///
/// Format:
/// - Vertices[0]: Color encoded as (R, G, B) in Easting, Northing, Heading
/// - Vertices[1..n]: Alternating left/right edge points
///   - Odd indices: Left edge points
///   - Even indices: Right edge points
/// </summary>
public class CoveragePatch
{
    /// <summary>
    /// Zone index this patch belongs to (0-based)
    /// </summary>
    public int ZoneIndex { get; set; }

    /// <summary>
    /// Coverage color for this patch
    /// </summary>
    public CoverageColor Color { get; set; } = CoverageColor.Default;

    /// <summary>
    /// Triangle strip vertices.
    /// First vertex contains color, subsequent vertices are alternating left/right edges.
    /// </summary>
    public List<Vec3> Vertices { get; set; } = new();

    /// <summary>
    /// Whether this patch is currently being built (still receiving points)
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Number of triangles in this patch (vertices.Count - 3 when complete)
    /// </summary>
    public int TriangleCount => Vertices.Count > 2 ? Vertices.Count - 2 : 0;

    /// <summary>
    /// Check if the patch has enough vertices to render.
    /// Require 9 vertices (1 color + 8 geometry = 3 quads minimum) to filter out
    /// tiny glitch patches from section flickering at headland boundaries.
    /// </summary>
    public bool IsRenderable => Vertices.Count >= 9;
}

/// <summary>
/// Coverage color with RGB components (0-255 range)
/// </summary>
public readonly record struct CoverageColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Default coverage color (green)
    /// </summary>
    public static CoverageColor Default => new(152, 251, 152); // Pale green

    /// <summary>
    /// Create a Vec3 representation for storage in triangle strip
    /// </summary>
    public Vec3 ToVec3() => new(R, G, B);

    /// <summary>
    /// Parse color from Vec3 (used when loading from file)
    /// </summary>
    public static CoverageColor FromVec3(Vec3 v) => new((byte)v.Easting, (byte)v.Northing, (byte)v.Heading);
}
