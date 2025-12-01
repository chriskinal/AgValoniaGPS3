using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.WebHost;

/// <summary>
/// Manages WebSocket connections and message broadcasting
/// </summary>
public class WebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task HandleConnectionAsync(
        WebSocket webSocket,
        IGpsSimulationService simulatorService,
        ISettingsService settingsService,
        IFieldService fieldService,
        string fieldsRootDirectory,
        Func<bool> getSimulatorRunning,
        Action<bool> setSimulatorRunning,
        Action resetLocalPlane)
    {
        var connectionId = Guid.NewGuid().ToString();
        _connections.TryAdd(connectionId, webSocket);

        Console.WriteLine($"[WebSocket] Client connected: {connectionId}");

        try
        {
            // Send initial state
            var activeField = fieldService.ActiveField;
            await SendAsync(webSocket, new
            {
                type = "connected",
                connectionId,
                simulator = new
                {
                    running = getSimulatorRunning(),
                    speed = simulatorService.StepDistance * 10,
                    steerAngle = simulatorService.SteerAngle
                },
                field = activeField != null ? new
                {
                    name = activeField.Name,
                    isOpen = true
                } : null
            });

            // Handle incoming messages
            var buffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessageAsync(webSocket, message, simulatorService, settingsService,
                        fieldService, fieldsRootDirectory, getSimulatorRunning, setSimulatorRunning, resetLocalPlane);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[WebSocket] Error: {ex.Message}");
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            Console.WriteLine($"[WebSocket] Client disconnected: {connectionId}");

            if (webSocket.State != WebSocketState.Closed)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed",
                        CancellationToken.None);
                }
                catch { }
            }
        }
    }

    private async Task HandleMessageAsync(
        WebSocket webSocket,
        string message,
        IGpsSimulationService simulatorService,
        ISettingsService settingsService,
        IFieldService fieldService,
        string fieldsRootDirectory,
        Func<bool> getSimulatorRunning,
        Action<bool> setSimulatorRunning,
        Action resetLocalPlane)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
                return;

            var type = typeElement.GetString();

            switch (type)
            {
                case "simulator":
                    await HandleSimulatorMessage(webSocket, root, simulatorService,
                        getSimulatorRunning, setSimulatorRunning, resetLocalPlane);
                    break;

                case "settings":
                    await HandleSettingsMessage(webSocket, root, settingsService);
                    break;

                case "field":
                    await HandleFieldMessage(webSocket, root, fieldService, fieldsRootDirectory);
                    break;

                case "ping":
                    await SendAsync(webSocket, new { type = "pong" });
                    break;
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[WebSocket] Invalid JSON: {ex.Message}");
        }
    }

    private async Task HandleSimulatorMessage(
        WebSocket webSocket,
        JsonElement root,
        IGpsSimulationService simulatorService,
        Func<bool> getSimulatorRunning,
        Action<bool> setSimulatorRunning,
        Action resetLocalPlane)
    {
        if (!root.TryGetProperty("action", out var actionElement))
            return;

        var action = actionElement.GetString();

        switch (action)
        {
            case "start":
                setSimulatorRunning(true);
                await SendAsync(webSocket, new { type = "simulator", status = "started" });
                break;

            case "stop":
                setSimulatorRunning(false);
                await SendAsync(webSocket, new { type = "simulator", status = "stopped" });
                break;

            case "setSpeed":
                if (root.TryGetProperty("value", out var speedElement))
                {
                    var speed = speedElement.GetDouble();
                    simulatorService.StepDistance = speed / 10.0;
                    await SendAsync(webSocket, new { type = "simulator", action = "speedSet", speed });
                }
                break;

            case "setSteer":
                if (root.TryGetProperty("value", out var steerElement))
                {
                    simulatorService.SteerAngle = steerElement.GetDouble();
                }
                break;

            case "tick":
                // Manual tick for testing
                simulatorService.Tick(simulatorService.SteerAngle);
                break;

            case "setPosition":
                if (root.TryGetProperty("latitude", out var latElement) &&
                    root.TryGetProperty("longitude", out var lonElement))
                {
                    var lat = latElement.GetDouble();
                    var lon = lonElement.GetDouble();
                    simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(lat, lon));
                    resetLocalPlane(); // Reset so coordinates are recalculated relative to new position
                    await SendAsync(webSocket, new
                    {
                        type = "simulator",
                        status = "positionSet",
                        latitude = lat,
                        longitude = lon
                    });
                }
                break;

            case "getStatus":
                await SendAsync(webSocket, new
                {
                    type = "simulator",
                    action = "status",
                    running = getSimulatorRunning(),
                    speed = simulatorService.StepDistance * 10,
                    steerAngle = simulatorService.SteerAngle
                });
                break;
        }
    }

    private async Task HandleSettingsMessage(
        WebSocket webSocket,
        JsonElement root,
        ISettingsService settingsService)
    {
        if (!root.TryGetProperty("action", out var actionElement))
            return;

        var action = actionElement.GetString();

        switch (action)
        {
            case "get":
                await SendAsync(webSocket, new
                {
                    type = "settings",
                    data = settingsService.Settings
                });
                break;

            case "save":
                settingsService.Save();
                await SendAsync(webSocket, new { type = "settings", status = "saved" });
                break;
        }
    }

    private async Task HandleFieldMessage(
        WebSocket webSocket,
        JsonElement root,
        IFieldService fieldService,
        string fieldsRootDirectory)
    {
        if (!root.TryGetProperty("action", out var actionElement))
            return;

        var action = actionElement.GetString();

        switch (action)
        {
            case "list":
                var fields = fieldService.GetAvailableFields(fieldsRootDirectory);
                await SendAsync(webSocket, new
                {
                    type = "field",
                    action = "list",
                    fields = fields
                });
                break;

            case "open":
                if (root.TryGetProperty("name", out var nameElement))
                {
                    var fieldName = nameElement.GetString();
                    var fieldPath = Path.Combine(fieldsRootDirectory, fieldName!);
                    try
                    {
                        var field = fieldService.LoadField(fieldPath);
                        fieldService.SetActiveField(field);

                        // Build boundary data for WebUI
                        object? boundaryData = null;
                        if (field.Boundary?.OuterBoundary?.Points != null && field.Boundary.OuterBoundary.Points.Count > 0)
                        {
                            boundaryData = new
                            {
                                outer = field.Boundary.OuterBoundary.Points.Select(p => new { e = p.Easting, n = p.Northing }).ToArray(),
                                inner = field.Boundary.InnerBoundaries.Select(ib =>
                                    ib.Points.Select(p => new { e = p.Easting, n = p.Northing }).ToArray()
                                ).ToArray()
                            };
                        }

                        // Include origin so client can move simulator to field
                        var origin = field.Origin;

                        await SendAsync(webSocket, new
                        {
                            type = "field",
                            action = "opened",
                            name = field.Name,
                            success = true,
                            boundary = boundaryData,
                            origin = new { latitude = origin.Latitude, longitude = origin.Longitude }
                        });
                        // Broadcast to all clients
                        await BroadcastAsync(new
                        {
                            type = "field",
                            action = "changed",
                            name = field.Name,
                            isOpen = true,
                            boundary = boundaryData,
                            origin = new { latitude = origin.Latitude, longitude = origin.Longitude }
                        });
                    }
                    catch (Exception ex)
                    {
                        await SendAsync(webSocket, new
                        {
                            type = "field",
                            action = "error",
                            message = ex.Message
                        });
                    }
                }
                break;

            case "close":
                fieldService.SetActiveField(null);
                await SendAsync(webSocket, new
                {
                    type = "field",
                    action = "closed",
                    success = true
                });
                await BroadcastAsync(new
                {
                    type = "field",
                    action = "changed",
                    name = (string?)null,
                    isOpen = false
                });
                break;

            case "create":
                if (root.TryGetProperty("name", out var newNameElement))
                {
                    var fieldName = newNameElement.GetString();
                    try
                    {
                        var originPosition = new AgValoniaGPS.Models.Position { Latitude = 40.7128, Longitude = -74.0060 };
                        var field = fieldService.CreateField(fieldsRootDirectory, fieldName!, originPosition);
                        fieldService.SetActiveField(field);
                        await SendAsync(webSocket, new
                        {
                            type = "field",
                            action = "created",
                            name = field.Name,
                            success = true
                        });
                        await BroadcastAsync(new
                        {
                            type = "field",
                            action = "changed",
                            name = field.Name,
                            isOpen = true
                        });
                    }
                    catch (Exception ex)
                    {
                        await SendAsync(webSocket, new
                        {
                            type = "field",
                            action = "error",
                            message = ex.Message
                        });
                    }
                }
                break;

            case "getActive":
                var activeField = fieldService.ActiveField;
                await SendAsync(webSocket, new
                {
                    type = "field",
                    action = "active",
                    name = activeField?.Name,
                    isOpen = activeField != null
                });
                break;
        }
    }

    public async Task BroadcastAsync(object message)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var deadConnections = new List<string>();

        foreach (var (id, socket) in _connections)
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    deadConnections.Add(id);
                }
            }
            else
            {
                deadConnections.Add(id);
            }
        }

        foreach (var id in deadConnections)
        {
            _connections.TryRemove(id, out _);
        }
    }

    private async Task SendAsync(WebSocket webSocket, object message)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    public int ConnectionCount => _connections.Count;
}
