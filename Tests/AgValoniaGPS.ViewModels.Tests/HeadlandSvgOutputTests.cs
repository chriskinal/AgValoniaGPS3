using System.Globalization;
using System.Text;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Headland;
using AgValoniaGPS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgValoniaGPS.ViewModels.Tests;

/// <summary>
/// Tests that generate SVG visual output for headland and tram line verification.
/// SVG files are saved to the test output directory for visual inspection.
/// </summary>
[TestFixture]
public class HeadlandSvgOutputTests
{
    private static string OutputDir => Path.Combine(TestContext.CurrentContext.WorkDirectory, "svg_output");

    [OneTimeSetUp]
    public void Setup()
    {
        Directory.CreateDirectory(OutputDir);
    }

    private static List<Vec3> LoadBoundary()
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "TestData", "Fields", "UserField", "Boundary.txt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "Fields", "UserField", "Boundary.txt"),
        };
        string? filePath = paths.FirstOrDefault(File.Exists);
        if (filePath == null) return CreateSquare(200);

        var pts = new List<Vec3>();
        bool reading = false; int count = 0;
        foreach (var line in File.ReadAllLines(filePath))
        {
            var t = line.Trim();
            if (t.Contains("Boundary")) { reading = true; continue; }
            if (reading && count == 0) { if (t is "True" or "False") continue; int.TryParse(t, out count); continue; }
            if (reading && count > 0)
            {
                var p = t.Split(',');
                if (p.Length >= 3)
                    pts.Add(new Vec3(
                        double.Parse(p[0], CultureInfo.InvariantCulture),
                        double.Parse(p[1], CultureInfo.InvariantCulture),
                        double.Parse(p[2], CultureInfo.InvariantCulture)));
            }
        }
        if (pts.Count > 2)
        {
            double d = Math.Pow(pts[0].Easting - pts[^1].Easting, 2) + Math.Pow(pts[0].Northing - pts[^1].Northing, 2);
            if (d > 1.0) pts.Add(pts[0]);
        }
        return pts;
    }

    private static List<Vec3> CreateSquare(double size)
    {
        return new List<Vec3>
        {
            new(0, 0, 0), new(size, 0, Math.PI/2),
            new(size, size, Math.PI), new(0, size, 3*Math.PI/2), new(0, 0, 0)
        };
    }

    // ---------------------------------------------------------------
    // SVG rendering helpers
    // ---------------------------------------------------------------

    private class SvgRenderer
    {
        private readonly StringBuilder _sb = new();
        private double _minE, _maxE, _minN, _maxN;
        private int _width, _height;
        private double _scale;

        public SvgRenderer(IEnumerable<(double e, double n)> allPoints, int width = 800)
        {
            var pts = allPoints.ToList();
            _minE = pts.Min(p => p.e) - 20;
            _maxE = pts.Max(p => p.e) + 20;
            _minN = pts.Min(p => p.n) - 20;
            _maxN = pts.Max(p => p.n) + 20;
            _width = width;
            _scale = _width / (_maxE - _minE);
            _height = (int)((_maxN - _minN) * _scale);

            _sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{_width}\" height=\"{_height}\">");
            _sb.AppendLine($"<rect width=\"{_width}\" height=\"{_height}\" fill=\"#fafafa\"/>");
        }

        private string Pt(double e, double n) =>
            $"{((e - _minE) * _scale).ToString("F1", CultureInfo.InvariantCulture)}," +
            $"{((_maxN - n) * _scale).ToString("F1", CultureInfo.InvariantCulture)}";

        public void AddPolygon(List<Vec3> pts, string stroke, double strokeWidth = 2, string fill = "none", string dashArray = "")
        {
            var path = "M" + string.Join(" L", pts.Select(p => Pt(p.Easting, p.Northing))) + " Z";
            var dash = string.IsNullOrEmpty(dashArray) ? "" : $" stroke-dasharray=\"{dashArray}\"";
            _sb.AppendLine($"<path d=\"{path}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\"{dash}/>");
        }

        public void AddPolyline(IReadOnlyList<Vec2> pts, string stroke, double strokeWidth = 1.5, string dashArray = "")
        {
            if (pts.Count < 2) return;
            var path = "M" + string.Join(" L", pts.Select(p => Pt(p.Easting, p.Northing)));
            var dash = string.IsNullOrEmpty(dashArray) ? "" : $" stroke-dasharray=\"{dashArray}\"";
            _sb.AppendLine($"<path d=\"{path}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\"{dash}/>");
        }

        public void AddLabel(string text, double x = 10, double y = 20, string color = "#333", int fontSize = 14)
        {
            _sb.AppendLine($"<text x=\"{x}\" y=\"{y}\" font-size=\"{fontSize}\" font-family=\"sans-serif\" fill=\"{color}\">{text}</text>");
        }

        public string ToSvg()
        {
            _sb.AppendLine("</svg>");
            return _sb.ToString();
        }

        public void Save(string path)
        {
            File.WriteAllText(path, ToSvg());
        }
    }

    // ---------------------------------------------------------------
    // Headland tests with SVG output
    // ---------------------------------------------------------------

    [Test]
    public void UserField_HeadlandWithBoundaryOffset_SvgOutput()
    {
        var boundary = LoadBoundary();
        Assert.That(boundary.Count, Is.GreaterThan(10), "Need real boundary data");

        // Build headland via VM
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

        // Create a full-boundary headland segment
        var seg = new HeadlandSegment
        {
            Name = "Boundary Offset",
            Type = HeadlandSegmentType.Curve,
            Offset = 12,
            BoundaryPoints = new List<Vec3>(boundary),
            StartExtension = 50,
            EndExtension = 50
        };
        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True, "Full boundary offset should be effective");
        Assert.That(vm.HasHeadland, Is.True);

        // Get headland line
        var headland = (List<Vec3>?)typeof(MainViewModel).GetField("_currentHeadlandLine",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(vm);
        Assert.That(headland, Is.Not.Null);
        Assert.That(headland!.Count, Is.GreaterThan(10));

        // Render SVG
        var allPts = boundary.Select(p => (p.Easting, p.Northing))
            .Concat(headland.Select(p => (p.Easting, p.Northing)))
            .Concat(seg.OffsetPoints.Select(p => (p.Easting, p.Northing)));

        var svg = new SvgRenderer(allPts);
        svg.AddPolygon(boundary, "#d4760a", 2);  // boundary orange
        svg.AddPolygon(headland, "#c8b400", 2.5); // headland yellow
        svg.AddPolygon(new List<Vec3>(seg.OffsetPoints) { seg.OffsetPoints[0] }, "#00a000", 1, dashArray: "4,2"); // offset green dashed

        svg.AddLabel("Orange: boundary", 10, 20, "#d4760a");
        svg.AddLabel("Yellow: headland path", 10, 38, "#b0a000");
        svg.AddLabel("Green dashed: segment offset", 10, 56, "#00a000");

        var svgPath = Path.Combine(OutputDir, "headland_boundary_offset.svg");
        svg.Save(svgPath);
        TestContext.WriteLine($"SVG saved: {svgPath}");
        TestContext.AddTestAttachment(svgPath, "Headland boundary offset");
    }


}
