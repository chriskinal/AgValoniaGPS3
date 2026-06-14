// Projects the live ApplicationState (the pipeline's single source of truth)
// into the wire contract. This is the litmus test in action: the data the map
// needs is filled from the pipeline/state, never from view-state.

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.RemoteServer;

public sealed class SceneProjector
{
    private readonly ApplicationState _state;

    public SceneProjector(ApplicationState state) => _state = state;

    public SceneDto BuildScene(long version)
    {
        var f = _state.Field;

        // The live boundary the UI draws lives on ActiveField.Boundary (a single
        // Boundary with an outer ring + optional inner holes) — NOT the (unused)
        // Field.Boundaries collection.
        var boundaries = new List<IReadOnlyList<Vec2Dto>>();
        var bnd = f.ActiveField?.Boundary;
        if (bnd is not null)
        {
            AddRing(boundaries, bnd.OuterBoundary);
            if (bnd.InnerBoundaries != null)
                foreach (var inner in bnd.InnerBoundaries.ToArray())
                    AddRing(boundaries, inner);
        }

        var tracks = f.Tracks.ToArray()
            .Where(t => t.IsVisible && t.Points.Count >= 2)
            .Select((t, i) => new TrackDto(
                i.ToString(),
                t.Name,
                (int)t.Type,
                t.Points.Select(p => new Vec2Dto(p.Easting, p.Northing)).ToList()))
            .ToList();

        return new SceneDto(
            version,
            f.OriginLatitude,
            f.OriginLongitude,
            f.FieldName,
            f.ActiveField != null,
            boundaries,
            tracks);
    }

    private static void AddRing(List<IReadOnlyList<Vec2Dto>> rings, BoundaryPolygon? poly)
    {
        if (poly is { Points.Count: >= 3 })
            rings.Add(poly.Points.Select(p => new Vec2Dto(p.Easting, p.Northing)).ToList());
    }

    public TickDto BuildTick(long sceneVersion)
    {
        var v = _state.Vehicle;
        var s = _state.Sections;
        int n = Math.Clamp(s.NumberOfSections, 0, SectionState.MaxSections);
        var sections = new bool[n];
        for (int i = 0; i < n; i++) sections[i] = s.GetSectionActive(i);

        return new TickDto(
            sceneVersion,
            new PoseDto(v.Easting, v.Northing, v.Heading, v.Speed),
            v.FixQuality,
            sections);
    }

    /// <summary>
    /// Cheap change-detector so the broadcaster only re-sends the Scene when the
    /// static-ish geometry actually changes (field swap, boundary/track edits).
    /// </summary>
    public long SceneFingerprint()
    {
        var f = _state.Field;
        long h = 17;
        h = h * 31 + f.FieldName.GetHashCode();
        var bnd = f.ActiveField?.Boundary;
        h = h * 31 + (bnd?.OuterBoundary?.Points.Count ?? 0);
        if (bnd?.InnerBoundaries != null)
            foreach (var inner in bnd.InnerBoundaries.ToArray()) h = h * 31 + inner.Points.Count;
        h = h * 31 + f.Tracks.Count;
        foreach (var t in f.Tracks.ToArray()) h = h * 31 + t.Points.Count;
        return h;
    }
}
