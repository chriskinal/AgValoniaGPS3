using System;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.Skia;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(AgValoniaGPS.UI.Tests.TestApp))]

namespace AgValoniaGPS.UI.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        Resources.MergedDictionaries.Add(
            (Avalonia.Controls.ResourceDictionary)AvaloniaXamlLoader.Load(
                new Uri("avares://AgValoniaGPS.Views/Styles/SharedResources.axaml")));
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .UseReactiveUI();
}
