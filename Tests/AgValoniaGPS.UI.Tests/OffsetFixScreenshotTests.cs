using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Views.Controls;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Visual verification of GPS offset fix (#36).
/// Captures before/after screenshots showing vehicle position
/// relative to a field boundary.
/// </summary>
[TestFixture]
public class OffsetFixScreenshotTests
{
    private static string ScreenshotDir
    {
        get
        {
            var dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots", "offset_fix");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [AvaloniaTest]
    public void OffsetFix_TrailingImplementFollowsVehicle()
    {
        // Configure trailing implement
        var config = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        config.Tool.HitchLength = 2.0;
        config.Tool.TrailingHitchLength = 3.0;
        config.Tool.IsToolTrailing = true;
        config.Tool.IsToolRearFixed = false;
        config.Tool.IsToolFrontFixed = false;
        config.Tool.IsToolTBT = false;
        config.Tool.Width = 6.0;

        var toolService = new AgValoniaGPS.Services.Tool.ToolPositionService();

        var mapControl = new DrawingContextMapControl
        {
            Width = 600,
            Height = 600,
            IsGridVisible = true
        };

        // Set boundary: 40m square centered at (0, 0)
        var outerPoly = new AgValoniaGPS.Models.BoundaryPolygon();
        outerPoly.Points.Add(new AgValoniaGPS.Models.BoundaryPoint { Easting = -20, Northing = -20 });
        outerPoly.Points.Add(new AgValoniaGPS.Models.BoundaryPoint { Easting = 20, Northing = -20 });
        outerPoly.Points.Add(new AgValoniaGPS.Models.BoundaryPoint { Easting = 20, Northing = 20 });
        outerPoly.Points.Add(new AgValoniaGPS.Models.BoundaryPoint { Easting = -20, Northing = 20 });
        var boundary = new AgValoniaGPS.Models.Boundary { OuterBoundary = outerPoly };
        mapControl.SetBoundary(boundary);

        // Zoom in 8x
        for (int i = 0; i < 8; i++) mapControl.Zoom(1.2);

        var window = new Window
        {
            Content = mapControl,
            Width = 600,
            Height = 600,
            SizeToContent = SizeToContent.Manual,
            Background = Brushes.Black
        };
        window.Show();

        // Drive north a few frames to establish trailing heading
        for (int i = 0; i < 60; i++)
        {
            double n = -10 + i * 0.5; // Drive from -10 to +20
            var pos = new Vec3(0, n, 0); // Heading north
            toolService.Update(pos, 0);
        }

        // Screenshot 1: Vehicle at center, trailing implement behind
        double vehicleN = 20;
        var vehiclePos = new Vec3(0, vehicleN, 0);
        toolService.Update(vehiclePos, 0);
        mapControl.SetVehiclePosition(0, vehicleN, 0);
        mapControl.SetToolPosition(
            toolService.ToolPosition.Easting, toolService.ToolPosition.Northing,
            toolService.ToolHeading, 6,
            toolService.HitchPosition.Easting, toolService.HitchPosition.Northing);

        CaptureScreenshot(window, 600, 600,
            Path.Combine(ScreenshotDir, "01_trailing_no_offset.png"));

        // Screenshot 2: Apply 10m south offset - both tractor and implement shift
        double driftN = -10;
        var driftedPos = new Vec3(0, vehicleN + driftN, 0);
        toolService.Update(driftedPos, 0);
        mapControl.SetVehiclePosition(0, vehicleN + driftN, 0);
        mapControl.SetToolPosition(
            toolService.ToolPosition.Easting, toolService.ToolPosition.Northing,
            toolService.ToolHeading, 6,
            toolService.HitchPosition.Easting, toolService.HitchPosition.Northing);

        CaptureScreenshot(window, 600, 600,
            Path.Combine(ScreenshotDir, "02_trailing_offset_10m_south.png"));

        // Verify: hitch is still close to vehicle (not 100m away)
        double hitchToVehicle = Math.Abs(toolService.HitchPosition.Northing - (vehicleN + driftN));
        Assert.That(hitchToVehicle, Is.LessThan(5.0),
            $"Hitch should be within 5m of vehicle, got {hitchToVehicle:F1}m");

        // Verify screenshots
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "01_trailing_no_offset.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "02_trailing_offset_10m_south.png")), Is.True);

        TestContext.Out.WriteLine($"Screenshots saved to: {ScreenshotDir}");
        TestContext.Out.WriteLine($"Hitch distance from vehicle: {hitchToVehicle:F2}m");

        // Screenshot 3: Simulate the bug - vehicle jumps but tool NOT updated yet
        // This is what happens when PropertyChanged fires for Easting before ToolEasting
        mapControl.SetVehiclePosition(0, vehicleN + driftN + 100, 0);
        // Tool position NOT updated - still at old location
        CaptureScreenshot(window, 600, 600,
            Path.Combine(ScreenshotDir, "03_BUG_vehicle_jumped_tool_stale.png"));

        // Screenshot 4: After tool catches up (both updated)
        var jumpedPos = new Vec3(0, vehicleN + driftN + 100, 0);
        toolService.ResetTrailingState(jumpedPos, 0);
        toolService.Update(jumpedPos, 0);
        mapControl.SetVehiclePosition(0, vehicleN + driftN + 100, 0);
        mapControl.SetToolPosition(
            toolService.ToolPosition.Easting, toolService.ToolPosition.Northing,
            toolService.ToolHeading, 6,
            toolService.HitchPosition.Easting, toolService.HitchPosition.Northing);
        CaptureScreenshot(window, 600, 600,
            Path.Combine(ScreenshotDir, "04_FIXED_both_updated.png"));
    }

    private static void CaptureScreenshot(Window window, int width, int height, string filePath)
    {
        window.UpdateLayout();
        var renderTarget = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        renderTarget.Render(window);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        renderTarget.Save(filePath);
    }
}
