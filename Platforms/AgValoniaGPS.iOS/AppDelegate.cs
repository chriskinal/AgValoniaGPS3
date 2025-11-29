using System;
using Avalonia;
using Avalonia.iOS;
using Foundation;
using UIKit;

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
}
