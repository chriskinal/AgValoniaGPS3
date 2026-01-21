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

using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Android;

[Activity(
    Label = "AgValoniaGPS",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Enable immersive full-screen mode
        EnableImmersiveMode();
    }

    protected override void OnResume()
    {
        base.OnResume();

        // Re-enable immersive mode when returning to the app
        EnableImmersiveMode();
    }

    protected override void OnPause()
    {
        base.OnPause();

        // Save app state when going to background
        SaveAppState();
    }

    protected override void OnStop()
    {
        base.OnStop();

        // Also save on stop in case OnPause wasn't enough
        SaveAppState();
    }

    private void SaveAppState()
    {
        try
        {
            // Save panel positions from MainView
            App.MainView?.SavePanelPositions();

            // Save settings to ConfigurationStore and disk
            if (App.Services != null)
            {
                var configService = App.Services.GetService<IConfigurationService>();
                configService?.SaveAppSettings();
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainActivity] Error saving app state: {ex.Message}");
        }
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);

        // Re-enable immersive mode when window gains focus
        if (hasFocus)
        {
            EnableImmersiveMode();
        }
    }

    private void EnableImmersiveMode()
    {
        if (Window == null) return;

        // Enable immersive full-screen mode (requires Android 11+ / API 30+)
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            Window.SetDecorFitsSystemWindows(false);
            var controller = Window.InsetsController;
            if (controller != null)
            {
                controller.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .LogToTrace();
    }
}
