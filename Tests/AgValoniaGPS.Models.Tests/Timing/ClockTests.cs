// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models.Timing;

namespace AgValoniaGPS.Models.Tests.Timing;

[TestFixture]
public class ClockTests
{
    [TearDown]
    public void TearDown()
    {
        Clock.Reset();
    }

    [Test]
    public void SystemClock_ReturnsCurrentTime()
    {
        var clock = SystemClock.Instance;
        var before = DateTime.Now;
        var clockNow = clock.Now;
        var after = DateTime.Now;

        Assert.That(clockNow, Is.GreaterThanOrEqualTo(before));
        Assert.That(clockNow, Is.LessThanOrEqualTo(after));
    }

    [Test]
    public void SystemClock_GetTimestamp_Increases()
    {
        var clock = SystemClock.Instance;
        long t1 = clock.GetTimestamp();
        long t2 = clock.GetTimestamp();
        Assert.That(t2, Is.GreaterThanOrEqualTo(t1));
    }

    [Test]
    public void TestClock_StartsAtSpecifiedTime()
    {
        var startTime = new DateTime(2026, 1, 1, 8, 0, 0);
        var clock = new TestClock(startTime);

        Assert.That(clock.Now, Is.EqualTo(startTime));
    }

    [Test]
    public void TestClock_AdvanceMs_MovesTimeForward()
    {
        var clock = new TestClock();
        var initial = clock.Now;

        clock.AdvanceMs(500);

        Assert.That(clock.Now, Is.EqualTo(initial.AddMilliseconds(500)));
    }

    [Test]
    public void TestClock_AdvanceSeconds_MovesTimeForward()
    {
        var clock = new TestClock();
        var initial = clock.Now;

        clock.AdvanceSeconds(3.5);

        Assert.That(clock.Now, Is.EqualTo(initial.AddSeconds(3.5)));
    }

    [Test]
    public void TestClock_SetTime_JumpsToNewTime()
    {
        var clock = new TestClock();
        var newTime = new DateTime(2030, 12, 25, 0, 0, 0);

        clock.SetTime(newTime);

        Assert.That(clock.Now, Is.EqualTo(newTime));
    }

    [Test]
    public void TestClock_GetTimestamp_AdvancesWithTime()
    {
        var clock = new TestClock();
        long t1 = clock.GetTimestamp();

        clock.AdvanceSeconds(1.0);
        long t2 = clock.GetTimestamp();

        double elapsed = clock.ElapsedSeconds(t1, t2);
        Assert.That(elapsed, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void TestClock_ElapsedMs_ReturnsCorrectValue()
    {
        var clock = new TestClock();
        long t1 = clock.GetTimestamp();

        clock.AdvanceMs(250);
        long t2 = clock.GetTimestamp();

        double ms = clock.ElapsedMs(t1, t2);
        Assert.That(ms, Is.EqualTo(250.0).Within(0.1));
    }

    [Test]
    public void Clock_Static_DefaultsToSystemClock()
    {
        Assert.That(Clock.Current, Is.InstanceOf<SystemClock>());
    }

    [Test]
    public void Clock_Set_SwapsInstance()
    {
        var testClock = new TestClock();
        Clock.Set(testClock);

        Assert.That(Clock.Current, Is.SameAs(testClock));
    }

    [Test]
    public void Clock_Reset_RestoresSystemClock()
    {
        Clock.Set(new TestClock());
        Clock.Reset();

        Assert.That(Clock.Current, Is.InstanceOf<SystemClock>());
    }

    [Test]
    public void SystemClock_TimeScale_DefaultsToOne()
    {
        Assert.That(SystemClock.Instance.TimeScale, Is.EqualTo(1.0));
    }

    [Test]
    public void TestClock_TimeScale_IsSettable()
    {
        var clock = new TestClock { TimeScale = 10.0 };
        Assert.That(clock.TimeScale, Is.EqualTo(10.0));
    }
}
