using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Tests for track recording commands (#45).
/// </summary>
[TestFixture]
public class TrackRecordingTests
{
    [Test]
    public void StartNewABLine_SetsCreationMode()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.StartNewABLineCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Drive-in AB Line"));
    }

    [Test]
    public void StartNewABCurve_StartsCurveRecording()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.StartNewABCurveCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Curve recording started"));
    }
}
