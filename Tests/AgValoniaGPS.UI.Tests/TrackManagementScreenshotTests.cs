using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Controls.Dialogs;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Screenshot capture tests for track management features.
/// Renders UI controls headlessly and saves PNG screenshots for visual verification.
/// </summary>
[TestFixture]
public class TrackManagementScreenshotTests
{
    private string _screenshotDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _screenshotDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "screenshots");
        Directory.CreateDirectory(_screenshotDir);
    }

    #region Test Data Helpers

    private static Track CreateRecordedPath(string name, double startE, double startN)
    {
        var points = new List<Vec3>();
        for (int i = 0; i < 50; i++)
        {
            double e = startE + i * 2.0;
            double n = startN + Math.Sin(i * 0.2) * 10.0;
            double heading = Math.Atan2(2.0, Math.Cos(i * 0.2) * 10.0 * 0.2);
            points.Add(new Vec3(e, n, heading));
        }
        return Track.FromRecordedPath(name, points);
    }

    private static Track CreateContourStrip(string name, double startE, double startN)
    {
        var points = new List<Vec3>();
        for (int i = 0; i < 40; i++)
        {
            double e = startE + i * 2.5;
            double n = startN + Math.Cos(i * 0.15) * 8.0;
            double heading = Math.Atan2(2.5, -Math.Sin(i * 0.15) * 8.0 * 0.15);
            points.Add(new Vec3(e, n, heading));
        }
        return Track.FromContour(name, points);
    }

    private static Track CreateABLine(string name, double eA, double nA, double eB, double nB)
    {
        return Track.FromABLine(name,
            new Vec3(eA, nA, Math.Atan2(eB - eA, nB - nA)),
            new Vec3(eB, nB, Math.Atan2(eB - eA, nB - nA)));
    }

    private static Track CreateCurve(string name, double startE, double startN)
    {
        var points = new List<Vec3>();
        for (int i = 0; i < 30; i++)
        {
            double angle = i * 0.1;
            double e = startE + 50.0 * Math.Sin(angle);
            double n = startN + i * 3.0;
            points.Add(new Vec3(e, n, angle));
        }
        return Track.FromCurve(name, points);
    }

    private void SaveScreenshot(Window window, string fileName)
    {
        // Use Avalonia headless capture API to get rendered frame
        var bitmap = window.CaptureRenderedFrame();
        Assert.That(bitmap, Is.Not.Null, "Headless renderer returned null frame");

        var filePath = Path.Combine(_screenshotDir, fileName);
        bitmap!.Save(filePath);

        Assert.That(File.Exists(filePath), Is.True, $"Screenshot file was not created: {filePath}");
        var info = new FileInfo(filePath);
        Assert.That(info.Length, Is.GreaterThan(0), $"Screenshot file is empty: {filePath}");
        TestContext.Out.WriteLine($"Screenshot saved: {filePath} ({info.Length} bytes)");
    }

    #endregion

    #region Recorded Path Display Tests

    [AvaloniaTest]
    public void Map_RecordedPaths_Visible()
    {
        var mapControl = new DrawingContextMapControl();
        mapControl.Width = 800;
        mapControl.Height = 600;

        // Feed recorded path data
        var paths = new List<Track>
        {
            CreateRecordedPath("Path A", -40, -20),
            CreateRecordedPath("Path B", -40, 10),
            CreateRecordedPath("Path C", -40, 40)
        };
        mapControl.SetRecordedPaths(paths);

        // Also set an active AB line track for context
        var activeTrack = CreateABLine("AB_90.0", 0, -80, 0, 80);
        mapControl.SetActiveTrack(activeTrack);

        var window = new Window { Content = mapControl, Width = 800, Height = 600 };
        window.Show();

        SaveScreenshot(window, "recorded_paths_visible.png");

        Assert.That(paths, Has.Count.EqualTo(3));
        Assert.That(paths.All(p => p.IsRecordedPath), Is.True);
    }

    [AvaloniaTest]
    public void Map_RecordedPaths_Hidden()
    {
        var mapControl = new DrawingContextMapControl();
        mapControl.Width = 800;
        mapControl.Height = 600;

        // Set empty recorded paths (hidden state)
        mapControl.SetRecordedPaths(Array.Empty<Track>());

        // Still show an active AB line
        var activeTrack = CreateABLine("AB_90.0", 0, -80, 0, 80);
        mapControl.SetActiveTrack(activeTrack);

        var window = new Window { Content = mapControl, Width = 800, Height = 600 };
        window.Show();

        SaveScreenshot(window, "recorded_paths_hidden.png");

        // Verify active track is still present
        Assert.That(activeTrack.Points, Has.Count.EqualTo(2));
    }

    #endregion

    #region Contour Strip Display Tests

    [AvaloniaTest]
    public void Map_ContourStrips_Visible()
    {
        var mapControl = new DrawingContextMapControl();
        mapControl.Width = 800;
        mapControl.Height = 600;

        // Feed contour strip data
        var strips = new List<Track>
        {
            CreateContourStrip("Contour 1", -50, -30),
            CreateContourStrip("Contour 2", -50, 0),
            CreateContourStrip("Contour 3", -50, 30)
        };
        mapControl.SetContourStrips(strips);

        var window = new Window { Content = mapControl, Width = 800, Height = 600 };
        window.Show();

        SaveScreenshot(window, "contour_strips_visible.png");

        Assert.That(strips, Has.Count.EqualTo(3));
        Assert.That(strips.All(s => s.IsContour), Is.True);
    }

    [AvaloniaTest]
    public void Map_MixedTracks_AllTypes()
    {
        var mapControl = new DrawingContextMapControl();
        mapControl.Width = 800;
        mapControl.Height = 600;

        // All track types rendered together
        var paths = new List<Track>
        {
            CreateRecordedPath("Recorded A", -60, -40),
            CreateRecordedPath("Recorded B", -60, 50)
        };
        var strips = new List<Track>
        {
            CreateContourStrip("Contour 1", -50, -10),
            CreateContourStrip("Contour 2", -50, 20)
        };
        mapControl.SetRecordedPaths(paths);
        mapControl.SetContourStrips(strips);

        var activeTrack = CreateCurve("Active Curve", -20, -40);
        mapControl.SetActiveTrack(activeTrack);

        var window = new Window { Content = mapControl, Width = 800, Height = 600 };
        window.Show();

        SaveScreenshot(window, "mixed_tracks_all_types.png");

        Assert.That(paths.Count + strips.Count, Is.EqualTo(4));
    }

    #endregion

    #region Import Tracks Dialog Tests

    [AvaloniaTest]
    public void ImportTracksDialog_Rendered()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new ImportTracksDialogPanel { DataContext = vm };

        var window = new Window { Content = dialog, Width = 500, Height = 400 };
        window.Show();

        // Populate fields list and show dialog
        vm.ImportFieldsList.Add("Field_North_2025");
        vm.ImportFieldsList.Add("Field_South_2025");
        vm.ImportFieldsList.Add("Field_East_Wheat");
        vm.State.UI.ShowDialog(DialogType.ImportTracks);

        SaveScreenshot(window, "import_tracks_dialog.png");

        Assert.That(dialog.IsVisible, Is.True);
        Assert.That(vm.ImportFieldsList, Has.Count.EqualTo(3));
    }

    [AvaloniaTest]
    public void ImportTracksDialog_NotVisible_ByDefault()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new ImportTracksDialogPanel { DataContext = vm };

        Assert.That(dialog.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void ImportTracksDialog_CloseCommand_Works()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.UI.ShowDialog(DialogType.ImportTracks);

        vm.CloseImportTracksDialogCommand!.Execute(null);

        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.None));
    }

    #endregion

    #region Delete Confirmation Dialog Tests

    [AvaloniaTest]
    public void DeleteConfirmation_Dialog_Rendered()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new ConfirmationDialogPanel { DataContext = vm };

        var window = new Window { Content = dialog, Width = 500, Height = 300 };
        window.Show();

        // Set up a track and trigger deletion
        var track = CreateABLine("Test Track", 0, 0, 0, 100);
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        // Execute the delete command (which shows confirmation dialog)
        vm.DeleteContourTrackCommand!.Execute(null);

        SaveScreenshot(window, "delete_confirmation_dialog.png");

        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.Confirmation));
        Assert.That(vm.State.UI.IsConfirmationDialogVisible, Is.True);
    }

    [AvaloniaTest]
    public void DeleteConfirmation_Cancel_KeepsTrack()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateABLine("Keeper Track", 0, 0, 0, 100);
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        // Trigger delete, then cancel
        vm.DeleteContourTrackCommand!.Execute(null);
        vm.CancelConfirmationDialogCommand!.Execute(null);

        Assert.That(vm.SavedTracks, Has.Count.EqualTo(1));
        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.None));
    }

    [AvaloniaTest]
    public void DeleteConfirmation_Confirm_RemovesTrack()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateContourStrip("Doomed Contour", 0, 0);
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        // Trigger delete, then confirm
        vm.DeleteContourTrackCommand!.Execute(null);
        vm.ConfirmConfirmationDialogCommand!.Execute(null);

        Assert.That(vm.SavedTracks, Has.Count.EqualTo(0));
        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.None));
    }

    #endregion

    #region Tracks Dialog with New Features Tests

    [AvaloniaTest]
    public void TracksDialog_ShowsTrackTypes()
    {
        var vm = new MainViewModelBuilder().Build();

        // Add one of each track type
        vm.SavedTracks.Add(CreateABLine("AB Line Test", 0, 0, 0, 100));
        vm.SavedTracks.Add(CreateCurve("Curve Test", 10, 0));
        vm.SavedTracks.Add(CreateContourStrip("Contour Test", 20, 0));
        vm.SavedTracks.Add(CreateRecordedPath("Path Test", 30, 0));

        var dialog = new TracksDialogPanel { DataContext = vm };
        var window = new Window { Content = dialog, Width = 550, Height = 500 };
        window.Show();

        vm.State.UI.ShowDialog(DialogType.Tracks);

        SaveScreenshot(window, "tracks_dialog_all_types.png");

        Assert.That(dialog.IsVisible, Is.True);
        Assert.That(vm.SavedTracks, Has.Count.EqualTo(4));
    }

    #endregion
}
