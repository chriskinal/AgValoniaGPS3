// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using AgValoniaGPS.Services.Pipeline;

namespace AgValoniaGPS.Services.Tests.Pipeline;

[TestFixture]
public class ManualSteerMachineLoopTests
{
    [Test]
    public void Tick_WhenNotRunning_DoesNotFire()
    {
        var loop = new ManualSteerMachineLoop();
        int count = 0;
        loop.Ticked += _ => count++;

        loop.Tick(123);

        Assert.That(count, Is.EqualTo(0));
        Assert.That(loop.IsRunning, Is.False);
    }

    [Test]
    public void Tick_WhenRunning_FiresWithTimestamp()
    {
        var loop = new ManualSteerMachineLoop();
        long observedTs = -1;
        loop.Ticked += ts => observedTs = ts;
        loop.Start();

        loop.Tick(42);

        Assert.That(observedTs, Is.EqualTo(42));
    }

    [Test]
    public void Tick_AfterStop_DoesNotFire()
    {
        var loop = new ManualSteerMachineLoop();
        int count = 0;
        loop.Ticked += _ => count++;
        loop.Start();
        loop.Tick(1);
        loop.Stop();

        loop.Tick(2);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(loop.IsRunning, Is.False);
    }

    [Test]
    public void Start_IsIdempotent()
    {
        var loop = new ManualSteerMachineLoop();
        loop.Start();
        loop.Start();

        Assert.That(loop.IsRunning, Is.True);
    }

    [Test]
    public void DefaultFrequency_Is100Hz()
    {
        var loop = new ManualSteerMachineLoop();
        Assert.That(loop.FrequencyHz, Is.EqualTo(100.0));
    }
}

[TestFixture]
public class SteerMachineLoopServiceTests
{
    [Test]
    public void Constructor_RejectsNonPositiveFrequency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SteerMachineLoopService(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SteerMachineLoopService(-50));
    }

    [Test]
    public void Start_BeginsTickingAtConfiguredRate()
    {
        // Use 50 Hz for a faster test (20 ms period). Run for ~200 ms,
        // expect roughly 10 ticks (allow generous bounds for OS scheduling).
        using var loop = new SteerMachineLoopService(frequencyHz: 50);
        var timestamps = new List<long>();
        loop.Ticked += ts => { lock (timestamps) timestamps.Add(ts); }
;
        loop.Start();
        Thread.Sleep(220);
        loop.Stop();

        int count;
        lock (timestamps) count = timestamps.Count;
        Assert.That(count, Is.GreaterThanOrEqualTo(7),
            "Expected at least 7 ticks in 220 ms at 50 Hz");
        Assert.That(count, Is.LessThanOrEqualTo(15),
            "Expected at most 15 ticks in 220 ms at 50 Hz (large jitter cap)");
    }

    [Test]
    public void Start_IsIdempotent()
    {
        using var loop = new SteerMachineLoopService(frequencyHz: 50);
        loop.Start();
        loop.Start();
        Assert.That(loop.IsRunning, Is.True);
        loop.Stop();
    }

    [Test]
    public void Stop_StopsFiringTicks()
    {
        using var loop = new SteerMachineLoopService(frequencyHz: 100);
        int countAfterStop = 0;
        int countWhileRunning = 0;
        loop.Ticked += _ => Interlocked.Increment(ref countWhileRunning);
        loop.Start();
        Thread.Sleep(50);
        loop.Stop();

        // Switch the counter — anything firing after Stop is a bug.
        loop.Ticked += _ => Interlocked.Increment(ref countAfterStop);
        Thread.Sleep(50);

        Assert.That(loop.IsRunning, Is.False);
        Assert.That(countAfterStop, Is.EqualTo(0));
    }

    [Test]
    public void TickedTimestamps_AreMonotonicallyIncreasing()
    {
        using var loop = new SteerMachineLoopService(frequencyHz: 100);
        var timestamps = new List<long>();
        loop.Ticked += ts => { lock (timestamps) timestamps.Add(ts); };
        loop.Start();
        Thread.Sleep(100);
        loop.Stop();

        long[] copy;
        lock (timestamps) copy = timestamps.ToArray();
        for (int i = 1; i < copy.Length; i++)
        {
            Assert.That(copy[i], Is.GreaterThanOrEqualTo(copy[i - 1]),
                $"Tick {i} timestamp regressed from previous");
        }
    }

    [Test]
    public void Tick_ManualHookFiresEvent()
    {
        // Manual Tick should fire even on production impl — useful for
        // synthetic ticks at startup and for diagnostics.
        using var loop = new SteerMachineLoopService(frequencyHz: 100);
        long observed = -1;
        loop.Ticked += ts => observed = ts;

        loop.Tick(99);

        Assert.That(observed, Is.EqualTo(99));
    }

    [Test]
    public void Dispose_StopsLoop()
    {
        var loop = new SteerMachineLoopService(frequencyHz: 100);
        loop.Start();
        Thread.Sleep(20);
        loop.DisposeAsync().AsTask().Wait();

        Assert.That(loop.IsRunning, Is.False);
    }
}
