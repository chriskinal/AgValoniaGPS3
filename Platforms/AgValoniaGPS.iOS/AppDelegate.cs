using System;
using Avalonia;
using Avalonia.iOS;
using Foundation;
using UIKit;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        try
        {
            Console.WriteLine("[AppDelegate] CustomizeAppBuilder starting...");
            // Explicitly configure for iOS - this ensures no desktop window chrome
            var result = base.CustomizeAppBuilder(builder)
                .UseiOS()
                .LogToTrace();
            Console.WriteLine("[AppDelegate] CustomizeAppBuilder completed.");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppDelegate] CustomizeAppBuilder FAILED: {ex}");
            throw;
        }
    }

    // Force landscape orientation only
    [Export("application:supportedInterfaceOrientationsForWindow:")]
    public UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, UIWindow forWindow)
    {
        return UIInterfaceOrientationMask.Landscape;
    }

    [Export("applicationDidEnterBackground:")]
    public void OnDidEnterBackground(UIApplication application)
    {
        SaveCoverageToActiveField();
    }

    [Export("applicationWillTerminate:")]
    public void OnWillTerminate(UIApplication application)
    {
        SaveCoverageToActiveField();
    }

    private void SaveCoverageToActiveField()
    {
        try
        {
            if (App.Services == null) return;

            var fieldService = App.Services.GetRequiredService<IFieldService>();
            var coverageService = App.Services.GetRequiredService<ICoverageMapService>();

            if (fieldService.ActiveField != null && !string.IsNullOrEmpty(fieldService.ActiveField.DirectoryPath))
            {
                coverageService.SaveToFile(fieldService.ActiveField.DirectoryPath);
                Console.WriteLine($"[Coverage] Saved coverage on app background/terminate to {fieldService.ActiveField.DirectoryPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Coverage] Error saving coverage: {ex.Message}");
        }
    }
}
