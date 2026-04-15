using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Headland;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Tram;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using System.Globalization;
using System.Text;

namespace AgValoniaGPS.ViewModels.Tests;

/// <summary>
/// Stress-test headland and tram offset algorithms using field shapes
/// based on each letter of the alphabet. Letters like A, B, D, O test
/// convex shapes; C, L, U test concave shapes; H, K, M, W test complex
/// concavities with narrow sections.
/// </summary>
[TestFixture]
public class AlphabetFieldTests
{
    private static string SvgOutputDir => Path.Combine(TestContext.CurrentContext.WorkDirectory, "svg_output");

    [OneTimeSetUp]
    public void Setup()
    {
        Directory.CreateDirectory(SvgOutputDir);
        ConfigurationStore.Instance.Tram.TramWidth = 24.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;
    }

    /// <summary>
    /// Generate a field-shaped polygon from a letter using SKPath text rendering.
    /// Uses SkiaSharp to convert text glyphs to polygon outlines.
    /// Scale: each letter is roughly 200x250m.
    /// </summary>
    private static List<Vec3> GenerateLetterField(char letter)
    {
        using var paint = new SKPaint
        {
            TextSize = 200,
            IsAntialias = false,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                ?? SKTypeface.Default
        };

        using var textPath = paint.GetTextPath(letter.ToString(), 0, 200);
        if (textPath == null || textPath.PointCount < 3)
        {
            // Fallback: simple rectangle
            return new List<Vec3>
            {
                new(0, 0, 0), new(100, 0, Math.PI/2),
                new(100, 120, Math.PI), new(0, 120, 3*Math.PI/2), new(0, 0, 0)
            };
        }

        // Sample the path at regular intervals to get polygon points
        using var measure = new SKPathMeasure(textPath, true);
        var pts = new List<Vec3>();
        float totalLen = measure.Length;
        float step = Math.Max(1.0f, totalLen / 300); // ~300 points max

        for (float d = 0; d < totalLen; d += step)
        {
            if (measure.GetPosition(d, out var pos))
            {
                // Scale and flip Y (SKPath has Y-down, we want Y-up)
                pts.Add(new Vec3(pos.X, 250 - pos.Y, 0));
            }
        }

        if (pts.Count < 3) return pts;

        // Compute headings
        for (int i = 0; i < pts.Count; i++)
        {
            int next = (i + 1) % pts.Count;
            double dx = pts[next].Easting - pts[i].Easting;
            double dy = pts[next].Northing - pts[i].Northing;
            pts[i] = new Vec3(pts[i].Easting, pts[i].Northing, Math.Atan2(dx, dy));
        }

        // Close
        pts.Add(pts[0]);
        return pts;
    }

