using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Views.Controls.Panels;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Headless UI tests for BottomNavigationPanel buttons.
/// Renders the actual panel in a headless window and verifies
/// button bindings, visibility, and command execution through the UI.
/// </summary>
[TestFixture]
public class BottomPanelHeadlessTests
{
    private static Track CreateTestTrack() => new()
    {
        Name = "Test AB",
        Type = TrackType.ABLine,
        Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) },
        IsVisible = true,
        IsActive = true,
        NudgeDistance = 0
    };

    private static Button? FindButtonByTooltip(Window window, string tooltip)
    {
        return window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => ToolTip.GetTip(b)?.ToString() == tooltip);
    }

    [AvaloniaTest]
    public void Panel_RendersWithoutCrash()
    {
        var vm = new MainViewModelBuilder().Build();
        var panel = new BottomNavigationPanel { DataContext = vm };

        var window = new Window { Content = panel };
        window.Show();

        Assert.That(panel, Is.Not.Null);
    }

    [AvaloniaTest]
    public void SnapButtons_HiddenWhenNoTrack()
    {
        var vm = new MainViewModelBuilder().Build();
        var panel = new BottomNavigationPanel { DataContext = vm };

        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var snapLeft = FindButtonByTooltip(window, "Snap to Left Track");
        var snapRight = FindButtonByTooltip(window, "Snap to Right Track");
        var snapPivot = FindButtonByTooltip(window, "Snap to Pivot");

        // Buttons exist but should not be visible without active track
        Assert.That(snapLeft, Is.Not.Null, "Snap Left button should exist in visual tree");
        Assert.That(snapLeft!.IsVisible, Is.False, "Snap Left should be hidden when no track");
        Assert.That(snapRight!.IsVisible, Is.False, "Snap Right should be hidden when no track");
        Assert.That(snapPivot!.IsVisible, Is.False, "Snap Pivot should be hidden when no track");
    }

    [AvaloniaTest]
    public void SnapButtons_VisibleWhenTrackActive()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var snapLeft = FindButtonByTooltip(window, "Snap to Left Track");
        var snapRight = FindButtonByTooltip(window, "Snap to Right Track");
        var snapPivot = FindButtonByTooltip(window, "Snap to Pivot");

        Assert.That(snapLeft!.IsVisible, Is.True, "Snap Left should be visible with active track");
        Assert.That(snapRight!.IsVisible, Is.True, "Snap Right should be visible with active track");
        Assert.That(snapPivot!.IsVisible, Is.True, "Snap Pivot should be visible with active track");
    }

    [AvaloniaTest]
    public void ResetToolHeadingButton_AlwaysVisible()
    {
        var vm = new MainViewModelBuilder().Build();
        var panel = new BottomNavigationPanel { DataContext = vm };

        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var resetBtn = FindButtonByTooltip(window, "Reset Tool Heading");

        Assert.That(resetBtn, Is.Not.Null, "Reset Tool Heading button should exist");
        Assert.That(resetBtn!.IsVisible, Is.True, "Reset Tool Heading should always be visible");
    }

    [AvaloniaTest]
    public void ResetToolHeadingButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.Heading = 45.0;
        vm.ToolHeadingRadians = 0.0;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var resetBtn = FindButtonByTooltip(window, "Reset Tool Heading");
        resetBtn!.Command!.Execute(null);

        double expectedRadians = 45.0 * Math.PI / 180.0;
        Assert.That(vm.ToolHeadingRadians, Is.EqualTo(expectedRadians).Within(0.001));
    }

    [AvaloniaTest]
    public void SnapLeftButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var snapLeft = FindButtonByTooltip(window, "Snap to Left Track");
        snapLeft!.Command!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Snapped left"));
    }

    [AvaloniaTest]
    public void SnapRightButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var snapRight = FindButtonByTooltip(window, "Snap to Right Track");
        snapRight!.Command!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Snapped right"));
    }

    [AvaloniaTest]
    public void SnapToPivotButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;
        vm.State.Guidance.CrossTrackError = 0.3;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var snapPivot = FindButtonByTooltip(window, "Snap to Pivot");
        snapPivot!.Command!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Nudged"));
    }

    [AvaloniaTest]
    public void NudgeLeftButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var nudgeLeft = FindButtonByTooltip(window, "Nudge Left (A+)");
        nudgeLeft!.Command!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Nudged left"));
    }

    [AvaloniaTest]
    public void NudgeRightButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var nudgeRight = FindButtonByTooltip(window, "Nudge Right (B+)");
        nudgeRight!.Command!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Nudged right"));
    }

    [AvaloniaTest]
    public void FineNudgeLeftButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var fineNudgeLeft = FindButtonByTooltip(window, "Fine Nudge Left (A-)");
        fineNudgeLeft!.Command!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Nudged left"));
    }

    [AvaloniaTest]
    public void FineNudgeRightButton_ExecutesCommand()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        var panel = new BottomNavigationPanel { DataContext = vm };
        var window = new Window { Content = panel, Width = 1200, Height = 200 };
        window.Show();

        var fineNudgeRight = FindButtonByTooltip(window, "Fine Nudge Right (B-)");
        fineNudgeRight!.Command!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Nudged right"));
    }
}
