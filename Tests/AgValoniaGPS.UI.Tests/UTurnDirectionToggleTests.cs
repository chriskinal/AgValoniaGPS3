// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Tests for the U-turn direction toggle command, which mirrors the legacy
/// FormGPS.SwapDirection behavior (AgOpen_Snapshot/GPS/Forms/GUI.Designer.cs:1426).
/// </summary>
[TestFixture]
public class UTurnDirectionToggleTests
{
    // Bug-A fix: ToggleUTurnDirection no longer flips State.YouTurn.IsTurnLeft
    // directly when armed. It posts the override; the state machine's path-drop
    // block reads it on the next cycle, drops the rendered path, and CREATE
    // re-bends with the new direction. The visible flip happens through the
    // snapshot mirror after one pipeline cycle, not via direct VM mutation.
    // Detailed coverage of the new contract lives in
    // Tests/AgValoniaGPS.ViewModels.Tests/YouTurnDirectionToggleTests.

    [Test]
    public void Toggle_WhenArmedAndNotExecuting_DoesNotDirectlyMutateIsTurnLeft()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsExecuting = false;
        vm.State.YouTurn.IsTurnLeft = false;

        vm.ToggleUTurnDirectionCommand!.Execute(null);

        // No transient flip — State.YouTurn.IsTurnLeft is unchanged until the
        // next snapshot mirror lands. Override is what carries the request.
        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.False,
            "Bug A fix: armed toggle must NOT mutate State.YouTurn.IsTurnLeft directly.");
        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.True,
            "Armed toggle must post the override so the state machine re-bends on the next cycle.");
    }

    [Test]
    public void Toggle_WhenArmedTwice_AlternatesOverride()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsExecuting = false;
        vm.State.YouTurn.IsTurnLeft = true;

        vm.ToggleUTurnDirectionCommand!.Execute(null);
        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.False,
            "First armed toggle: opposite-of-current posted as override.");

        // Simulate the pipeline cycle that would consume the override and
        // mirror the new direction onto State.YouTurn.IsTurnLeft. Without
        // this, the toggle still reads the original IsTurnLeft and posts
        // the same override value (real flow alternates because the
        // snapshot mirror updates State between operator taps).
        vm.State.YouTurn.IsTurnLeft = false;
        vm.NextUTurnDirectionLeftOverride = null;

        vm.ToggleUTurnDirectionCommand!.Execute(null);
        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.True,
            "Second armed toggle: opposite of the newly-mirrored direction.");
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

    // ── IsUTurnDistanceVisible ─────────────────────────────────────────────
    // The distance widget must appear during approach (not just during execution).
    // See fix/uturn-distance-counter — the original af7d223b widget gated visibility
    // on State.YouTurn.IsTriggered, which only flips true at the moment of execution.

    [Test]
    public void IsUTurnDistanceVisible_WhenYouTurnDisabled_ReturnsFalse()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsEnabled = false;
        vm.State.YouTurn.DistanceToTrigger = 25.0;
        vm.State.YouTurn.IsTriggered = false;
        vm.State.YouTurn.IsExecuting = false;

        Assert.That(vm.IsUTurnDistanceVisible, Is.False,
            "Widget must be hidden when YouTurn feature is off.");
    }

    [Test]
    public void IsUTurnDistanceVisible_WhenEnabledAndApproaching_ReturnsTrue()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsEnabled = true;
        vm.State.YouTurn.DistanceToTrigger = 25.0;
        vm.State.YouTurn.IsTriggered = false;
        vm.State.YouTurn.IsExecuting = false;

        Assert.That(vm.IsUTurnDistanceVisible, Is.True,
            "Widget must show during approach when DistanceToTrigger > 0.");
    }

    [Test]
    public void IsUTurnDistanceVisible_WhenEnabledAndDistanceZero_ReturnsFalse()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsEnabled = true;
        vm.State.YouTurn.DistanceToTrigger = 0.0;
        vm.State.YouTurn.IsTriggered = false;
        vm.State.YouTurn.IsExecuting = false;

        Assert.That(vm.IsUTurnDistanceVisible, Is.False,
            "No upcoming turn => no widget. Pipeline emits 0 when no turn is being approached.");
    }

    [Test]
    public void IsUTurnDistanceVisible_WhenTriggered_ReturnsTrue()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsEnabled = true;
        vm.State.YouTurn.DistanceToTrigger = 0.0;
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsExecuting = false;

        Assert.That(vm.IsUTurnDistanceVisible, Is.True,
            "Widget must remain visible the moment the turn arms.");
    }

    [Test]
    public void IsUTurnDistanceVisible_WhenExecuting_ReturnsTrue()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsEnabled = true;
        vm.State.YouTurn.DistanceToTrigger = 0.0;
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsExecuting = true;

        Assert.That(vm.IsUTurnDistanceVisible, Is.True,
            "Widget must remain visible through turn execution.");
    }

    /// <summary>
    /// Regression for the v4 distance-widget-not-showing bug. The cycle's
    /// <see cref="YouTurnSnapshot"/> already populates DistanceToTrigger, but
    /// the widget's <see cref="MainViewModel.IsUTurnDistanceVisible"/> predicate
    /// gates on <c>State.YouTurn.IsEnabled</c>. Both fields must be mirrored
    /// from the snapshot onto <c>State.YouTurn</c> by ApplyGpsCycleResult so
    /// the widget actually shows during approach.
    /// </summary>
    [Test]
    public void ApplyGpsCycleResult_MirrorsSnapshotDistanceAndIsEnabled_OntoStateYouTurn()
    {
        var vm = new MainViewModelBuilder().Build();

        var result = new GpsCycleResult
        {
            YouTurn = new YouTurnSnapshot
            {
                IsEnabled = true,
                IsTriggered = false,
                IsExecuting = false,
                DistanceToTrigger = 27.5,
            },
        };

        vm.ApplyGpsCycleResult(result);

        Assert.Multiple(() =>
        {
            Assert.That(vm.State.YouTurn.IsEnabled, Is.True,
                "Snapshot.IsEnabled must reach State.YouTurn.IsEnabled");
            Assert.That(vm.State.YouTurn.DistanceToTrigger, Is.EqualTo(27.5).Within(0.001),
                "Snapshot.DistanceToTrigger must reach State.YouTurn.DistanceToTrigger");
            Assert.That(vm.IsUTurnDistanceVisible, Is.True,
                "Widget must be visible once both fields are mirrored");
        });
    }

    [Test]
    public void IsUTurnDistanceVisible_RaisesPropertyChanged_WhenDistanceToTriggerChanges()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.State.YouTurn.IsEnabled = true;
        vm.State.YouTurn.DistanceToTrigger = 0.0;

        bool raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsUTurnDistanceVisible)) raised = true;
        };

        vm.State.YouTurn.DistanceToTrigger = 30.0;

        Assert.That(raised, Is.True,
            "IsUTurnDistanceVisible must re-raise when its inputs change so AXAML refreshes.");
    }
}
