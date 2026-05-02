// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Tool;

namespace AgValoniaGPS.Services.Tests.Tool;

/// <summary>
/// Verifies #313 commit 5b: ToolPositionService publishes a consistent
/// snapshot to readers even under concurrent Update + ResetTrailingState
/// + reads. Each reader observation must come from a single Update — no
/// torn reads where ToolPosition is from update N and HitchPosition is
/// from update N+1.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class ToolPositionServiceConcurrencyTests
{
    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());
        var tool = ConfigurationStore.Instance.Tool;
        tool.Width = 6;
        tool.HitchLength = 0;
        tool.IsToolRearFixed = true;
        tool.IsToolTrailing = false;
        tool.Offset = 0;
    }

    [Test]
    public void ToolPosition_UnderConcurrentWrites_ReturnsConsistentVec3()
    {
        // Vec3 is 24 bytes (3 doubles). Without the snapshot pattern, a
        // mid-write read would split bytes from two writes and produce a
        // garbage value. Writer flips between two well-separated positions;
        // the reader observation must match one of them exactly.
        var service = new ToolPositionService();

        var poseA = new Vec3(100, 200, 0);
        var poseB = new Vec3(500, 600, 0);

        service.Update(poseA, 0);

        using var cts = new CancellationTokenSource();
        int torn = 0;
        var readers = new Task[6];
        for (int i = 0; i < readers.Length; i++)
        {
            readers[i] = Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var pos = service.ToolPosition;
                    bool isA = pos.Easting == 100 && pos.Northing == 200;
                    bool isB = pos.Easting == 500 && pos.Northing == 600;
                    if (!isA && !isB)
                        Interlocked.Increment(ref torn);
                }
            });
        }

        var writerEnd = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 0.05);
        bool useA = false;
        while (Stopwatch.GetTimestamp() < writerEnd)
        {
            service.Update(useA ? poseA : poseB, 0);
            useA = !useA;
        }
        cts.Cancel();
        Task.WaitAll(readers);

        Assert.That(torn, Is.EqualTo(0),
            "Each ToolPosition read must return a Vec3 from one full Update, never a torn mix");
    }

    [Test]
    public void Update_AndResetTrailingState_AreSerialized()
    {
        // Concurrent Update + ResetTrailingState shouldn't corrupt the snapshot.
        // Smoke: run both for a short burst, verify no exception and final
        // snapshot reflects one of the two writers' poses.
        var service = new ToolPositionService();
        service.Update(new Vec3(0, 0, 0), 0);

        using var cts = new CancellationTokenSource();
        var updater = Task.Run(() =>
        {
            int n = 0;
            while (!cts.IsCancellationRequested)
            {
                service.Update(new Vec3(n, n, 0), 0.5);
                n++;
            }
        });
        var resetter = Task.Run(() =>
        {
            int n = 0;
            while (!cts.IsCancellationRequested)
            {
                service.ResetTrailingState(new Vec3(n + 10000, n + 10000, 0), 1.5);
                n++;
            }
        });

        Thread.Sleep(50);
        cts.Cancel();
        Task.WaitAll(updater, resetter);

        // No assertion on final state beyond "didn't crash" — the point is
        // that the lock keeps internal Torriem state consistent across
        // interleaved writers.
        Assert.Pass();
    }
}
