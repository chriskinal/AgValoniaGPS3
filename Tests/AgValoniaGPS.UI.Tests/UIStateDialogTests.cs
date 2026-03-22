using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Pure model tests for dialog visibility state — no rendering needed.
/// </summary>
[TestFixture]
public class UIStateDialogTests
{
    private UIState _ui = null!;

    [SetUp]
    public void SetUp() => _ui = new UIState();

    [Test]
    public void ShowDialog_SetsActiveDialog()
    {
        _ui.ShowDialog(DialogType.AppDirectories);
        Assert.That(_ui.ActiveDialog, Is.EqualTo(DialogType.AppDirectories));
    }

    [Test]
    public void CloseDialog_ResetsToNone()
    {
        _ui.ShowDialog(DialogType.AppDirectories);
        _ui.CloseDialog();
        Assert.That(_ui.ActiveDialog, Is.EqualTo(DialogType.None));
    }

    [Test]
    public void IsAppDirectoriesDialogVisible_TrueWhenActive()
    {
        _ui.ShowDialog(DialogType.AppDirectories);
        Assert.That(_ui.IsAppDirectoriesDialogVisible, Is.True);
    }

    [Test]
    public void IsAppDirectoriesDialogVisible_FalseWhenOtherDialogOpen()
    {
        _ui.ShowDialog(DialogType.Confirmation);
        Assert.That(_ui.IsAppDirectoriesDialogVisible, Is.False);
    }

    [Test]
    public void ShowDialog_RaisesPropertyChangedForVisibility()
    {
        var changed = new List<string>();
        _ui.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        _ui.ShowDialog(DialogType.AppDirectories);

        Assert.That(changed, Contains.Item(nameof(UIState.IsAppDirectoriesDialogVisible)));
        Assert.That(changed, Contains.Item(nameof(UIState.IsDialogOpen)));
    }

    [Test]
    public void OnlyOneDialogVisibleAtATime()
    {
        _ui.ShowDialog(DialogType.AppDirectories);

        Assert.That(_ui.IsAppDirectoriesDialogVisible, Is.True);
        Assert.That(_ui.IsConfirmationDialogVisible, Is.False);
        Assert.That(_ui.IsNtripProfilesDialogVisible, Is.False);
    }

    [Test]
    public void IsDialogOpen_FalseWhenNone()
    {
        Assert.That(_ui.IsDialogOpen, Is.False);
    }

    [Test]
    public void IsDialogOpen_TrueWhenAnyDialogShown()
    {
        _ui.ShowDialog(DialogType.AppDirectories);
        Assert.That(_ui.IsDialogOpen, Is.True);
    }
}
