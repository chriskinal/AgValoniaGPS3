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
using System.Threading.Tasks;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing NTRIP connection management.
/// Handles NTRIP caster connections, RTCM data reception, and profile management.
/// </summary>
public partial class MainViewModel
{
    #region NTRIP Fields

    private bool _isNtripConnected;
    private string _ntripStatus = "Not Connected";
    private ulong _ntripBytesReceived;
    private string _ntripCasterAddress = "rtk2go.com";
    private int _ntripCasterPort = 2101;
    private string _ntripMountPoint = "";
    private string _ntripUsername = "";
    private string _ntripPassword = "";

    #endregion

    #region NTRIP Properties

    public bool IsNtripConnected
    {
        get => _isNtripConnected;
        set => SetProperty(ref _isNtripConnected, value);
    }

    public string NtripStatus
    {
        get => _ntripStatus;
        set => SetProperty(ref _ntripStatus, value);
    }

    public string NtripBytesReceived
    {
        get => $"{(_ntripBytesReceived / 1024):N0} KB";
    }

    public string NtripCasterAddress
    {
        get => _ntripCasterAddress;
        set => SetProperty(ref _ntripCasterAddress, value);
    }

    public int NtripCasterPort
    {
        get => _ntripCasterPort;
        set => SetProperty(ref _ntripCasterPort, value);
    }

    public string NtripMountPoint
    {
        get => _ntripMountPoint;
        set => SetProperty(ref _ntripMountPoint, value);
    }

    public string NtripUsername
    {
        get => _ntripUsername;
        set => SetProperty(ref _ntripUsername, value);
    }

    public string NtripPassword
    {
        get => _ntripPassword;
        set => SetProperty(ref _ntripPassword, value);
    }

    #endregion

    #region NTRIP Connection Methods

    public async Task ConnectToNtripAsync()
    {
        try
        {
            var config = new NtripConfiguration
            {
                CasterAddress = NtripCasterAddress,
                CasterPort = NtripCasterPort,
                MountPoint = NtripMountPoint,
                Username = NtripUsername,
                Password = NtripPassword,
                SubnetAddress = "192.168.5",
                UdpForwardPort = 2233,
                GgaIntervalSeconds = 10,
                UseManualPosition = false
            };

            await _ntripService.ConnectAsync(config);
        }
        catch (Exception ex)
        {
            NtripStatus = $"Error: {ex.Message}";
        }
    }

    public async Task DisconnectFromNtripAsync()
    {
        await _ntripService.DisconnectAsync();
    }

    /// <summary>
    /// Handles NTRIP profile connection when a field is loaded.
    /// Checks for field-specific profile or falls back to default profile.
    /// </summary>
    private async Task HandleNtripProfileForFieldAsync(string fieldName)
    {
        try
        {
            var profile = _ntripProfileService.GetProfileForField(fieldName);

            if (profile == null)
            {
                _logger.LogDebug("No NTRIP profile found for field '{FieldName}' (no default set)", fieldName);
                return;
            }

            if (!profile.AutoConnectOnFieldLoad)
            {
                _logger.LogDebug("NTRIP profile '{ProfileName}' has auto-connect disabled", profile.Name);
                return;
            }

            // Disconnect from current caster if connected
            if (_ntripService.IsConnected)
            {
                _logger.LogDebug("Disconnecting from current NTRIP caster");
                await _ntripService.DisconnectAsync();
            }

            // Update UI properties for display
            NtripCasterAddress = profile.CasterHost;
            NtripCasterPort = profile.CasterPort;
            NtripMountPoint = profile.MountPoint;
            NtripUsername = profile.Username;
            NtripPassword = profile.Password;

            // Connect to new caster
            _logger.LogInformation("Connecting to NTRIP profile '{ProfileName}' for field '{FieldName}'", profile.Name, fieldName);
            await ConnectToNtripAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NTRIP profile for field '{FieldName}'", fieldName);
        }
    }

    #endregion

    #region NTRIP Event Handlers

    private void OnNtripConnectionChanged(object? sender, NtripConnectionEventArgs e)
    {
        // Marshal to UI thread (use Invoke for synchronous execution to avoid modal dialog issues)
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateNtripConnectionProperties(e);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateNtripConnectionProperties(e));
        }
    }

    private void UpdateNtripConnectionProperties(NtripConnectionEventArgs e)
    {
        // Update centralized state
        State.Connections.IsNtripConnected = e.IsConnected;
        State.Connections.NtripStatus = e.Message ?? (e.IsConnected ? "Connected" : "Not Connected");

        // Legacy property updates
        IsNtripConnected = e.IsConnected;
        NtripStatus = e.Message ?? (e.IsConnected ? "Connected" : "Not Connected");
    }

    private void OnRtcmDataReceived(object? sender, RtcmDataReceivedEventArgs e)
    {
        // Marshal to UI thread (use Invoke for synchronous execution to avoid modal dialog issues)
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateNtripDataProperties();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateNtripDataProperties());
        }
    }

    private void UpdateNtripDataProperties()
    {
        // Update centralized state
        State.Connections.NtripBytesReceived = _ntripService.TotalBytesReceived;

        // Legacy property updates
        _ntripBytesReceived = _ntripService.TotalBytesReceived;
        OnPropertyChanged(nameof(NtripBytesReceived));
    }

    #endregion
}
