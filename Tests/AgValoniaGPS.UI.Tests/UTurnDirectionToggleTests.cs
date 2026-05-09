// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Tests for the U-turn direction toggle command, which mirrors the legacy
/// FormGPS.SwapDirection behavior (AgOpen_Snapshot/GPS/Forms/GUI.Designer.cs:1426).
/// </summary>
[TestFixture]
public class UTurnDirectionToggleTests
{
    [Test]
    public void Toggle_WhenArmedAndNotExecuting_FlipsIsTurnLeft()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsExecuting = false;
        vm.State.YouTurn.IsTurnLeft = false;

        vm.ToggleUTurnDirectionCommand!.Execute(null);

        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.True, "Should flip to left when armed.");
    }

    [Test]
    public void Toggle_WhenArmedTwice_FlipsBackAndForth()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsExecuting = false;
        vm.State.YouTurn.IsTurnLeft = true;

        vm.ToggleUTurnDirectionCommand!.Execute(null);
        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.False);

        vm.ToggleUTurnDirectionCommand!.Execute(null);
        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.True);
    }

    [Test]
    public void Toggle_WhileExecuting_DoesNotFlip()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsExecuting = true;
        vm.State.YouTurn.IsTurnLeft = false;

        vm.ToggleUTurnDirectionCommand!.Execute(null);

        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.False, "Must not flip while executing.");
        Assert.That(vm.StatusMessage, Does.Contain("executing"));
    }

    [Test]
    public void Toggle_WhenIdle_FlipsNextDirectionOverride()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsTriggered = false;
        vm.State.YouTurn.IsExecuting = false;
        vm.NextUTurnDirectionLeftOverride = false;

        vm.ToggleUTurnDirectionCommand!.Execute(null);

        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.True);
    }

    [Test]
    public void Toggle_WhenIdle_DoesNotTouchYouTurnState()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsTriggered = false;
        vm.State.YouTurn.IsExecuting = false;
        vm.State.YouTurn.IsTurnLeft = false;

        vm.ToggleUTurnDirectionCommand!.Execute(null);

        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.False, "Must not modify YouTurnState when idle.");
    }
}
