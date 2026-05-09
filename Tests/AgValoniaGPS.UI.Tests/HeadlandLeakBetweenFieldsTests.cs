// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;
using Avalonia.Headless.NUnit;
using NSubstitute;
using NUnit.Framework;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Regression tests for the "headland leak between fields" bug:
/// closing field A then creating field B used to surface field A's
/// headland geometry on B because in-memory headland state
/// (HeadlandSegments + HeadlandPreviewLine) was never cleared on close.
/// </summary>
[TestFixture]
public class HeadlandLeakBetweenFieldsTests
{
    [AvaloniaTest]
    public async Task CloseFieldAsync_ClearsHeadlandStateOnMapAndInVm()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Seed an "active field" so CloseFieldAsync takes the save path
        // and not just the early-return clear path.
        var fieldDir = Path.Combine(Path.GetTempPath(), "HeadlandLeakTestFieldA");
        Directory.CreateDirectory(fieldDir);
        var fieldA = new Field { Name = "FieldA", DirectoryPath = fieldDir };
        vm.ActiveField = fieldA;

        // Seed in-memory headland state as if field A had built a headland.
        var line = new List<Vec3>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0), new(0, 0, 0)
        };
        vm.CurrentHeadlandLine = line;
        vm.HeadlandSegments.Add(new HeadlandSegment
        {
            Name = "seg-A",
            Type = HeadlandSegmentType.Boundary,
            BoundaryPoints = new List<Vec3>(line),
            OffsetPoints = new List<Vec3>(line),
            Offset = 12.0
        });
        vm.HeadlandPreviewLine = new List<Vec2> { new(0, 0), new(1, 1), new(2, 0) };

        // Sanity check the seed worked.
        Assert.That(vm.HeadlandSegments.Count, Is.EqualTo(1));
        Assert.That(vm.CurrentHeadlandLine, Is.Not.Null);
        Assert.That(vm.HeadlandPreviewLine, Is.Not.Null);

        // Act: close the field. This is the operator's "close field A" step.
        await vm.CloseFieldAsync();

        // Assert: every in-memory headland surface is wiped.
        Assert.That(vm.CurrentHeadlandLine, Is.Null,
            "_currentHeadlandLine must be null after close");
        Assert.That(vm.State.Field.HeadlandLine, Is.Null,
            "State.Field.HeadlandLine must be null after close");
        Assert.That(vm.HeadlandSegments, Is.Empty,
            "HeadlandSegments must be cleared so the next field can't inherit them");
        Assert.That(vm.HeadlandPreviewLine, Is.Null,
            "HeadlandPreviewLine must be cleared (FieldBuilder dialog reads it)");
        Assert.That(vm.HasHeadland, Is.False);
        Assert.That(vm.IsHeadlandOn, Is.False);

        // Assert: the map service was told to drop the headland geometry
        // and to hide the layer. Without these, the map keeps drawing
        // the previous field's headland on the next field.
        builder.MapService.Received().SetHeadlandLine(null);
        builder.MapService.Received().SetHeadlandVisible(false);

        // Cleanup
        try { Directory.Delete(fieldDir, recursive: true); } catch { }
    }

    [AvaloniaTest]
    public async Task NewFieldAfterClose_DoesNotInheritOldHeadlandSegments()
    {
        // This is the user-reported flow: open A (with headland), close A,
        // create new B. Verifies B starts with empty headland state and
        // BuildHeadlandFromSegments on the (boundary-less) new field is a no-op
        // — without the close-side fix, segments from A would still be present
        // and BuildHeadlandFromSegments would reuse them.
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        var fieldDir = Path.Combine(Path.GetTempPath(), "HeadlandLeakTestFieldA2");
        Directory.CreateDirectory(fieldDir);
        vm.ActiveField = new Field { Name = "FieldA", DirectoryPath = fieldDir };

        // Seed headland state on field A.
        vm.HeadlandSegments.Add(new HeadlandSegment
        {
            Name = "seg-A",
            Type = HeadlandSegmentType.Line,
            BoundaryPoints = new List<Vec3> { new(0, 0, 0), new(10, 0, 0) },
            OffsetPoints = new List<Vec3> { new(0, 12, 0), new(10, 12, 0) }
        });
        vm.CurrentHeadlandLine = new List<Vec3>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0), new(0, 0, 0)
        };

        // Close A.
        await vm.CloseFieldAsync();

        // Simulate user creating a brand-new field B (no boundary, no headland on disk).
        // The real ConfirmNewFieldDialogCommand sets ActiveField on FieldService —
        // we mimic the resulting VM state directly here since we can't run the
        // full file-creation command without writing real files.
        Assert.That(vm.HeadlandSegments, Is.Empty,
            "Field B must start with no headland segments — none were leaked from A");

        // Building headland from (empty) segments on B must not resurrect A's headland.
        vm.BuildHeadlandFromSegments();
        Assert.That(vm.CurrentHeadlandLine, Is.Null,
            "B has no boundary and no segments — headland line must stay null");
        Assert.That(vm.HasHeadland, Is.False);

        // Cleanup
        try { Directory.Delete(fieldDir, recursive: true); } catch { }
    }
}
