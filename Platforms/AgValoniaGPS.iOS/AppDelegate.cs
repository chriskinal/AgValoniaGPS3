// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using Avalonia;
using Avalonia.iOS;
using Avalonia.ReactiveUI;
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
                .UseReactiveUI()
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
        SaveAppState();
    }

    [Export("applicationWillTerminate:")]
    public void OnWillTerminate(UIApplication application)
    {
        SaveAppState();
    }

    private void SaveAppState()
    {
        try
        {
            if (App.Services == null) return;

            // Save panel positions from MainView
            if (App.MainView != null)
            {
                App.MainView.SavePanelPositions();
            }

            // Save configuration (includes panel positions)
            var configService = App.Services.GetRequiredService<IConfigurationService>();
            configService.SaveAppSettings();
            Console.WriteLine("[AppDelegate] Saved configuration on app background/terminate");

            // Save coverage to active field
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
            Console.WriteLine($"[AppDelegate] Error saving app state: {ex.Message}");
        }
    }
}
