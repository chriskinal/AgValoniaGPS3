// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.ViewModels.Tests;

/// <summary>
/// Integration tests for the two arrow-direction bugs the operator hit
/// on bench-test in v14.x — see PR thread under
/// <c>fix/uturn-arrow-direction-v2</c>.
///
/// Bug A: <c>MainViewModel.ToggleUTurnDirection</c> used to mutate
/// <c>State.YouTurn.IsTurnLeft</c> directly inside its <c>IsTriggered</c>
/// branch. The next cycle's <see cref="MainViewModel.ApplyGpsCycleResult"/>
/// mirror re-wrote from the unchanged working state, so the visual flip
/// lasted ~1 cycle then reverted. Operator could catch the transient
/// arrow-vs-rendered-path disagreement.
///
/// Bug B: the UI's <c>NextUTurnDirectionLeftOverride</c> cache used to
/// be a non-nullable <c>bool</c>; the working state's same-named field
/// is <c>bool?</c>. The state machine consumed and cleared the working
/// state value; <c>GpsPipelineService.ProcessCycle</c> at lines 479-480
/// re-wrote the UI's cache (always non-null) into the working state
/// next cycle. After one armed turn consumed the override, the UI's
/// stale value persisted into the next turn and biased its direction.
/// </summary>
[TestFixture]
public class YouTurnDirectionToggleTests
{
    // ── Bug A: mid-arm toggle must not directly mutate IsTurnLeft ─────

    [Test]
    public void ToggleUTurnDirection_WhileArmed_PostsOverrideIntent_DoesNotMutateState()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Arm a turn: simulate the state machine having armed a right turn
        // (IsTriggered=true, IsTurnLeft=false). The arrow widget renders
        // ↱ (right). The pipeline-emitted snapshot is the only source of
        // truth for State.YouTurn.IsTurnLeft.
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsTurnLeft = false;

        // Pre-toggle: nothing has been posted to the override.
        builder.GpsPipelineService.ClearReceivedCalls();

        vm.ToggleUTurnDirection();

