// Embeds a minimal Kestrel + SignalR server inside the running app. The host
// passes in the live ApplicationState (the DI singleton the pipeline writes to),
// and we bridge it into the server's own DI container as a singleton instance.

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.RemoteServer;

public sealed class RemoteServerHost
{
    private WebApplication? _app;

    /// <param name="state">The live DI ApplicationState the app/pipeline updates.</param>
    /// <param name="port">Bound on 0.0.0.0 so LAN clients (tablets) can connect.</param>
    public async Task StartAsync(ApplicationState state, int port = 5174)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        builder.Services.AddSignalR();
        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton<SceneProjector>();
        builder.Services.AddSingleton<MapBroadcaster>();

        var app = builder.Build();

        app.MapHub<MapHub>("/maphub");
        app.MapGet("/", () => Results.Content(ReadAsset("index.html"), "text/html"));
        app.MapGet("/app.js", () => Results.Content(ReadAsset("app.js"), "text/javascript"));

        await app.StartAsync();
        app.Services.GetRequiredService<MapBroadcaster>().Start();
        _app = app;
    }

    public async Task StopAsync()
    {
        if (_app is null) return;
        await _app.Services.GetRequiredService<MapBroadcaster>().DisposeAsync();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    private static string ReadAsset(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var res = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".wwwroot." + name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Embedded client asset not found: {name}");
        using var s = asm.GetManifestResourceStream(res)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
