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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Ntrip;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// NTRIP profile management commands.
/// </summary>
public partial class MainViewModel
{
    private void InitializeNtripCommands()
    {
        ShowNtripProfilesDialogCommand = new RelayCommand(() =>
        {
            RefreshNtripProfiles();
            State.UI.ShowDialog(DialogType.NtripProfiles);
        });

        CloseNtripProfilesDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedNtripProfile = null;
        });

        AddNtripProfileCommand = new RelayCommand(() =>
        {
            EditingNtripProfile = _ntripProfileService.CreateNewProfile("New Profile");
            PopulateAvailableFieldsForProfile(EditingNtripProfile);
            State.UI.ShowDialog(DialogType.NtripProfileEditor);
        });

        EditNtripProfileCommand = new RelayCommand(() =>
        {
            if (SelectedNtripProfile != null)
            {
                EditingNtripProfile = new NtripProfile
                {
                    Id = SelectedNtripProfile.Id,
                    Name = SelectedNtripProfile.Name,
                    CasterHost = SelectedNtripProfile.CasterHost,
                    CasterPort = SelectedNtripProfile.CasterPort,
                    MountPoint = SelectedNtripProfile.MountPoint,
                    Username = SelectedNtripProfile.Username,
                    Password = SelectedNtripProfile.Password,
                    AutoConnectOnFieldLoad = SelectedNtripProfile.AutoConnectOnFieldLoad,
                    IsDefault = SelectedNtripProfile.IsDefault,
                    AssociatedFields = new List<string>(SelectedNtripProfile.AssociatedFields),
                    FilePath = SelectedNtripProfile.FilePath
                };
                PopulateAvailableFieldsForProfile(EditingNtripProfile);
                State.UI.ShowDialog(DialogType.NtripProfileEditor);
            }
        });

        DeleteNtripProfileCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedNtripProfile != null)
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "Delete NTRIP Profile",
                    $"Are you sure you want to delete the profile '{SelectedNtripProfile.Name}'?");
                if (!confirmed) return;

                await _ntripProfileService.DeleteProfileAsync(SelectedNtripProfile.Id);
                RefreshNtripProfiles();
                SelectedNtripProfile = null;
                StatusMessage = "NTRIP profile deleted";
            }
        });

        SetDefaultNtripProfileCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedNtripProfile != null)
            {
                await _ntripProfileService.SetDefaultProfileAsync(SelectedNtripProfile.Id);
                RefreshNtripProfiles();
                StatusMessage = $"Set '{SelectedNtripProfile.Name}' as default NTRIP profile";
            }
        });

        SaveNtripProfileCommand = new AsyncRelayCommand(async () =>
        {
            if (EditingNtripProfile != null)
            {
                EditingNtripProfile.AssociatedFields = AvailableFieldsForProfile
                    .Where(f => f.IsSelected)
                    .Select(f => f.FieldName)
                    .ToList();

                await _ntripProfileService.SaveProfileAsync(EditingNtripProfile);
                RefreshNtripProfiles();
                EditingNtripProfile = null;
                AvailableFieldsForProfile.Clear();
                State.UI.ShowDialog(DialogType.NtripProfiles);
                StatusMessage = "NTRIP profile saved";
            }
        });

        CancelNtripProfileEditCommand = new RelayCommand(() =>
        {
            EditingNtripProfile = null;
            AvailableFieldsForProfile.Clear();
            NtripTestStatus = string.Empty;
            State.UI.ShowDialog(DialogType.NtripProfiles);
        });

        TestNtripConnectionCommand = new AsyncRelayCommand(async () =>
        {
            if (EditingNtripProfile == null) return;
            if (string.IsNullOrWhiteSpace(EditingNtripProfile.CasterHost))
            {
                NtripTestStatus = "Error: Caster host is required";
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingNtripProfile.MountPoint))
            {
                NtripTestStatus = "Error: Mount point is required";
                return;
            }

            IsTestingNtripConnection = true;
            NtripTestStatus = "Testing connection...";

            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                var connectTask = tcpClient.ConnectAsync(
                    EditingNtripProfile.CasterHost,
                    EditingNtripProfile.CasterPort);

                if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
                {
                    if (tcpClient.Connected)
                    {
                        using var stream = tcpClient.GetStream();
                        var request = $"GET /{EditingNtripProfile.MountPoint} HTTP/1.1\r\n" +
                                    $"Host: {EditingNtripProfile.CasterHost}\r\n" +
                                    $"Ntrip-Version: Ntrip/2.0\r\n" +
                                    $"User-Agent: NTRIP AgValoniaGPS/Test\r\n";

                        if (!string.IsNullOrEmpty(EditingNtripProfile.Username))
                        {
                            var credentials = Convert.ToBase64String(
                                System.Text.Encoding.ASCII.GetBytes(
                                    $"{EditingNtripProfile.Username}:{EditingNtripProfile.Password}"));
                            request += $"Authorization: Basic {credentials}\r\n";
                        }
                        request += "\r\n";

                        var requestBytes = System.Text.Encoding.ASCII.GetBytes(request);
                        await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                        var buffer = new byte[1024];
                        stream.ReadTimeout = 3000;
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        var response = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        if (response.Contains("200 OK") || response.Contains("ICY 200"))
                        {
                            NtripTestStatus = "Success: Connected to caster and mount point";
                        }
                        else if (response.Contains("401"))
                        {
                            NtripTestStatus = "Error: Authentication failed (check username/password)";
                        }
                        else if (response.Contains("404"))
                        {
                            NtripTestStatus = "Error: Mount point not found";
                        }
                        else
                        {
                            NtripTestStatus = "Connected to caster (mount point status unknown)";
                        }
                    }
                    else
                    {
                        NtripTestStatus = "Error: Could not connect to caster";
                    }
                }
                else
                {
                    NtripTestStatus = "Error: Connection timed out";
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                NtripTestStatus = $"Error: {ex.Message}";
            }
            catch (Exception ex)
            {
                NtripTestStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsTestingNtripConnection = false;
            }
        });
    }
}