    private static bool IsPointInPolygon(double px, double py, List<Vec3> polygon)
    {
        bool inside = false;
        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            double yi = polygon[i].Northing, yj = polygon[j].Northing;
            double xi = polygon[i].Easting, xj = polygon[j].Easting;
            if (((yi > py) != (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }

    // ---------------------------------------------------------------
    // Test each letter
    // ---------------------------------------------------------------

    [TestCase('A')] [TestCase('B')] [TestCase('C')] [TestCase('D')]
    [TestCase('E')] [TestCase('F')] [TestCase('G')] [TestCase('H')]
    [TestCase('I')] [TestCase('J')] [TestCase('K')] [TestCase('L')]
    [TestCase('M')] [TestCase('N')] [TestCase('O')] [TestCase('P')]
    [TestCase('Q')] [TestCase('R')] [TestCase('S')] [TestCase('T')]
    [TestCase('U')] [TestCase('V')] [TestCase('W')] [TestCase('X')]
    [TestCase('Y')] [TestCase('Z')]
    public void Letter_HeadlandOffset_AllPointsInsideBoundary(char letter)
    {
        var boundary = GenerateLetterField(letter);
        Assert.That(boundary.Count, Is.GreaterThan(3), $"Letter {letter} should have polygon points");

        var vm = new MainViewModelBuilder().Build();
        var bndModel = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = boundary.Select(p => new BoundaryPoint(p.Easting, p.Northing, p.Heading)).ToList()
            }
        };
        bndModel.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, bndModel);

        var seg = new HeadlandSegment
        {
            Name = $"Letter_{letter}",
            Type = HeadlandSegmentType.Curve,
            Offset = 3, // 3m offset for letter-sized fields
            BoundaryPoints = new List<Vec3>(boundary)
        };

        vm.ComputeSegmentOffset(seg);

        // Skip if offset collapsed (letter too small for 8m offset)
        if (seg.OffsetPoints.Count < 3)
        {
            Assert.Pass($"Letter {letter}: offset collapsed (shape too narrow for 8m)");
            return;
        }

        // All offset points must be inside the boundary
        int outside = 0;
        var outsidePts = new List<string>();
        foreach (var pt in seg.OffsetPoints)
        {
            if (!IsPointInPolygon(pt.Easting, pt.Northing, boundary))
            {
                outside++;
                if (outsidePts.Count < 3)
                    outsidePts.Add($"({pt.Easting:F1},{pt.Northing:F1})");
            }
        }

        Assert.That(outside, Is.EqualTo(0),
            $"Letter {letter}: {outside}/{seg.OffsetPoints.Count} offset points outside boundary: {string.Join(", ", outsidePts)}");
    }

