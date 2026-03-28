using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.ReactiveUI;
using Avalonia.Skia;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(AgValoniaGPS.IntegrationTests.IntegrationTestApp))]

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// Test application configured for real Skia rendering (not headless drawing).
/// Loads Fluent theme and shared resources to match production appearance.
/// </summary>
public class IntegrationTestApp : Application
{
    public override void Initialize()
    {
        // Match production App.axaml setup
        Styles.Add(new FluentTheme());

        // Load shared resources (converters like FixQualityToColorConverter)
        var sharedResources = new ResourceInclude(new System.Uri("avares://AgValoniaGPS.Views"))
        {
            Source = new System.Uri("avares://AgValoniaGPS.Views/Styles/SharedResources.axaml")
        };
        Resources.MergedDictionaries.Add(sharedResources);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<IntegrationTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .UseSkia()
            .UseReactiveUI();
}
