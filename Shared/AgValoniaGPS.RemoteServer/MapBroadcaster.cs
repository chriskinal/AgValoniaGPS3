// Background 10 Hz broadcaster. Uses a PeriodicTimer (NOT a DispatcherTimer) —
// it runs off the UI thread and has no Avalonia dependency, which foreshadows
// the ITimer abstraction the headless host needs (CONFIG_STATE_AUDIT §11.3).

using Microsoft.AspNetCore.SignalR;

namespace AgValoniaGPS.RemoteServer;

public sealed class MapBroadcaster : IAsyncDisposable
{
    private readonly IHubContext<MapHub> _hub;
    private readonly SceneProjector _projector;
    private readonly CancellationTokenSource _cts = new();

    private long _sceneVersion;
    private long _lastFingerprint = long.MinValue;
    private SceneDto _currentScene;
    private Task? _loop;

    public MapBroadcaster(IHubContext<MapHub> hub, SceneProjector projector)
    {
        _hub = hub;
        _projector = projector;
        _currentScene = projector.BuildScene(0);
    }

    /// <summary>Latest scene — sent to each client on connect.</summary>
    public SceneDto CurrentScene() => _currentScene;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100)); // 10 Hz
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var fp = _projector.SceneFingerprint();
                if (fp != _lastFingerprint)
                {
                    _lastFingerprint = fp;
                    _currentScene = _projector.BuildScene(++_sceneVersion);
                    await _hub.Clients.All.SendAsync("scene", _currentScene, ct).ConfigureAwait(false);
                }

                await _hub.Clients.All.SendAsync("tick", _projector.BuildTick(_sceneVersion), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                // Tolerate transient read races: ApplicationState collections are
                // mutated on the UI thread; an occasional dropped frame is fine.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { /* ignore */ }
        }
        _cts.Dispose();
    }
}
