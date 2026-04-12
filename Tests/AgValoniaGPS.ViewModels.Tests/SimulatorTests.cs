namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class SimulatorTests
{
    [Test]
    public void IsSimulatorEnabled_CanBeToggled()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.IsSimulatorEnabled = true;
        Assert.That(vm.IsSimulatorEnabled, Is.True);

        vm.IsSimulatorEnabled = false;
        Assert.That(vm.IsSimulatorEnabled, Is.False);
    }

    [Test]
    public void ToggleSimulatorPanelCommand_TogglesPanelVisibility()
    {
        var vm = new MainViewModelBuilder().Build();

        bool initialVisible = vm.IsSimulatorPanelVisible;

        vm.ToggleSimulatorPanelCommand!.Execute(null);

        Assert.That(vm.IsSimulatorPanelVisible, Is.Not.EqualTo(initialVisible));
    }

    [Test]
    public void SimulatorSpeedKph_CanBeSet()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.SimulatorSpeedKph = 15.0;
        Assert.That(vm.SimulatorSpeedKph, Is.EqualTo(15.0));
    }
}
