using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Controls.Panels;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Generates before/after screenshot pairs for display wiring features.
/// Screenshots are saved to screenshots/display-wiring/ for PR documentation.
/// </summary>
[TestFixture]
public class DisplayWiringScreenshots
{
    private const int W = 800;
    private const int H = 600;

    private static string ScreenshotDir
    {
        get
        {
            var dir = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "screenshots", "display-wiring");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static void Capture(Window window, string filePath)
    {
        window.UpdateLayout();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var rt = new RenderTargetBitmap(new PixelSize(W, H), new Vector(96, 96));
        rt.Render(window);
        rt.Save(filePath);
        Assert.That(File.Exists(filePath), Is.True);
        Assert.That(new FileInfo(filePath).Length, Is.GreaterThan(0));
    }

    private static DrawingContextMapControl CreateMapWithTrack()
    {
        var map = new DrawingContextMapControl();
        map.SetVehiclePosition(0, 0, 0);
        map.SetToolPosition(0, -3, 0, 6.0, 0, -3);
        map.SetCamera(0, 0, 2.0, 0);
        map.SetDayMode(true);
        map.SetGridVisible(true);

        var track = Track.FromABLine("Test AB",
            new Vec3(10, -50, 0), new Vec3(10, 50, 0));
        map.SetActiveTrack(track);

        return map;
    }

    private static Window CreateMapWindow(DrawingContextMapControl map)
    {
        return new Window
        {
            Content = map,
            Width = W,
            Height = H,
            SizeToContent = SizeToContent.Manual
        };
    }

    // ---- ExtraGuidelines ----

    [AvaloniaTest]
    public void Capture_ExtraGuidelines_Toggle()
    {
        var map = CreateMapWithTrack();
        var window = CreateMapWindow(map);
        window.Show();

        ConfigurationStore.Instance.Display.ExtraGuidelines = true;
        ConfigurationStore.Instance.Display.ExtraGuidelinesCount = 5;
        map.InvalidateVisual();
        Capture(window, Path.Combine(ScreenshotDir, "extra_guidelines_ON.png"));

        ConfigurationStore.Instance.Display.ExtraGuidelines = false;
        map.InvalidateVisual();
        Capture(window, Path.Combine(ScreenshotDir, "extra_guidelines_OFF.png"));

        window.Close();
    }

    // ---- FieldTextureVisible ----

    [AvaloniaTest]
    public void Capture_FieldTexture_Toggle()
    {
        var map = CreateMapWithTrack();
        var window = CreateMapWindow(map);
        window.Show();

        ConfigurationStore.Instance.Display.FieldTextureVisible = true;
        map.InvalidateVisual();
        Capture(window, Path.Combine(ScreenshotDir, "field_texture_ON.png"));

        ConfigurationStore.Instance.Display.FieldTextureVisible = false;
        map.InvalidateVisual();
        Capture(window, Path.Combine(ScreenshotDir, "field_texture_OFF.png"));

        window.Close();
    }

    // ---- UTurnButtonVisible ----

    [AvaloniaTest]
    public void Capture_UTurnButton_Toggle()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = new Track
        {
            Name = "Test", Type = TrackType.ABLine,
            Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) },
            IsVisible = true, IsActive = true
        };
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        // UTurn visible
        ConfigurationStore.Instance.Display.UTurnButtonVisible = true;
        var panel1 = new RightNavigationPanel { DataContext = vm };
        var w1 = new Window { Content = panel1, Width = 100, Height = 500, SizeToContent = SizeToContent.Manual };
        w1.Show();
        var rt1 = new RenderTargetBitmap(new PixelSize(100, 500), new Vector(96, 96));
        w1.UpdateLayout();
        rt1.Render(w1);
        rt1.Save(Path.Combine(ScreenshotDir, "uturn_button_ON.png"));
        w1.Close();

        // UTurn hidden
        ConfigurationStore.Instance.Display.UTurnButtonVisible = false;
        var vm2 = new MainViewModelBuilder().Build();
        vm2.SavedTracks.Add(track);
        vm2.SelectedTrack = track;
        var panel2 = new RightNavigationPanel { DataContext = vm2 };
        var w2 = new Window { Content = panel2, Width = 100, Height = 500, SizeToContent = SizeToContent.Manual };
        w2.Show();
        var rt2 = new RenderTargetBitmap(new PixelSize(100, 500), new Vector(96, 96));
        w2.UpdateLayout();
        rt2.Render(w2);
        rt2.Save(Path.Combine(ScreenshotDir, "uturn_button_OFF.png"));
        w2.Close();

        // Reset
        ConfigurationStore.Instance.Display.UTurnButtonVisible = true;
    }

    // ---- AutoDayNight ----

    [AvaloniaTest]
    public void Capture_DayNight_Modes()
    {
        var map = CreateMapWithTrack();
        var window = CreateMapWindow(map);
        window.Show();

        map.SetDayMode(true);
        map.InvalidateVisual();
        Capture(window, Path.Combine(ScreenshotDir, "daynight_DAY.png"));

        map.SetDayMode(false);
        map.InvalidateVisual();
        Capture(window, Path.Combine(ScreenshotDir, "daynight_NIGHT.png"));

        window.Close();
    }
}
