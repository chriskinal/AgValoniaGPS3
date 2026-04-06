// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models.Timing;

namespace AgValoniaGPS.Models.Tests.Timing;

[TestFixture]
public class ClockTests
{
    [TearDown]
    public void TearDown()
    {
        Clock.Reset(); // Always restore system clock after tests
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
        var clock = new TestClock(new DateTime(2026, 1, 1, 12, 0, 0));

        clock.AdvanceMs(500);

        Assert.That(clock.Now.Millisecond, Is.EqualTo(500));
    }

    [Test]
    public void TestClock_AdvanceSeconds_MovesTimeForward()
    {
        var clock = new TestClock(new DateTime(2026, 1, 1, 12, 0, 0));

        clock.AdvanceSeconds(30);

        Assert.That(clock.Now.Second, Is.EqualTo(30));
    }

    [Test]
    public void TestClock_Advance_Accumulates()
    {
        var clock = new TestClock(new DateTime(2026, 1, 1, 12, 0, 0));

        clock.AdvanceSeconds(10);
        clock.AdvanceSeconds(20);

        Assert.That(clock.Now, Is.EqualTo(new DateTime(2026, 1, 1, 12, 0, 30)));
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
    public void TestClock_ElapsedMs_Correct()
    {
        var clock = new TestClock();
        long t1 = clock.GetTimestamp();
        clock.AdvanceMs(250);
        long t2 = clock.GetTimestamp();

        double ms = clock.ElapsedMs(t1, t2);
        Assert.That(ms, Is.EqualTo(250.0).Within(0.1));
    }

    [Test]
    public void TestClock_SetTime_JumpsToTime()
    {
        var clock = new TestClock(new DateTime(2026, 1, 1, 12, 0, 0));
        var newTime = new DateTime(2026, 6, 15, 18, 30, 0);

        clock.SetTime(newTime);

        Assert.That(clock.Now, Is.EqualTo(newTime));
    }

    [Test]
    public void Clock_Static_DefaultsToSystemClock()
    {
        Clock.Reset();
        Assert.That(Clock.Current, Is.InstanceOf<SystemClock>());
    }

    [Test]
    public void Clock_Static_CanBeSwapped()
    {
        var testClock = new TestClock(new DateTime(2026, 3, 1, 10, 0, 0));
        Clock.Set(testClock);

        Assert.That(Clock.Current.Now, Is.EqualTo(new DateTime(2026, 3, 1, 10, 0, 0)));
    }

    [Test]
    public void Clock_Static_ResetRestoresSystemClock()
    {
        Clock.Set(new TestClock());
        Clock.Reset();

        Assert.That(Clock.Current, Is.InstanceOf<SystemClock>());
    }

    [Test]
    public void TestClock_TimeDoesNotAdvanceAutomatically()
    {
        var clock = new TestClock(new DateTime(2026, 1, 1, 12, 0, 0));

        var t1 = clock.Now;
        // No Advance call
        var t2 = clock.Now;

        Assert.That(t1, Is.EqualTo(t2));
    }
}