        // Bug A repro path: previously this directly flipped
        // State.YouTurn.IsTurnLeft. Post-fix it must NOT — that
        // direct mutation produced the transient arrow-vs-path
        // disagreement the operator reported.
        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.False,
            "ToggleUTurnDirection while armed must NOT directly mutate "
            + "State.YouTurn.IsTurnLeft. The next cycle's snapshot mirror "
            + "is the sole writer; direct mutation lives one cycle then "
            + "reverts, producing the operator-reported arrow flicker.");

        // Instead, the toggle posts the desired direction through the
        // override path. The state machine's path-drop block will pick
        // it up next cycle and recreate the rendered path bending left.
        builder.GpsPipelineService
            .Received(1)
            .SetNextUTurnDirectionLeftOverride(Arg.Is<bool?>(v => v == true));
    }

    [Test]
    public void ToggleUTurnDirection_WhileArmedLeft_PostsRightOverride()
    {
        // Mirror case: armed left, toggle should post right (false).
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();
        vm.State.YouTurn.IsTriggered = true;
        vm.State.YouTurn.IsTurnLeft = true;
        builder.GpsPipelineService.ClearReceivedCalls();

        vm.ToggleUTurnDirection();

        Assert.That(vm.State.YouTurn.IsTurnLeft, Is.True,
            "State.YouTurn.IsTurnLeft must not be directly mutated.");
        builder.GpsPipelineService
            .Received(1)
            .SetNextUTurnDirectionLeftOverride(Arg.Is<bool?>(v => v == false));
    }

    [Test]
    public void ToggleUTurnDirection_WhileExecuting_IsNoOp()
    {
        // Mid-arc flips are unsafe — the controller is actively chasing
        // a goal on the current path. Toggle must be silently ignored
        // (status message updated, no override posted).
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();
        vm.State.YouTurn.IsExecuting = true;
        builder.GpsPipelineService.ClearReceivedCalls();

        vm.ToggleUTurnDirection();

        Assert.That(vm.StatusMessage, Does.Contain("executing"));
        builder.GpsPipelineService
            .DidNotReceiveWithAnyArgs()
            .SetNextUTurnDirectionLeftOverride(default);
    }

    // ── Bug B: UI override cache is one-shot, cleared by snapshot ─────

    [Test]
    public void OverrideCache_StartsNull_NoPreference()
    {
        var vm = new MainViewModelBuilder().Build();
        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.Null,
            "Fresh VM has no operator preference. The pipeline gate at "
            + "GpsPipelineService.ProcessCycle requires HasValue, so null "
            + "means the auto-arm decides direction.");
    }

    [Test]
    public void OverrideCache_ClearedWhenSnapshotReportsConsumed()
    {
        // Simulate the consumption sequence:
        //   1. User taps toggle while idle → UI sets override=true,
        //      pipeline.SetNextUTurnDirectionLeftOverride(true) called.
        //   2. State machine arms a turn next cycle, consumes the
        //      override, clears working state to null.
        //   3. ApplyGpsCycleResult observes snapshot.NextUTurnDirection
        //      LeftOverride == null and must clear the UI cache too.
        //      Otherwise the same UI value gets re-written into the
        //      working state every cycle and biases the next auto-arm.
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        vm.NextUTurnDirectionLeftOverride = true;
        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.True,
            "Setter mirrors the value into the UI cache.");
        builder.GpsPipelineService
            .Received(1)
            .SetNextUTurnDirectionLeftOverride(Arg.Is<bool?>(v => v == true));
        builder.GpsPipelineService.ClearReceivedCalls();

        // Cycle result with snapshot.NextUTurnDirectionLeftOverride=null,
        // simulating "state machine consumed the override this cycle".
        var result = new GpsCycleResult
        {
            YouTurn = new YouTurnSnapshot
            {
                NextUTurnDirectionLeftOverride = null,
            },
        };
        vm.ApplyGpsCycleResult(result);

        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.Null,
            "Snapshot reported the working state's override as null "
            + "(consumed). UI cache must follow so the pipeline's "
            + "cycle-overwrite stops re-writing the stale value.");
        // The clear path goes through the property setter, which calls
        // the pipeline with null. That's the wire that ensures the
        // pipeline cache also drops to null.
        builder.GpsPipelineService
            .Received(1)
            .SetNextUTurnDirectionLeftOverride(Arg.Is<bool?>(v => v == null));
    }

    [Test]
    public void OverrideCache_StaysSet_WhileWorkingStateStillHoldsValue()
    {
        // Negative case: if the snapshot still reports non-null
        // (override set but not yet consumed because the tractor isn't
        // in turn-creation range), the UI cache must NOT be cleared.
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        vm.NextUTurnDirectionLeftOverride = true;
        builder.GpsPipelineService.ClearReceivedCalls();

        var result = new GpsCycleResult
        {
            YouTurn = new YouTurnSnapshot
            {
                NextUTurnDirectionLeftOverride = true, // still pending
            },
        };
        vm.ApplyGpsCycleResult(result);

        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.True,
            "Working state still has the override pending consumption — "
            + "UI cache must not clear or the pipeline would lose the "
            + "operator's intent before the state machine can apply it.");
        builder.GpsPipelineService
            .DidNotReceiveWithAnyArgs()
            .SetNextUTurnDirectionLeftOverride(default);
    }

    // ── Combined: two armed turns back-to-back, no stuck override ────

    [Test]
    public void TwoArmedTurnsInSequence_SecondTurnDirection_NotBiasedByFirst()
    {
        // The stuck-override scenario from the operator's bench-test:
        // 1. Operator sets override=true.
        // 2. State machine arms turn 1 (goes left because of override).
        // 3. Turn completes. Working state clears override.
        // 4. Snapshot mirrors the cleared override.
        // 5. UI cache (under fix) drops to null.
        // 6. Tractor reaches a second auto-arm window. Working state
        //    starts that cycle with override=null because step 5 stopped
        //    the pipeline's cycle-overwrite. State machine arms turn 2
        //    using the natural geometric direction, NOT the stale left.
        //
        // Without the fix, step 5 stayed at true, the pipeline kept
        // overwriting working state with true every cycle, and turn 2
        // arrived as left even though geometry says right.
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Step 1-2: operator sets override, state machine consumes it.
        vm.NextUTurnDirectionLeftOverride = true;
        // Discard the initial setter's call so the no-op-between-turns
        // assertion below tracks only post-arm activity.
        builder.GpsPipelineService.ClearReceivedCalls();
        var armingResult = new GpsCycleResult
        {
            YouTurn = new YouTurnSnapshot
            {
                IsTriggered = true,
                IsTurnLeft = true, // consumed override, arms left
                NextUTurnDirectionLeftOverride = null, // cleared in-cycle
            },
        };
        vm.ApplyGpsCycleResult(armingResult);

        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.Null,
            "After turn 1 arming, UI cache must drop to null so the "
            + "next auto-arm doesn't see a stale operator-preference.");

        // Step 3-4: turn completes. State.YouTurn.IsTriggered flips
        // false, working state's override stays null.
        var completedResult = new GpsCycleResult
        {
            YouTurn = new YouTurnSnapshot
            {
                IsTriggered = false,
                IsExecuting = false,
                NextUTurnDirectionLeftOverride = null,
                JustCompleted = true,
            },
        };
        vm.ApplyGpsCycleResult(completedResult);

        Assert.That(vm.NextUTurnDirectionLeftOverride, Is.Null,
            "Between turns 1 and 2 with no operator input, the UI "
            + "cache must remain null. The stuck-override bug had this "
            + "stay at the consumed value, biasing the next turn.");

        // Step 6: a second auto-arm cycle. Natural geometry → right.
        // No further pipeline.SetNextUTurnDirectionLeftOverride calls
        // should have been made between turns — the UI didn't post
        // anything new. Verify that's the case.
        builder.GpsPipelineService
            .DidNotReceive()
            .SetNextUTurnDirectionLeftOverride(Arg.Is<bool?>(v => v == true));
    }
}
