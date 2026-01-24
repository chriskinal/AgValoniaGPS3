using Avalonia.Controls;
using Avalonia.Threading;
using AgOpenGPS.Core.Interfaces.Services;
using System;

namespace AgOpenGPS.Avalonia.Desktop.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _renderTimer;
    private IRenderService? _renderService;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initialize the window with services (called from App.axaml.cs)
    /// </summary>
    public void InitializeServices(IRenderService renderService)
    {
        _renderService = renderService ?? throw new ArgumentNullException(nameof(renderService));

        // Set up render timer for continuous rendering (60 FPS target)
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _renderTimer.Tick += (s, e) => _renderService.RequestFrame();
        _renderTimer.Start();
    }
}