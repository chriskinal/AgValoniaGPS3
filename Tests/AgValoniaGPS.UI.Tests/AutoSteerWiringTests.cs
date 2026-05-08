using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Regression fence for the autosteer engage/disengage wire-up between
/// MainViewModel and IAutoSteerService. The bug previously was: the VM
/// flipped its own IsAutoSteerEngaged property and notified the pipeline,
/// but never called Engage()/Disengage() on AutoSteerService — which is
/// what flips the state PgnBuilder reads when assembling PGN 254. As a
/// result the engaged status bit on the wire stayed 0 and external steer
/// modules never saw engagement. The in-app simulator wasn't affected
/// because it bypasses the wire and reads VM/pipeline state directly.
/// </summary>
[TestFixture]
public class AutoSteerWiringTests
{
    [Test]
    public void ToggleAutoSteerCommand_WhenEngaged_CallsDisengageOnService()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Disengage path has no preconditions ("the user must be able to
        // stop the tractor even after the track/field has been cleared").
        vm.IsAutoSteerEngaged = true;
        builder.AutoSteerService.ClearReceivedCalls();

        vm.ToggleAutoSteerCommand!.Execute(null);

        Assert.That(vm.IsAutoSteerEngaged, Is.False);
        builder.AutoSteerService.Received(1).Disengage();
        builder.AutoSteerService.DidNotReceive().Engage();
    }

    [Test]
    public void ApplyGpsCycleResult_WithAutoSteerDisengagedThisCycle_CallsDisengageOnService()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        vm.IsAutoSteerEngaged = true;
        builder.AutoSteerService.ClearReceivedCalls();

        vm.ApplyGpsCycleResult(new GpsCycleResult
        {
            AutoSteerDisengagedThisCycle = true,
            DisengageReason = "test boundary kickout"
        });

        Assert.That(vm.IsAutoSteerEngaged, Is.False);
        builder.AutoSteerService.Received(1).Disengage();
    }

    [Test]
    public async Task CloseFieldAsync_WhileEngaged_CallsDisengageOnService()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        vm.IsAutoSteerEngaged = true;
        builder.AutoSteerService.ClearReceivedCalls();

        await vm.CloseFieldAsync();

        Assert.That(vm.IsAutoSteerEngaged, Is.False);
        builder.AutoSteerService.Received(1).Disengage();
    }
}
