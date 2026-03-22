using Avalonia.Controls;
using Avalonia.Threading;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Views.Controls.Dialogs;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Headless rendering tests for AppDirectoriesDialogPanel.
/// Verifies the dialog opens, shows directory entries, and closes correctly.
/// </summary>
[TestFixture]
public class AppDirectoriesDialogTests
{
    [AvaloniaTest]
    public void Dialog_NotVisible_ByDefault()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new AppDirectoriesDialogPanel { DataContext = vm };

        Assert.That(dialog.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void Dialog_BecomesVisible_WhenDialogTypeSet()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new AppDirectoriesDialogPanel { DataContext = vm };

        // Put in a window so bindings evaluate
        var window = new Window { Content = dialog };
        window.Show();

        vm.State.UI.ShowDialog(DialogType.AppDirectories);

        Assert.That(dialog.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public void ShowAppDirectoriesCommand_OpensDialog()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.ShowAppDirectoriesDialogCommand!.Execute(null);

        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.AppDirectories));
        Assert.That(vm.State.UI.IsAppDirectoriesDialogVisible, Is.True);
    }

    [AvaloniaTest]
    public void CloseAppDirectoriesCommand_ClosesDialog()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.ShowAppDirectoriesDialogCommand!.Execute(null);

        vm.CloseAppDirectoriesDialogCommand!.Execute(null);

        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.None));
    }

    [AvaloniaTest]
    public void ShowCommand_PopulatesAppDirectories()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.ShowAppDirectoriesDialogCommand!.Execute(null);

        Assert.That(vm.AppDirectories, Has.Count.EqualTo(4),
            "Should show Settings, Fields, Vehicles, NTRIP paths");
        Assert.That(vm.AppDirectories.Select(d => d.Name),
            Is.EquivalentTo(new[] { "Settings", "Fields", "Vehicle Profiles", "NTRIP Profiles" }));
    }
}
