// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Headless;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// HTTP server for remote headless UI testing.
/// Accepts mouse/keyboard actions and returns screenshots + state.
///
/// Usage: dotnet run -- --headless --remote-test [--port 5123]
///
/// Endpoints:
///   GET  /screenshot          - Current window as PNG
///   GET  /state               - JSON app state (position, speed, track, etc.)
///   POST /click               - {"x":100,"y":200,"button":"left"}
///   POST /doubleclick         - {"x":100,"y":200}
///   POST /drag                - {"fromX":0,"fromY":0,"toX":100,"toY":100}
///   POST /key                 - {"key":"A"} or {"key":"Enter"}
///   POST /type                - {"text":"hello"}
///   POST /command             - {"name":"ToggleAutoSteerCommand"}
///   POST /scroll              - {"x":100,"y":200,"delta":3}
///   GET  /elements            - Visual tree with bounds (for element discovery)
///   POST /wait                - {"ms":500} - wait and return screenshot
/// </summary>
public class RemoteTestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Window _window;
    private readonly MainViewModel _vm;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _port;

    public RemoteTestServer(Window window, MainViewModel vm, int port = 5123)
    {
        _window = window;
        _vm = vm;
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public async Task RunAsync()
    {
        _listener.Start();
        Console.WriteLine($"[Remote Test Server] Listening on http://localhost:{_port}/");
        Console.WriteLine("[Remote Test Server] Endpoints: /screenshot /state /click /key /type /command /elements /wait");

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx));
            }
            catch (HttpListenerException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Remote Test Server] Error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var resp = ctx.Response;
        resp.Headers.Add("Access-Control-Allow-Origin", "*");

        try
        {
            var path = req.Url?.AbsolutePath ?? "/";
            var method = req.HttpMethod;

            switch (path)
            {
                case "/screenshot":
                    await HandleScreenshot(resp);
                    break;

                case "/state":
                    await HandleState(resp);
                    break;

                case "/click":
                    await HandleClick(req, resp);
                    break;

                case "/doubleclick":
                    await HandleDoubleClick(req, resp);
                    break;

                case "/drag":
                    await HandleDrag(req, resp);
                    break;

                case "/key":
                    await HandleKey(req, resp);
                    break;

                case "/type":
                    await HandleType(req, resp);
                    break;

                case "/command":
                    await HandleCommand(req, resp);
                    break;

                case "/scroll":
                    await HandleScroll(req, resp);
                    break;

                case "/elements":
                    await HandleElements(resp);
                    break;

                case "/wait":
                    await HandleWait(req, resp);
                    break;

                case "/tracks":
                    await HandleTracks(req, resp);
                    break;

                case "/setproperty":
                    await HandleSetProperty(req, resp);
                    break;

                default:
                    resp.StatusCode = 404;
                    await WriteJson(resp, new { error = "Unknown endpoint", path });
                    break;
            }
        }
        catch (Exception ex)
        {
            resp.StatusCode = 500;
            await WriteJson(resp, new { error = ex.Message });
        }
        finally
        {
            resp.Close();
        }
    }

    private async Task HandleScreenshot(HttpListenerResponse resp)
    {
        var png = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
            var ps = new PixelSize(
                Math.Max((int)_window.Bounds.Width, 1),
                Math.Max((int)_window.Bounds.Height, 1));
            var bmp = new RenderTargetBitmap(ps, new Vector(96, 96));
            bmp.Render(_window);
            using var ms = new MemoryStream();
            bmp.Save(ms);
            return ms.ToArray();
        });

        resp.ContentType = "image/png";
        resp.ContentLength64 = png.Length;
        await resp.OutputStream.WriteAsync(png);
    }

    private async Task HandleState(HttpListenerResponse resp)
    {
        var state = await Dispatcher.UIThread.InvokeAsync(() => new
        {
            easting = _vm.Easting,
            northing = _vm.Northing,
            heading = _vm.Heading,
            speed = _vm.SpeedKmh,
            latitude = _vm.Latitude,
            longitude = _vm.Longitude,
            isFieldOpen = _vm.IsFieldOpen,
            hasActiveTrack = _vm.HasActiveTrack,
            selectedTrack = _vm.SelectedTrack?.Name,
            isAutoSteerEngaged = _vm.IsAutoSteerEngaged,
            isSectionMasterOn = _vm.IsSectionMasterOn,
            isSimulatorEnabled = _vm.IsSimulatorEnabled,
            crossTrackError = _vm.CrossTrackError,
            isDayMode = _vm.IsDayMode,
            isDialogOpen = _vm.State.UI.IsDialogOpen,
            activeDialog = _vm.State.UI.ActiveDialog.ToString(),
            trackCount = _vm.SavedTracks.Count,
            windowWidth = _window.Bounds.Width,
            windowHeight = _window.Bounds.Height
        });

        await WriteJson(resp, state);
    }

    private async Task HandleClick(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        double x = body.GetProperty("x").GetDouble();
        double y = body.GetProperty("y").GetDouble();
        string button = body.TryGetProperty("button", out var b) ? b.GetString() ?? "left" : "left";

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var mouseButton = button == "right" ? MouseButton.Right : MouseButton.Left;
            SimulateClick(new Point(x, y), mouseButton);
            Dispatcher.UIThread.RunJobs();
        });

        await Task.Delay(100);
        await HandleScreenshot(resp);
    }

    private async Task HandleDoubleClick(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        double x = body.GetProperty("x").GetDouble();
        double y = body.GetProperty("y").GetDouble();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SimulateClick(new Point(x, y), MouseButton.Left);
            SimulateClick(new Point(x, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        });

        await Task.Delay(100);
        await HandleScreenshot(resp);
    }

    private async Task HandleDrag(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        double fromX = body.GetProperty("fromX").GetDouble();
        double fromY = body.GetProperty("fromY").GetDouble();
        double toX = body.GetProperty("toX").GetDouble();
        double toY = body.GetProperty("toY").GetDouble();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Simulate drag with intermediate moves
            SimulateClick(new Point(fromX, fromY), MouseButton.Left);
            int steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                SimulateMove(new Point(fromX + (toX - fromX) * t, fromY + (toY - fromY) * t));
            }
            Dispatcher.UIThread.RunJobs();
        });

        await Task.Delay(100);
        await HandleScreenshot(resp);
    }

    private async Task HandleKey(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        string keyName = body.GetProperty("key").GetString() ?? "A";

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Enum.TryParse<Key>(keyName, true, out var key))
            {
                var args = new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = key
                };
                _window.RaiseEvent(args);
            }
            Dispatcher.UIThread.RunJobs();
        });

        await Task.Delay(50);
        await HandleScreenshot(resp);
    }

    private async Task HandleType(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        string text = body.GetProperty("text").GetString() ?? "";

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (char c in text)
            {
                var args = new TextInputEventArgs
                {
                    RoutedEvent = InputElement.TextInputEvent,
                    Text = c.ToString()
                };
                _window.RaiseEvent(args);
            }
            Dispatcher.UIThread.RunJobs();
        });

        await Task.Delay(50);
        await HandleScreenshot(resp);
    }

    private async Task HandleCommand(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        string cmdName = body.GetProperty("name").GetString() ?? "";

        string result = "unknown";
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Find command by reflection
            var prop = _vm.GetType().GetProperty(cmdName);
            if (prop?.GetValue(_vm) is System.Windows.Input.ICommand cmd)
            {
                var param = body.TryGetProperty("parameter", out var p) ? p.GetString() : null;
                if (cmd.CanExecute(param))
                {
                    cmd.Execute(param);
                    result = "executed";
                }
                else
                {
                    result = "cannot_execute";
                }
            }
            else
            {
                result = "not_found";
            }
            Dispatcher.UIThread.RunJobs();
        });

        await Task.Delay(100);
        await WriteJson(resp, new { command = cmdName, result });
    }

    private async Task HandleScroll(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        double x = body.GetProperty("x").GetDouble();
        double y = body.GetProperty("y").GetDouble();
        double delta = body.GetProperty("delta").GetDouble();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Use zoom commands as a proxy for scroll
            for (int i = 0; i < Math.Abs((int)delta); i++)
            {
                if (delta > 0)
                    _vm.ZoomInCommand?.Execute(null);
                else
                    _vm.ZoomOutCommand?.Execute(null);
            }
            Dispatcher.UIThread.RunJobs();
        });

        await Task.Delay(50);
        await HandleScreenshot(resp);
    }

    private async Task HandleElements(HttpListenerResponse resp)
    {
        var elements = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var list = new System.Collections.Generic.List<object>();
            CollectElements(_window, list, 0);
            return list;
        });

        await WriteJson(resp, new { elements });
    }

    private void CollectElements(Avalonia.Visual visual, System.Collections.Generic.List<object> list, int depth)
    {
        if (depth > 20) return; // Limit depth

        if (visual is Control control && control.IsVisible && control.Bounds.Width > 0)
        {
            var topLeft = control.TranslatePoint(new Point(0, 0), _window);
            if (topLeft.HasValue)
            {
                string? name = control.Name;
                string type = control.GetType().Name;
                string? text = null;

                if (control is Avalonia.Controls.TextBlock tb) text = tb.Text;
                else if (control is Avalonia.Controls.Button btn) text = btn.Content?.ToString();

                if (name != null || text != null || control is Button)
                {
                    list.Add(new
                    {
                        type,
                        name,
                        text,
                        x = topLeft.Value.X,
                        y = topLeft.Value.Y,
                        width = control.Bounds.Width,
                        height = control.Bounds.Height,
                        isEnabled = control.IsEnabled
                    });
                }
            }
        }

        foreach (var child in visual.GetVisualChildren())
        {
            if (child is Avalonia.Visual v)
                CollectElements(v, list, depth + 1);
        }
    }

    private async Task HandleWait(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        int ms = body.TryGetProperty("ms", out var m) ? m.GetInt32() : 500;
        ms = Math.Clamp(ms, 0, 10000);

        await Task.Delay(ms);
        await Dispatcher.UIThread.InvokeAsync(() => Dispatcher.UIThread.RunJobs());
        await HandleScreenshot(resp);
    }

    private async Task HandleTracks(HttpListenerRequest req, HttpListenerResponse resp)
    {
        if (req.HttpMethod == "POST")
        {
            // Select a track by index
            var body = await ReadJson(req);
            int index = body.GetProperty("index").GetInt32();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (index >= 0 && index < _vm.SavedTracks.Count)
                {
                    _vm.SelectedTrack = _vm.SavedTracks[index];
                }
                Dispatcher.UIThread.RunJobs();
            });
            await Task.Delay(100);
        }

        var tracks = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var list = new System.Collections.Generic.List<object>();
            for (int i = 0; i < _vm.SavedTracks.Count; i++)
            {
                var t = _vm.SavedTracks[i];
                list.Add(new
                {
                    index = i,
                    name = t.Name,
                    isActive = t.IsActive,
                    pointCount = t.Points.Count,
                    isABLine = t.IsABLine,
                    isCurve = t.IsCurve
                });
            }
            return list;
        });

        await WriteJson(resp, new
        {
            tracks,
            selectedTrack = _vm.SelectedTrack?.Name,
            hasActiveTrack = _vm.HasActiveTrack
        });
    }

    private async Task HandleSetProperty(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadJson(req);
        string propName = body.GetProperty("name").GetString() ?? "";
        string result = "unknown";

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var prop = _vm.GetType().GetProperty(propName);
            if (prop != null && prop.CanWrite)
            {
                try
                {
                    object? value = null;
                    var propType = prop.PropertyType;

                    if (body.TryGetProperty("value", out var jsonVal))
                    {
                        if (propType == typeof(double))
                            value = jsonVal.GetDouble();
                        else if (propType == typeof(int))
                            value = jsonVal.GetInt32();
                        else if (propType == typeof(bool))
                            value = jsonVal.GetBoolean();
                        else if (propType == typeof(string))
                            value = jsonVal.GetString();
                    }

                    if (value != null)
                    {
                        prop.SetValue(_vm, value);
                        result = "set";
                    }
                    else
                    {
                        result = "unsupported_type";
                    }
                }
                catch (Exception ex)
                {
                    result = $"error: {ex.Message}";
                }
            }
            else
            {
                result = prop == null ? "not_found" : "read_only";
            }
            Dispatcher.UIThread.RunJobs();
        });

        await WriteJson(resp, new { property = propName, result });
    }

    // --- Helpers ---

    private static async Task<JsonElement> ReadJson(HttpListenerRequest req)
    {
        using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    private static async Task WriteJson(HttpListenerResponse resp, object data)
    {
        resp.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes);
    }

    // Use Avalonia headless input simulation via the window's input manager
    private void SimulateClick(Point pos, MouseButton button)
    {
        // Find the control at the given position and invoke its click
        var hit = _window.InputHitTest(pos);
        if (hit is Avalonia.Input.IInputElement)
        {
            // Walk up the visual tree to find a clickable control
            var current = hit as Avalonia.Visual;
            while (current != null)
            {
                if (current is Button btn)
                {
                    if (btn.Command != null && btn.Command.CanExecute(btn.CommandParameter))
                    {
                        btn.Command.Execute(btn.CommandParameter);
                        return;
                    }
                    // Fire Click event for buttons with code-behind handlers
                    btn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    return;
                }

                // Handle DataGrid row clicks - select the item
                if (current is Avalonia.Controls.DataGridRow row)
                {
                    var grid = row.FindAncestorOfType<Avalonia.Controls.DataGrid>();
                    if (grid != null)
                    {
                        grid.SelectedIndex = row.Index;
                    }
                    return;
                }

                // Handle ListBoxItem clicks - select the item
                if (current is Avalonia.Controls.ListBoxItem lbi)
                {
                    var listBox = lbi.FindAncestorOfType<Avalonia.Controls.ListBox>();
                    if (listBox != null)
                    {
                        listBox.SelectedItem = lbi.DataContext;
                    }
                    return;
                }

                // Handle CheckBox clicks - toggle
                if (current is Avalonia.Controls.CheckBox cb)
                {
                    cb.IsChecked = !cb.IsChecked;
                    return;
                }

                // Handle TabItem clicks - select the tab
                if (current is Avalonia.Controls.TabItem tabItem)
                {
                    var tabControl = tabItem.FindAncestorOfType<Avalonia.Controls.TabControl>();
                    if (tabControl != null)
                    {
                        tabControl.SelectedItem = tabItem;
                    }
                    return;
                }

                current = current.GetVisualParent();
            }

            // No known interactive control found - use headless mouse simulation
            // which will propagate PointerPressed through the visual tree
        }

        // Fallback: use Avalonia headless mouse simulation
        try
        {
            _window.MouseDown(pos, button);
            _window.MouseUp(pos, button);
        }
        catch { /* Headless methods not available */ }
    }

    private void SimulateMove(Point pos)
    {
        try
        {
            _window.MouseMove(pos);
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }
}
