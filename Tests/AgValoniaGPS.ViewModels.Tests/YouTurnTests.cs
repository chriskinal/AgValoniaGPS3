using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class YouTurnTests
{
    [Test]
    public void UTurnCommand_WithNoTrack_SetsErrorStatus()
    {
        var vm = new MainViewModelBuilder().Build();

        // No track selected
        Assert.That(vm.SelectedTrack, Is.Null);

        // Execute U-turn command
        vm.UTurnCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("No track"));
    }

    [Test]
    public void YouTurnState_ClearedWhenTrackDeselected()
    {
        var vm = new MainViewModelBuilder().Build();

        var track = new Track
        {
            Name = "AB1",
            Points = new List<Vec3>
            {
                new(0, 0, 0),
                new(0, 100, 0)
            }
        };

        // Select then deselect
        vm.SelectedTrack = track;
        vm.IsYouTurnEnabled = true;
        vm.SelectedTrack = null;

        // YouTurn should be cleared along with the track
        Assert.That(vm.HasActiveTrack, Is.False);
        Assert.That(vm.State.YouTurn.IsTriggered, Is.False);
        Assert.That(vm.State.YouTurn.IsExecuting, Is.False);
    }

    [Test]
    public void IsYouTurnEnabled_CanBeToggled()
    {
        var vm = new MainViewModelBuilder().Build();

        Assert.That(vm.IsYouTurnEnabled, Is.False);

        vm.IsYouTurnEnabled = true;
        Assert.That(vm.IsYouTurnEnabled, Is.True);

        vm.IsYouTurnEnabled = false;
        Assert.That(vm.IsYouTurnEnabled, Is.False);
    }
}
