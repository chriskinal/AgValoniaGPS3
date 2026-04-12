using System.Windows.Input;

namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class CommandSmokeTests
{
    [Test]
    public void CriticalCommands_AreNotNull()
    {
        var vm = new MainViewModelBuilder().Build();

        var commands = new Dictionary<string, ICommand?>
        {
            [nameof(vm.CloseFieldCommand)] = vm.CloseFieldCommand,
            [nameof(vm.ZoomInCommand)] = vm.ZoomInCommand,
            [nameof(vm.ZoomOutCommand)] = vm.ZoomOutCommand,
            [nameof(vm.ToggleDayNightCommand)] = vm.ToggleDayNightCommand,
            [nameof(vm.ToggleGridCommand)] = vm.ToggleGridCommand,
            [nameof(vm.Toggle3DModeCommand)] = vm.Toggle3DModeCommand,
            [nameof(vm.ToggleSimulatorPanelCommand)] = vm.ToggleSimulatorPanelCommand,
            [nameof(vm.ToggleSectionMasterCommand)] = vm.ToggleSectionMasterCommand,
            [nameof(vm.ToggleSectionCommand)] = vm.ToggleSectionCommand,
            [nameof(vm.UTurnCommand)] = vm.UTurnCommand,
            [nameof(vm.StartBoundaryRecordingCommand)] = vm.StartBoundaryRecordingCommand,
            [nameof(vm.StopBoundaryRecordingCommand)] = vm.StopBoundaryRecordingCommand,
            [nameof(vm.ShowFieldSelectionDialogCommand)] = vm.ShowFieldSelectionDialogCommand,
            [nameof(vm.ShowConfigurationDialogCommand)] = vm.ShowConfigurationDialogCommand,
            [nameof(vm.ShowHeadlandBuilderCommand)] = vm.ShowHeadlandBuilderCommand,
            [nameof(vm.ToggleNorthUpCommand)] = vm.ToggleNorthUpCommand,
        };

        foreach (var (name, command) in commands)
        {
            Assert.That(command, Is.Not.Null, $"Command {name} should not be null after construction");
        }
    }

    [Test]
    public void SimpleToggleCommands_CanExecuteWithoutThrowing()
    {
        var vm = new MainViewModelBuilder().Build();

        // These are simple toggle commands that should not throw
        Assert.DoesNotThrow(() => vm.ToggleDayNightCommand!.Execute(null));
        Assert.DoesNotThrow(() => vm.ToggleGridCommand!.Execute(null));
        Assert.DoesNotThrow(() => vm.Toggle3DModeCommand!.Execute(null));
        Assert.DoesNotThrow(() => vm.ToggleNorthUpCommand!.Execute(null));
        Assert.DoesNotThrow(() => vm.ZoomInCommand!.Execute(null));
        Assert.DoesNotThrow(() => vm.ZoomOutCommand!.Execute(null));
        Assert.DoesNotThrow(() => vm.ToggleSimulatorPanelCommand!.Execute(null));
    }

    [Test]
    public void CloseFieldCommand_CanExecuteWithoutThrowing()
    {
        var vm = new MainViewModelBuilder().Build();

        // CloseField with no field open should be safe
        Assert.DoesNotThrow(() => vm.CloseFieldCommand!.Execute(null));
    }
}
