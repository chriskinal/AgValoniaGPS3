using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Timers;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.WebHost;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Set WebRoot to WebUI-POC folder - find it relative to the project file
var projectDir = Directory.GetCurrentDirectory();
var webRoot = Path.Combine(projectDir, "WebUI-POC");
if (!Directory.Exists(webRoot))
{
    // Try relative to solution
    webRoot = Path.GetFullPath(Path.Combine(projectDir, "..", "..", "WebUI-POC"));
}
if (!Directory.Exists(webRoot))
{
    // Try from bin output directory
    webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "WebUI-POC"));
}
Console.WriteLine($"WebRoot: {webRoot} (exists: {Directory.Exists(webRoot)})");

// Configure CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register AgValoniaGPS services
builder.Services.AddSingleton<IGpsService, GpsService>();
builder.Services.AddSingleton<IGpsSimulationService, GpsSimulationService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IFieldService, FieldService>();
builder.Services.AddSingleton<IBoundaryRecordingService, BoundaryRecordingService>();

// WebSocket connection manager
builder.Services.AddSingleton<AgValoniaGPS.WebHost.WebSocketManager>();

var app = builder.Build();

app.UseCors();
app.UseWebSockets();

// Initialize services
var gpsService = app.Services.GetRequiredService<IGpsService>();
var simulatorService = app.Services.GetRequiredService<IGpsSimulationService>();
var settingsService = app.Services.GetRequiredService<ISettingsService>();
var fieldService = app.Services.GetRequiredService<AgValoniaGPS.Services.IFieldService>();
var wsManager = app.Services.GetRequiredService<AgValoniaGPS.WebHost.WebSocketManager>();

// Fields directory - use Documents/AgValoniaGPS/Fields
var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var fieldsRootDirectory = Path.Combine(documentsPath, "AgValoniaGPS", "Fields");
Directory.CreateDirectory(fieldsRootDirectory);
Console.WriteLine($"Fields directory: {fieldsRootDirectory}");

// Load settings
settingsService.Load();

// Initialize simulator with default position
simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(40.7128, -74.0060));

// Track simulator state (the interface doesn't have IsRunning)
bool simulatorRunning = false;
System.Timers.Timer? simulatorTimer = null;

// Local coordinate plane for converting WGS84 to UTM
AgValoniaGPS.Models.LocalPlane? localPlane = null;

// Wire up simulator to GPS service
simulatorService.GpsDataUpdated += (sender, e) =>
{
    var data = e.Data;

    // Create LocalPlane on first data if not exists
    if (localPlane == null)
    {
        var sharedProps = new AgValoniaGPS.Models.SharedFieldProperties();
        localPlane = new AgValoniaGPS.Models.LocalPlane(data.Position, sharedProps);
    }

    // Convert to local coords
    var localCoord = localPlane.ConvertWgs84ToGeoCoord(data.Position);

    var gpsData = new AgValoniaGPS.Models.GpsData
    {
        CurrentPosition = new AgValoniaGPS.Models.Position
        {
            Latitude = data.Position.Latitude,
            Longitude = data.Position.Longitude,
            Heading = data.HeadingDegrees,
            Speed = data.SpeedKmh / 3.6,
            Easting = localCoord.Easting,
            Northing = localCoord.Northing
        },
        FixQuality = 4,
        SatellitesInUse = data.SatellitesTracked,
        Hdop = data.Hdop,
        Timestamp = DateTime.Now
    };
    gpsService.UpdateGpsData(gpsData);
};

// Broadcast GPS updates to all connected WebSocket clients
gpsService.GpsDataUpdated += async (sender, e) =>
{
    var message = new
    {
        type = "gps",
        data = new
        {
            latitude = e.CurrentPosition.Latitude,
            longitude = e.CurrentPosition.Longitude,
            easting = e.CurrentPosition.Easting,
            northing = e.CurrentPosition.Northing,
            heading = e.CurrentPosition.Heading,
            speed = e.CurrentPosition.Speed * 3.6, // m/s to km/h
            fixQuality = e.FixQuality,
            satellites = e.SatellitesInUse
        }
    };
    await wsManager.BroadcastAsync(message);
};

// Serve static files from WebUI-POC folder
if (Directory.Exists(webRoot))
{
    var fileProvider = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
}
else
{
    Console.WriteLine("WARNING: WebUI folder not found, static files won't be served");
}

