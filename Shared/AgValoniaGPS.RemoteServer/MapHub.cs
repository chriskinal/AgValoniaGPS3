using Microsoft.AspNetCore.SignalR;

namespace AgValoniaGPS.RemoteServer;

/// <summary>
/// SignalR hub for the map feed. Commands (mark AB, select track, etc.) will be
/// added in Phase 3; for now it's a one-way feed — the client receives "scene"
/// and "tick" messages, and gets the current scene immediately on connect.
/// </summary>
public sealed class MapHub : Hub
{
    private readonly MapBroadcaster _broadcaster;

    public MapHub(MapBroadcaster broadcaster) => _broadcaster = broadcaster;

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("scene", _broadcaster.CurrentScene());
        await base.OnConnectedAsync();
    }
}
