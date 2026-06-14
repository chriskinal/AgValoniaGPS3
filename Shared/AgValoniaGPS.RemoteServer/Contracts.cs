// Phase 1 wire contract (deliberate, not VM-mirroring). Field-local meters;
// the host sends the field origin once in the Scene, everything else is x=E, y=N.
// Camera is NOT here — it's client-owned (REMOTE_WEB_UI_SPLIT.md §5 rule 3).

namespace AgValoniaGPS.RemoteServer;

/// <summary>A field-local point in meters (x = easting, y = northing).</summary>
public record Vec2Dto(double E, double N);

public record TrackDto(string Id, string Name, int Type, IReadOnlyList<Vec2Dto> Points);

/// <summary>Static-ish geometry: sent on connect and whenever the fingerprint changes.</summary>
public record SceneDto(
    long Version,
    double OriginLat,
    double OriginLon,
    string FieldName,
    bool HasField,
    IReadOnlyList<IReadOnlyList<Vec2Dto>> Boundaries, // each = one outer ring
    IReadOnlyList<TrackDto> Tracks);

public record PoseDto(double E, double N, double Heading, double Speed);

/// <summary>~10 Hz dynamic feed the client extrapolates/draws from.</summary>
public record TickDto(
    long SceneVersion,
    PoseDto Pose,
    int Fix,
    bool[] Sections);