// WebSocket endpoint
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await wsManager.HandleConnectionAsync(webSocket, simulatorService, settingsService,
            fieldService, fieldsRootDirectory,
            () => simulatorRunning,
            (running) => {
                if (running && !simulatorRunning)
                {
                    // Start simulator timer
                    simulatorTimer = new System.Timers.Timer(100); // 10Hz
                    simulatorTimer.Elapsed += (s, e) => simulatorService.Tick(simulatorService.SteerAngle);
                    simulatorTimer.Start();
                    simulatorRunning = true;
                }
                else if (!running && simulatorRunning)
                {
                    simulatorTimer?.Stop();
                    simulatorTimer?.Dispose();
                    simulatorTimer = null;
                    simulatorRunning = false;
                }
            },
            () => localPlane = null); // Reset local plane callback
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// REST API endpoints
app.MapGet("/api/status", () => new
{
    status = "ok",
    simulator = simulatorRunning,
    gpsConnected = gpsService.IsConnected
});

app.MapGet("/api/settings", () => settingsService.Settings);

app.MapPost("/api/settings", async (HttpContext context) =>
{
    var settings = await context.Request.ReadFromJsonAsync<AgValoniaGPS.Models.AppSettings>();
    if (settings != null)
    {
        settingsService.Settings.SimulatorEnabled = settings.SimulatorEnabled;
        settingsService.Settings.SimulatorLatitude = settings.SimulatorLatitude;
        settingsService.Settings.SimulatorLongitude = settings.SimulatorLongitude;
        settingsService.Save();
        return Results.Ok(new { success = true });
    }
    return Results.BadRequest();
});

app.MapPost("/api/simulator/start", () =>
{
    if (!simulatorRunning)
    {
        simulatorTimer = new System.Timers.Timer(100); // 10Hz
        simulatorTimer.Elapsed += (s, e) => simulatorService.Tick(simulatorService.SteerAngle);
        simulatorTimer.Start();
        simulatorRunning = true;
    }
    return new { success = true, running = true };
});

app.MapPost("/api/simulator/stop", () =>
{
    simulatorTimer?.Stop();
    simulatorTimer?.Dispose();
    simulatorTimer = null;
    simulatorRunning = false;
    return new { success = true, running = false };
});

app.MapPost("/api/simulator/speed", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<SpeedRequest>();
    if (body != null)
    {
        simulatorService.StepDistance = body.Speed / 10.0;
        return Results.Ok(new { success = true, speed = body.Speed });
    }
    return Results.BadRequest();
});

app.MapPost("/api/simulator/steer", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<SteerRequest>();
    if (body != null)
    {
        simulatorService.SteerAngle = body.Angle;
        return Results.Ok(new { success = true, angle = body.Angle });
    }
    return Results.BadRequest();
});

app.MapPost("/api/simulator/position", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<PositionRequest>();
    if (body != null)
    {
        simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(body.Latitude, body.Longitude));
        localPlane = null; // Reset local plane
        return Results.Ok(new { success = true });
    }
    return Results.BadRequest();
});

// Field API endpoints
app.MapGet("/api/fields", () =>
{
    var fields = fieldService.GetAvailableFields(fieldsRootDirectory);
    return new { fields };
});

app.MapGet("/api/fields/active", () =>
{
    var active = fieldService.ActiveField;
    return new
    {
        name = active?.Name,
        isOpen = active != null
    };
});

app.MapPost("/api/fields/open", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<FieldRequest>();
    if (body?.Name != null)
    {
        try
        {
            var fieldPath = Path.Combine(fieldsRootDirectory, body.Name);
            var field = fieldService.LoadField(fieldPath);
            fieldService.SetActiveField(field);
            return Results.Ok(new { success = true, name = field.Name });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }
    return Results.BadRequest(new { success = false, error = "Name required" });
});

app.MapPost("/api/fields/create", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<FieldRequest>();
    if (body?.Name != null)
    {
        try
        {
            var originPosition = new AgValoniaGPS.Models.Position { Latitude = 40.7128, Longitude = -74.0060 };
            var field = fieldService.CreateField(fieldsRootDirectory, body.Name, originPosition);
            fieldService.SetActiveField(field);
            return Results.Ok(new { success = true, name = field.Name });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }
    return Results.BadRequest(new { success = false, error = "Name required" });
});

app.MapPost("/api/fields/close", () =>
{
    fieldService.SetActiveField(null);
    return new { success = true };
});

Console.WriteLine("AgValoniaGPS WebHost starting...");
Console.WriteLine("WebSocket: ws://localhost:5000/ws");
Console.WriteLine("API: http://localhost:5000/api/");
Console.WriteLine("WebUI: http://localhost:5000/");

app.Run("http://0.0.0.0:5000");

// Request DTOs
record SpeedRequest(double Speed);
record SteerRequest(double Angle);
record PositionRequest(double Latitude, double Longitude);
record FieldRequest(string Name);
