using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using AgOpenGPS.Avalonia.ViewModels;
using AgOpenGPS.Avalonia.Desktop.Views;
using AgOpenGPS.Avalonia.Desktop.Services;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Avalonia.OpenGL;

namespace AgOpenGPS.Avalonia.Desktop;

public partial class App : Application
{
    private IApplicationService? _applicationService;
    private IRenderService? _renderService;
    private IPlatformService? _platformService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Set up dependency injection manually (Phase 2 approach)
            // In Phase 3+, we can use a DI container like Microsoft.Extensions.DependencyInjection

            // Create temporary window to get platform service set up
            var tempWindow = new Window();

            // Create platform service
            _platformService = new WindowsPlatformService(tempWindow);

            // Create application service
            _applicationService = new ApplicationService(_platformService);

            // We need to create services before the main window, but the render service
            // needs the OpenGL control. For now, we'll create a placeholder.
            // This will be improved in Phase 3 with proper DI container.

            // Create a temporary main window to access its OpenGL control
            // This is a workaround for Phase 2
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow CreateMainWindow()
    {
        // Phase 2: Manual service wiring (will be replaced with DI container in Phase 3)

        // Create main window
        var window = new MainWindow();

        // Find the OpenGL control
        var glControl = window.FindControl<AgOpenGLControl>("OpenGLControl");
        if (glControl == null)
        {
            throw new System.InvalidOperationException("OpenGL control 'OpenGLControl' not found in MainWindow");
        }

        // Create render service
        _renderService = new AvaloniaRenderService(glControl);

        // Create view model
        var viewModel = new MainViewModel(_applicationService!, _renderService);

        // Set data context
        window.DataContext = viewModel;

        // Initialize services
        window.InitializeServices(_renderService);

        return window;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}