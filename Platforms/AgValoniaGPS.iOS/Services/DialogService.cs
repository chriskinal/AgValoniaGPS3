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

using System.Threading.Tasks;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.iOS.Services;

/// <summary>
/// iOS-specific dialog service - stub implementation
/// </summary>
public class DialogService : IDialogService
{
    public Task ShowMessageAsync(string title, string message)
    {
        // TODO: Show iOS alert
        System.Diagnostics.Debug.WriteLine($"[Dialog] {title}: {message}");
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        // TODO: Show iOS confirmation alert
        System.Diagnostics.Debug.WriteLine($"[Dialog Confirm] {title}: {message}");
        return Task.FromResult(true);
    }

    public Task ShowDataIODialogAsync()
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] Data I/O - Not implemented on iOS");
        return Task.CompletedTask;
    }

    public Task<(double Latitude, double Longitude)?> ShowSimCoordsDialogAsync(double currentLatitude, double currentLongitude)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] Sim Coords - Not implemented on iOS");
        return Task.FromResult<(double, double)?>(null);
    }

    public Task<DialogFieldSelectionResult?> ShowFieldSelectionDialogAsync(string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine($"[Dialog] Field Selection from {fieldsDirectory} - Not implemented on iOS");
        return Task.FromResult<DialogFieldSelectionResult?>(null);
    }

    public Task<DialogNewFieldResult?> ShowNewFieldDialogAsync(Position currentPosition)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] New Field - Not implemented on iOS");
        return Task.FromResult<DialogNewFieldResult?>(null);
    }

    public Task<DialogFromExistingFieldResult?> ShowFromExistingFieldDialogAsync(string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] From Existing Field - Not implemented on iOS");
        return Task.FromResult<DialogFromExistingFieldResult?>(null);
    }

    public Task<DialogIsoXmlImportResult?> ShowIsoXmlImportDialogAsync(string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] ISO-XML Import - Not implemented on iOS");
        return Task.FromResult<DialogIsoXmlImportResult?>(null);
    }

    public Task<DialogKmlImportResult?> ShowKmlImportDialogAsync(string fieldsDirectory, string? currentFieldPath = null)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] KML Import - Not implemented on iOS");
        return Task.FromResult<DialogKmlImportResult?>(null);
    }

    public Task<DialogAgShareDownloadResult?> ShowAgShareDownloadDialogAsync(string apiKey, string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] AgShare Download - Not implemented on iOS");
        return Task.FromResult<DialogAgShareDownloadResult?>(null);
    }

    public Task<bool> ShowAgShareUploadDialogAsync(string apiKey, string fieldName, string fieldDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] AgShare Upload - Not implemented on iOS");
        return Task.FromResult(false);
    }

    public Task ShowAgShareSettingsDialogAsync()
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] AgShare Settings - Not implemented on iOS");
        return Task.CompletedTask;
    }

    public Task<DialogMapBoundaryResult?> ShowMapBoundaryDialogAsync(double centerLatitude, double centerLongitude)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] Map Boundary - Not implemented on iOS");
        return Task.FromResult<DialogMapBoundaryResult?>(null);
    }

    public Task<double?> ShowNumericInputDialogAsync(string description, double initialValue, double minValue = double.MinValue, double maxValue = double.MaxValue, int decimalPlaces = 2)
    {
        System.Diagnostics.Debug.WriteLine($"[Dialog] Numeric Input: {description} = {initialValue} - Not implemented on iOS");
        return Task.FromResult<double?>(null);
    }
}
