// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class HelpDialogPanel : UserControl
{
    public HelpDialogPanel()
    {
        InitializeComponent();
    }

    private void OnGitHubClick(object? sender, RoutedEventArgs e)
        => OpenUrl("https://github.com/AgOpenGPS-Official/AgValoniaGPS");

    private void OnDiscussionsClick(object? sender, RoutedEventArgs e)
        => OpenUrl("https://discourse.agopengps.com/");

    private void OnReleasesClick(object? sender, RoutedEventArgs e)
        => OpenUrl("https://github.com/AgOpenGPS-Official/AgValoniaGPS/releases");

    private void OnYouTubeClick(object? sender, RoutedEventArgs e)
        => OpenUrl("https://www.youtube.com/@AgOpenGPS/videos");

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
            vm.CloseHelpDialogCommand?.Execute(null);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch { }
    }
}