    [TestCase('A')] [TestCase('B')] [TestCase('C')] [TestCase('D')]
    [TestCase('E')] [TestCase('F')] [TestCase('G')] [TestCase('H')]
    [TestCase('I')] [TestCase('J')] [TestCase('K')] [TestCase('L')]
    [TestCase('M')] [TestCase('N')] [TestCase('O')] [TestCase('P')]
    [TestCase('Q')] [TestCase('R')] [TestCase('S')] [TestCase('T')]
    [TestCase('U')] [TestCase('V')] [TestCase('W')] [TestCase('X')]
    [TestCase('Y')] [TestCase('Z')]
    public void Letter_HeadlandOffset_AreaSmallerAndFullyContained(char letter)
    {
        var boundary = GenerateLetterField(letter);

        var vm = new MainViewModelBuilder().Build();
        var bndModel = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = boundary.Select(p => new BoundaryPoint(p.Easting, p.Northing, p.Heading)).ToList()
            }
        };
        bndModel.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, bndModel);

        var seg = new HeadlandSegment
        {
            Name = $"Letter_{letter}",
            Type = HeadlandSegmentType.Curve,
            Offset = 3,
            BoundaryPoints = new List<Vec3>(boundary)
        };

        vm.ComputeSegmentOffset(seg);

        if (seg.OffsetPoints.Count < 3)
        {
            Assert.Pass($"Letter {letter}: offset collapsed");
            return;
        }

        // 1. Area check: offset area must be strictly smaller than boundary
        double bndArea = Math.Abs(SignedArea(boundary));
        double offArea = Math.Abs(SignedArea(seg.OffsetPoints.ToList()));
        Assert.That(offArea, Is.LessThan(bndArea),
            $"Letter {letter}: offset area ({offArea:F0}) must be < boundary ({bndArea:F0})");

        // 2. Clipper2 containment: intersect offset with boundary
        //    If offset is fully inside, clipped area == offset area
        double scale = 1000.0;
        var bndPath = new Clipper2Lib.Path64(boundary.Count);
        foreach (var p in boundary)
            bndPath.Add(new Clipper2Lib.Point64((long)(p.Easting * scale), (long)(p.Northing * scale)));

        var offPath = new Clipper2Lib.Path64(seg.OffsetPoints.Count);
        foreach (var p in seg.OffsetPoints)
            offPath.Add(new Clipper2Lib.Point64((long)(p.Easting * scale), (long)(p.Northing * scale)));

        var clipper = new Clipper2Lib.Clipper64();
        clipper.AddSubject(new Clipper2Lib.Paths64 { offPath });
        clipper.AddClip(new Clipper2Lib.Paths64 { bndPath });
        var clipped = new Clipper2Lib.Paths64();
        clipper.Execute(Clipper2Lib.ClipType.Intersection, Clipper2Lib.FillRule.NonZero, clipped);

        double clippedArea = 0;
        foreach (var p in clipped)
            clippedArea += Math.Abs(Clipper2Lib.Clipper.Area(p));
        double offAreaScaled = Math.Abs(Clipper2Lib.Clipper.Area(offPath));

        // Nothing should be clipped away (>99% retained)
        double ratio = offAreaScaled > 0 ? clippedArea / offAreaScaled : 0;
        Assert.That(ratio, Is.GreaterThan(0.99),
            $"Letter {letter}: {(1-ratio)*100:F1}% of offset outside boundary (not fully contained)");
    }

    private static double SignedArea(List<Vec3> polygon)
    {
        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            area += (p2.Easting - p1.Easting) * (p2.Northing + p1.Northing);
        }
        return area / 2.0;
    }

    /// <summary>
    /// Generate SVG showing all 26 letter fields with their headland offsets.
    /// </summary>
    [Test]
    public void AllLetters_GenerateSvgCatalog()
    {
        var sb = new StringBuilder();
        int cols = 6;
        int cellW = 140, cellH = 170;
        int totalW = cols * cellW;
        int totalH = ((26 + cols - 1) / cols) * cellH;

        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{totalW}\" height=\"{totalH}\">");
        sb.AppendLine($"<rect width=\"{totalW}\" height=\"{totalH}\" fill=\"#fafafa\"/>");

        for (int idx = 0; idx < 26; idx++)
        {
            char letter = (char)('A' + idx);
            int col = idx % cols;
            int row = idx / cols;
            double ox = col * cellW + 15;
            double oy = row * cellH + 25;

            var boundary = GenerateLetterField(letter);

            var vm = new MainViewModelBuilder().Build();
            var bndModel = new Boundary
            {
                OuterBoundary = new BoundaryPolygon
                {
                    Points = boundary.Select(p => new BoundaryPoint(p.Easting, p.Northing, p.Heading)).ToList()
                }
            };
            bndModel.OuterBoundary.UpdateBounds();
            typeof(MainViewModel).GetField("_currentBoundary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(vm, bndModel);

            var seg = new HeadlandSegment
            {
                Name = $"{letter}",
                Type = HeadlandSegmentType.Curve,
                Offset = 3,
                BoundaryPoints = new List<Vec3>(boundary)
            };
            vm.ComputeSegmentOffset(seg);

            // Label
            sb.AppendLine($"<text x=\"{ox}\" y=\"{oy - 5}\" font-size=\"16\" font-weight=\"bold\" font-family=\"sans-serif\" fill=\"#333\">{letter}</text>");

            // Boundary (orange)
            string bndPath = "M" + string.Join(" L", boundary.Select(p =>
                $"{(p.Easting + ox).ToString("F1", CultureInfo.InvariantCulture)},{(120 - p.Northing + oy).ToString("F1", CultureInfo.InvariantCulture)}")) + " Z";
            sb.AppendLine($"<path d=\"{bndPath}\" fill=\"none\" stroke=\"#d4760a\" stroke-width=\"1.5\"/>");

            // Offset (green)
            if (seg.OffsetPoints.Count >= 3)
            {
                string offPath = "M" + string.Join(" L", seg.OffsetPoints.Select(p =>
                    $"{(p.Easting + ox).ToString("F1", CultureInfo.InvariantCulture)},{(120 - p.Northing + oy).ToString("F1", CultureInfo.InvariantCulture)}")) + " Z";
                sb.AppendLine($"<path d=\"{offPath}\" fill=\"none\" stroke=\"#00a000\" stroke-width=\"1\"/>");
            }
        }

        sb.AppendLine("</svg>");

        var svgPath = Path.Combine(SvgOutputDir, "alphabet_fields.svg");
        File.WriteAllText(svgPath, sb.ToString());
        TestContext.AddTestAttachment(svgPath, "Alphabet field catalog");
    }
}
