// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.Timing;
using AgValoniaGPS.Services.Pipeline;

namespace AgValoniaGPS.Services.Tests.Pipeline;

[TestFixture]
public class PositionEstimatorTests
{
    [Test]
    public void GetPose_NoSnapshot_ReturnsDefault()
    {
        var estimator = new PositionEstimator();

        var pose = estimator.GetPose(Clock.Current.GetTimestamp());

        Assert.That(pose, Is.EqualTo(default(InterpolatedPose)));
    }

    [Test]
    public void GetLatestSnapshot_NoSnapshot_ReturnsNull()
    {
        var estimator = new PositionEstimator();

        Assert.That(estimator.GetLatestSnapshot(), Is.Null);
    }

    [Test]
    public void UpdateFromGps_RoundTrips_LatestSnapshot()
    {
        var estimator = new PositionEstimator();
        var snapshot = MakeSnapshot();

        estimator.UpdateFromGps(snapshot);

        Assert.That(estimator.GetLatestSnapshot(), Is.SameAs(snapshot));
    }

    [Test]
    public void GetPose_AtSnapshotTime_ReturnsSnapshotPose()
    {
        var estimator = new PositionEstimator();
        var snapshot = MakeSnapshot(
            position: new Vec2(100, 200),
            heading: 0.5,
            speedMps: 10,
            yawRate: 0.1,
            roll: 0.02);
        estimator.UpdateFromGps(snapshot);

        var pose = estimator.GetPose(snapshot.TimestampTicks);

        Assert.That(pose.Position.Easting, Is.EqualTo(100).Within(1e-9));
        Assert.That(pose.Position.Northing, Is.EqualTo(200).Within(1e-9));
        Assert.That(pose.Heading, Is.EqualTo(0.5).Within(1e-9));
        Assert.That(pose.SpeedMps, Is.EqualTo(10).Within(1e-9));
        Assert.That(pose.Roll, Is.EqualTo(0.02).Within(1e-9));
    }

    [Test]
    public void GetPose_StraightLineNorth_AdvancesPositionByVelocity()
    {
        // Heading=0 means due north (heading convention: 0=N, sin→E, cos→N).
        var estimator = new PositionEstimator();
        var t0 = Clock.Current.GetTimestamp();
        estimator.UpdateFromGps(MakeSnapshot(
            position: new Vec2(0, 0),
            heading: 0,
            speedMps: 10,
            yawRate: 0,
            timestampTicks: t0));

        var t1 = t0 + TicksFromSeconds(0.05); // 50 ms later
        var pose = estimator.GetPose(t1);

        // 10 m/s × 0.05 s = 0.5 m due north
        Assert.That(pose.Position.Easting, Is.EqualTo(0).Within(1e-9));
        Assert.That(pose.Position.Northing, Is.EqualTo(0.5).Within(1e-9));
        Assert.That(pose.Heading, Is.EqualTo(0).Within(1e-9));
    }

