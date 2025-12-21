using System.Threading.Tasks;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Android.Services;

/// <summary>
/// Android-specific dialog service - stub implementation
/// </summary>
public class DialogService : IDialogService
{
    public Task ShowMessageAsync(string title, string message)
    {
        // TODO: Show Android alert dialog
        System.Diagnostics.Debug.WriteLine($"[Dialog] {title}: {message}");
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        // TODO: Show Android confirmation dialog
        System.Diagnostics.Debug.WriteLine($"[Dialog Confirm] {title}: {message}");
        return Task.FromResult(true);
    }

    public Task ShowDataIODialogAsync()
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] Data I/O - Not implemented on Android");
        return Task.CompletedTask;
    }

    public Task<(double Latitude, double Longitude)?> ShowSimCoordsDialogAsync(double currentLatitude, double currentLongitude)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] Sim Coords - Not implemented on Android");
        return Task.FromResult<(double, double)?>(null);
    }

    public Task<DialogFieldSelectionResult?> ShowFieldSelectionDialogAsync(string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine($"[Dialog] Field Selection from {fieldsDirectory} - Not implemented on Android");
        return Task.FromResult<DialogFieldSelectionResult?>(null);
    }

    public Task<DialogNewFieldResult?> ShowNewFieldDialogAsync(Position currentPosition)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] New Field - Not implemented on Android");
        return Task.FromResult<DialogNewFieldResult?>(null);
    }

    public Task<DialogFromExistingFieldResult?> ShowFromExistingFieldDialogAsync(string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] From Existing Field - Not implemented on Android");
        return Task.FromResult<DialogFromExistingFieldResult?>(null);
    }

    public Task<DialogIsoXmlImportResult?> ShowIsoXmlImportDialogAsync(string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] ISO-XML Import - Not implemented on Android");
        return Task.FromResult<DialogIsoXmlImportResult?>(null);
    }

    public Task<DialogKmlImportResult?> ShowKmlImportDialogAsync(string fieldsDirectory, string? currentFieldPath = null)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] KML Import - Not implemented on Android");
        return Task.FromResult<DialogKmlImportResult?>(null);
    }

    public Task<DialogAgShareDownloadResult?> ShowAgShareDownloadDialogAsync(string apiKey, string fieldsDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] AgShare Download - Not implemented on Android");
        return Task.FromResult<DialogAgShareDownloadResult?>(null);
    }

    public Task<bool> ShowAgShareUploadDialogAsync(string apiKey, string fieldName, string fieldDirectory)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] AgShare Upload - Not implemented on Android");
        return Task.FromResult(false);
    }

    public Task ShowAgShareSettingsDialogAsync()
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] AgShare Settings - Not implemented on Android");
        return Task.CompletedTask;
    }

    public Task<DialogMapBoundaryResult?> ShowMapBoundaryDialogAsync(double centerLatitude, double centerLongitude)
    {
        System.Diagnostics.Debug.WriteLine("[Dialog] Map Boundary - Not implemented on Android");
        return Task.FromResult<DialogMapBoundaryResult?>(null);
    }

    public Task<double?> ShowNumericInputDialogAsync(string description, double initialValue, double minValue = double.MinValue, double maxValue = double.MaxValue, int decimalPlaces = 2)
    {
        System.Diagnostics.Debug.WriteLine($"[Dialog] Numeric Input: {description} = {initialValue} - Not implemented on Android");
        return Task.FromResult<double?>(null);
    }
}
