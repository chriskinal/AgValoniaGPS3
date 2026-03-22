using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;

[assembly: AvaloniaTestApplication(typeof(AgValoniaGPS.UI.Tests.TestApp))]

namespace AgValoniaGPS.UI.Tests;

public class TestApp : Application
{
    public override void Initialize() { }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseReactiveUI();
}