    [Test]
    public void GetPose_StraightLineEast_AdvancesPositionByVelocity()
    {
        // Heading=π/2 (90°) means due east.
        var estimator = new PositionEstimator();
        var t0 = Clock.Current.GetTimestamp();
        estimator.UpdateFromGps(MakeSnapshot(
            position: new Vec2(0, 0),
            heading: Math.PI / 2,
            speedMps: 10,
            yawRate: 0,
            timestampTicks: t0));

        var t1 = t0 + TicksFromSeconds(0.05);
        var pose = estimator.GetPose(t1);

        Assert.That(pose.Position.Easting, Is.EqualTo(0.5).Within(1e-9));
        Assert.That(pose.Position.Northing, Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void GetPose_WithYawRate_AdvancesHeading()
    {
        var estimator = new PositionEstimator();
        var t0 = Clock.Current.GetTimestamp();
        estimator.UpdateFromGps(MakeSnapshot(
            position: new Vec2(0, 0),
            heading: 0,
            speedMps: 0,        // Stationary, isolate heading prediction.
            yawRate: 1.0,       // 1 rad/s
            timestampTicks: t0));

        var t1 = t0 + TicksFromSeconds(0.1); // 100 ms later
        var pose = estimator.GetPose(t1);

        Assert.That(pose.Heading, Is.EqualTo(0.1).Within(1e-9));
        Assert.That(pose.Position.Easting, Is.EqualTo(0).Within(1e-9));
        Assert.That(pose.Position.Northing, Is.EqualTo(0).Within(1e-9));
    }

    [Test]
    public void GetPose_NegativeDt_ClampsToSnapshotPose()
    {
        // Out-of-order or skewed clock read should not rewind the prediction.
        var estimator = new PositionEstimator();
        var t0 = Clock.Current.GetTimestamp();
        estimator.UpdateFromGps(MakeSnapshot(
            position: new Vec2(50, 50),
            heading: 0,
            speedMps: 10,
            yawRate: 0,
            timestampTicks: t0));

        var pose = estimator.GetPose(t0 - TicksFromSeconds(0.05));

        Assert.That(pose.Position.Easting, Is.EqualTo(50).Within(1e-9));
        Assert.That(pose.Position.Northing, Is.EqualTo(50).Within(1e-9));
    }

    [Test]
    public void GetPose_BeyondMaxStaleSeconds_ClampsForwardTime()
    {
        // GPS dropout: once dt exceeds MaxStaleSeconds, don't keep predicting.
        var estimator = new PositionEstimator { MaxStaleSeconds = 0.5 };
        var t0 = Clock.Current.GetTimestamp();
        estimator.UpdateFromGps(MakeSnapshot(
            position: new Vec2(0, 0),
            heading: 0,
            speedMps: 10,
            yawRate: 0,
            timestampTicks: t0));

        var pose = estimator.GetPose(t0 + TicksFromSeconds(5.0));

        // Capped at 0.5 s × 10 m/s = 5 m, not 50 m.
        Assert.That(pose.Position.Northing, Is.EqualTo(5.0).Within(1e-6));
    }

    [Test]
    public void UpdateFromGps_ConcurrentReadersGetConsistentSnapshot()
    {
        // Stress: one writer flips between two snapshots while many readers
        // pull. Each reader must observe a fully-consistent record (never a
        // mix of fields from snapshot A and snapshot B).
        var estimator = new PositionEstimator();
        var snapA = MakeSnapshot(position: new Vec2(1, 2), heading: 0.1,
                                  speedMps: 1, yawRate: 0, roll: 0.01,
                                  timestampTicks: 1000);
        var snapB = MakeSnapshot(position: new Vec2(3, 4), heading: 0.2,
                                  speedMps: 2, yawRate: 0, roll: 0.02,
                                  timestampTicks: 2000);
        estimator.UpdateFromGps(snapA);

        using var cts = new CancellationTokenSource();
        int torn = 0;
        var readers = new Task[8];
        for (int i = 0; i < readers.Length; i++)
        {
            readers[i] = Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var s = estimator.GetLatestSnapshot();
                    if (s is null) continue;
                    bool isA = s.Position.Easting == 1 && s.Position.Northing == 2 &&
                               s.Heading == 0.1 && s.SpeedMps == 1 && s.Roll == 0.01 &&
                               s.TimestampTicks == 1000;
                    bool isB = s.Position.Easting == 3 && s.Position.Northing == 4 &&
                               s.Heading == 0.2 && s.SpeedMps == 2 && s.Roll == 0.02 &&
                               s.TimestampTicks == 2000;
                    if (!isA && !isB)
                        Interlocked.Increment(ref torn);
                }
            });
        }

        // Writer flips snapshots for ~50 ms.
        var writerEnd = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 0.05);
        bool useA = false;
        while (Stopwatch.GetTimestamp() < writerEnd)
        {
            estimator.UpdateFromGps(useA ? snapA : snapB);
            useA = !useA;
        }
        cts.Cancel();
        Task.WaitAll(readers);

        Assert.That(torn, Is.EqualTo(0),
            "Readers must always see a fully-consistent snapshot record");
    }

    private static long TicksFromSeconds(double seconds)
        => (long)(seconds * Stopwatch.Frequency);

    private static PoseSnapshot MakeSnapshot(
        Vec2? position = null,
        double heading = 0,
        double speedMps = 0,
        double yawRate = 0,
        double roll = 0,
        long? timestampTicks = null)
        => new(
            position ?? new Vec2(0, 0),
            heading,
            speedMps,
            yawRate,
            roll,
            timestampTicks ?? Clock.Current.GetTimestamp());
}
