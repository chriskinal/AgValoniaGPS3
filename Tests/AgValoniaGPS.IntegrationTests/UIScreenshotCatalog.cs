// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// Captures screenshots of every UI element in both light and dark mode.
/// Run via: dotnet run --project Tests/AgValoniaGPS.IntegrationTests/ -- --headless --catalog
/// Screenshots saved to screenshots/catalog/{dark,light}/
/// </summary>
public static class UIScreenshotCatalog
{
    private static string _baseDir = "";

    public static async Task Run(Window window, MainViewModel vm)
    {
        _baseDir = Path.Combine(AppContext.BaseDirectory, "screenshots", "catalog");

        Console.WriteLine($"[Catalog] Capturing UI catalog to: {_baseDir}");

        foreach (var theme in new[] { "dark", "light" })
        {
            bool isDayMode = theme == "light";
            var themeDir = Path.Combine(_baseDir, theme);
            Directory.CreateDirectory(themeDir);

            // Switch theme
            if (Avalonia.Application.Current != null)
            {
                Avalonia.Application.Current.RequestedThemeVariant =
                    isDayMode ? ThemeVariant.Light : ThemeVariant.Dark;
            }
            vm.IsDayMode = isDayMode;
            await Pump(200);

            Console.WriteLine($"[Catalog] === {theme.ToUpper()} MODE ===");

            // 1. Main view (clean, no dialogs)
            vm.State.UI.CloseDialog();
            if (vm.ConfigurationViewModel != null)
                vm.ConfigurationViewModel.IsDialogVisible = false;
            vm.State.UI.IsSimulatorPanelVisible = false;
            await Pump(200);
            Capture(window, themeDir, "00_main_view");

            // 2. Main view with simulator panel
            vm.State.UI.IsSimulatorPanelVisible = true;
            await Pump(200);
            Capture(window, themeDir, "01_simulator_panel");
            vm.State.UI.IsSimulatorPanelVisible = false;

            // 3. Dialogs
            var dialogs = new (DialogType type, string name)[]
            {
                (DialogType.FieldSelection, "field_selection"),
                (DialogType.Tracks, "tracks"),
                (DialogType.NewField, "new_field"),
                (DialogType.DataIO, "data_io"),
                (DialogType.HeadlandBuilder, "headland_builder"),
                (DialogType.QuickABSelector, "quick_ab_selector"),
                (DialogType.DrawAB, "draw_ab"),
                (DialogType.NtripProfiles, "ntrip_profiles"),
                (DialogType.AgShareSettings, "agshare_settings"),
                (DialogType.AppDirectories, "app_directories"),
                (DialogType.HotkeyConfig, "hotkey_config"),
                (DialogType.About, "about"),
                (DialogType.LogViewer, "log_viewer"),
                (DialogType.FlagByLatLon, "flag_by_latlon"),
                (DialogType.FlagList, "flag_list"),
                (DialogType.ViewSettings, "view_settings"),
                (DialogType.ImportTracks, "import_tracks"),
                (DialogType.KmlImport, "kml_import"),
                (DialogType.IsoXmlImport, "isoxml_import"),
                (DialogType.SimCoords, "sim_coords"),
            };

            foreach (var (type, name) in dialogs)
            {
                try
                {
                    vm.State.UI.ShowDialog(type);
                    await Pump(200);
                    Capture(window, themeDir, $"dialog_{name}");
                    vm.State.UI.CloseDialog();
                    await Pump(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [SKIP] dialog_{name}: {ex.Message}");
                    vm.State.UI.CloseDialog();
                    await Pump(100);
                }
            }

            // 4. Configuration dialog with each tab
            try
            {
                vm.ShowConfigurationDialogCommand?.Execute(null);
                await Pump(300);

                if (vm.ConfigurationViewModel != null)
                {
                    var tabNames = new[]
                    {
                        "vehicle", "tool", "uturn", "machine_control",
                        "tram_lines", "data_sources", "display", "additional_options"
                    };

                    // Find the TabControl in the visual tree
                    var tabControl = FindTabControl(window);

                    for (int i = 0; i < tabNames.Length; i++)
                    {
                        if (tabControl != null && i < tabControl.ItemCount)
                        {
                            tabControl.SelectedIndex = i;
                            await Pump(200);
                        }
                        Capture(window, themeDir, $"config_tab_{i}_{tabNames[i]}");
                    }

                    vm.ConfigurationViewModel.IsDialogVisible = false;
                    await Pump(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [SKIP] config tabs: {ex.Message}");
                if (vm.ConfigurationViewModel != null)
                    vm.ConfigurationViewModel.IsDialogVisible = false;
                await Pump(100);
            }

            // 5. Panels (non-modal, show over main view)
            vm.State.UI.CloseDialog();

            // Section control panel
            vm.State.UI.IsSectionControlPanelVisible = true;
            await Pump(200);
            Capture(window, themeDir, "panel_section_control");
            vm.State.UI.IsSectionControlPanelVisible = false;

            // Boundary panel
            vm.State.UI.IsBoundaryPanelVisible = true;
            await Pump(200);
            Capture(window, themeDir, "panel_boundary");
            vm.State.UI.IsBoundaryPanelVisible = false;

            // Offset fix panel
            vm.State.UI.IsOffsetFixPanelVisible = true;
            await Pump(200);
            Capture(window, themeDir, "panel_offset_fix");
            vm.State.UI.IsOffsetFixPanelVisible = false;

            // View settings panel
            vm.State.UI.IsViewSettingsPanelVisible = true;
            await Pump(200);
            Capture(window, themeDir, "panel_view_settings");
            vm.State.UI.IsViewSettingsPanelVisible = false;

            await Pump(100);
        }

        // Count screenshots
        int total = Directory.GetFiles(_baseDir, "*.png", SearchOption.AllDirectories).Length;
        Console.WriteLine($"[Catalog] Done: {total} screenshots captured");
    }

    private static void Capture(Window window, string dir, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var pixelSize = new PixelSize(
            Math.Max((int)window.Bounds.Width, 1),
            Math.Max((int)window.Bounds.Height, 1));
        var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        bitmap.Render(window);

        var path = Path.Combine(dir, $"{name}.png");
        bitmap.Save(path);
        var kb = new FileInfo(path).Length / 1024;
        Console.WriteLine($"  [{kb}KB] {name}");
    }

    private static async Task Pump(int ms)
    {
        await Task.Delay(ms);
        Dispatcher.UIThread.RunJobs();
    }

    private static TabControl? FindTabControl(Visual root)
    {
        if (root is TabControl tc && tc.Classes.Contains("MainConfigTabs"))
            return tc;

        foreach (var child in root.GetVisualDescendants())
        {
            if (child is TabControl tabCtrl && tabCtrl.Classes.Contains("MainConfigTabs"))
                return tabCtrl;
        }

        return null;
    }
}
