using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Field management commands - field selection, creation, opening, closing, import dialogs
/// </summary>
public partial class MainViewModel
{
    // Field Selection Dialog Commands
    public ICommand? ShowFieldSelectionDialogCommand { get; private set; }
    public ICommand? CancelFieldSelectionDialogCommand { get; private set; }
    public ICommand? ConfirmFieldSelectionDialogCommand { get; private set; }
    public ICommand? DeleteSelectedFieldCommand { get; private set; }
    public ICommand? SortFieldsCommand { get; private set; }

    // New Field Dialog Commands
    public ICommand? ShowNewFieldDialogCommand { get; private set; }
    public ICommand? CancelNewFieldDialogCommand { get; private set; }
    public ICommand? ConfirmNewFieldDialogCommand { get; private set; }

    // From Existing Field Dialog Commands
    public ICommand? ShowFromExistingFieldDialogCommand { get; private set; }
    public ICommand? CancelFromExistingFieldDialogCommand { get; private set; }
    public ICommand? ConfirmFromExistingFieldDialogCommand { get; private set; }
    public ICommand? AppendVehicleNameCommand { get; private set; }
    public ICommand? AppendDateCommand { get; private set; }
    public ICommand? AppendTimeCommand { get; private set; }
    public ICommand? BackspaceFieldNameCommand { get; private set; }
    public ICommand? ToggleCopyFlagsCommand { get; private set; }
    public ICommand? ToggleCopyMappingCommand { get; private set; }
    public ICommand? ToggleCopyHeadlandCommand { get; private set; }
    public ICommand? ToggleCopyLinesCommand { get; private set; }

    // KML Import Dialog Commands
    public ICommand? ShowKmlImportDialogCommand { get; private set; }
    public ICommand? CancelKmlImportDialogCommand { get; private set; }
    public ICommand? ConfirmKmlImportDialogCommand { get; private set; }
    public ICommand? KmlAppendDateCommand { get; private set; }
    public ICommand? KmlAppendTimeCommand { get; private set; }
    public ICommand? KmlBackspaceFieldNameCommand { get; private set; }

    // ISO-XML Import Dialog Commands
    public ICommand? ShowIsoXmlImportDialogCommand { get; private set; }
    public ICommand? CancelIsoXmlImportDialogCommand { get; private set; }
    public ICommand? ConfirmIsoXmlImportDialogCommand { get; private set; }
    public ICommand? IsoXmlAppendDateCommand { get; private set; }
    public ICommand? IsoXmlAppendTimeCommand { get; private set; }
    public ICommand? IsoXmlBackspaceFieldNameCommand { get; private set; }

    // Field Operations Commands
    public ICommand? CloseFieldCommand { get; private set; }
    public ICommand? DriveInCommand { get; private set; }
    public ICommand? ResumeFieldCommand { get; private set; }

    private void InitializeFieldCommands()
    {
        // Field Selection Dialog Commands
        ShowFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            // Use settings directory which defaults to ~/Documents/AgValoniaGPS/Fields
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            System.Diagnostics.Debug.WriteLine($"[FieldSelection] Settings.FieldsDirectory = '{fieldsDir}'");
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
                System.Diagnostics.Debug.WriteLine($"[FieldSelection] Using fallback path: '{fieldsDir}'");
            }
            _fieldSelectionDirectory = fieldsDir;
            System.Diagnostics.Debug.WriteLine($"[FieldSelection] Directory exists: {Directory.Exists(fieldsDir)}");

            // Populate the available fields list
            PopulateAvailableFields(fieldsDir);
            System.Diagnostics.Debug.WriteLine($"[FieldSelection] Found {AvailableFields.Count} fields");

            // Show the panel-based dialog
            State.UI.ShowDialog(DialogType.FieldSelection);
        });

        CancelFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedFieldInfo = null;
        });

        ConfirmFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            if (SelectedFieldInfo == null) return;

            var fieldPath = Path.Combine(_fieldSelectionDirectory, SelectedFieldInfo.Name);
            FieldsRootDirectory = _fieldSelectionDirectory;
            CurrentFieldName = SelectedFieldInfo.Name;
            IsFieldOpen = true;

            // Save as last opened field
            _settingsService.Settings.LastOpenedField = SelectedFieldInfo.Name;
            _settingsService.Save();

            // Load field origin from Field.txt (for map centering)
            try
            {
                var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                if (fieldInfo.Origin != null)
                {
                    _fieldOriginLatitude = fieldInfo.Origin.Latitude;
                    _fieldOriginLongitude = fieldInfo.Origin.Longitude;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[Field] Could not load Field.txt origin: {ex.Message}");
            }

            // Try to load boundary from field
            var boundary = _boundaryFileService.LoadBoundary(fieldPath);
            if (boundary != null)
            {
                SetCurrentBoundary(boundary);
                CenterMapOnBoundary(boundary);
            }

            // Try to load background image from field
            LoadBackgroundImage(fieldPath, boundary);

            // Set the active field so headland and other field-specific data loads
            var field = new Field
            {
                Name = SelectedFieldInfo.Name,
                DirectoryPath = fieldPath,
                Boundary = boundary
            };
            _fieldService.SetActiveField(field);

            // Handle NTRIP profile for this field
            _ = HandleNtripProfileForFieldAsync(SelectedFieldInfo.Name);

            State.UI.CloseDialog();
            IsJobMenuPanelVisible = false;
            StatusMessage = $"Opened field: {SelectedFieldInfo.Name}";
            SelectedFieldInfo = null;
        });

        DeleteSelectedFieldCommand = new RelayCommand(() =>
        {
            if (SelectedFieldInfo == null) return;

            var fieldPath = Path.Combine(_fieldSelectionDirectory, SelectedFieldInfo.Name);
            try
            {
                if (Directory.Exists(fieldPath))
                {
                    Directory.Delete(fieldPath, true);
                    StatusMessage = $"Deleted field: {SelectedFieldInfo.Name}";
                    PopulateAvailableFields(_fieldSelectionDirectory);
                    SelectedFieldInfo = null;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting field: {ex.Message}";
            }
        });

        SortFieldsCommand = new RelayCommand(() =>
        {
            _fieldsSortedAZ = !_fieldsSortedAZ;
            var sorted = _fieldsSortedAZ
                ? AvailableFields.OrderBy(f => f.Name).ToList()
                : AvailableFields.OrderByDescending(f => f.Name).ToList();
            AvailableFields.Clear();
            foreach (var field in sorted)
            {
                AvailableFields.Add(field);
            }
        });

        // New Field Dialog Commands
        ShowNewFieldDialogCommand = new RelayCommand(() =>
        {
            // Initialize with current GPS position or defaults
            NewFieldLatitude = Latitude != 0 ? Latitude : 40.7128;
            NewFieldLongitude = Longitude != 0 ? Longitude : -74.0060;
            NewFieldName = string.Empty;
            State.UI.ShowDialog(DialogType.NewField);
        });

        CancelNewFieldDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            NewFieldName = string.Empty;
        });

        ConfirmNewFieldDialogCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(NewFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            // Get the fields directory
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            // Create the field directory
            var fieldPath = Path.Combine(fieldsDir, NewFieldName);
            if (Directory.Exists(fieldPath))
            {
                StatusMessage = $"Field '{NewFieldName}' already exists";
                return;
            }

            try
            {
                Directory.CreateDirectory(fieldPath);

                // Save the field origin coordinates
                var originFile = Path.Combine(fieldPath, "field.origin");
                File.WriteAllText(originFile, $"{NewFieldLatitude},{NewFieldLongitude}");

                // Create Field.txt in AgOpenGPS format
                var fieldTxtPath = Path.Combine(fieldPath, "Field.txt");
                var fieldTxtContent = $"{DateTime.Now:yyyy-MMM-dd hh:mm:ss tt}\n" +
                                      "$FieldDir\n" +
                                      $"{NewFieldName}\n" +
                                      "$Offsets\n" +
                                      "0,0\n" +
                                      "Convergence\n" +
                                      "0\n" +
                                      "StartFix\n" +
                                      $"{NewFieldLatitude},{NewFieldLongitude}\n";
                File.WriteAllText(fieldTxtPath, fieldTxtContent);

                // Set as current field
                CurrentFieldName = NewFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                // Reset LocalPlane so it will be recreated with new origin
                _simulatorLocalPlane = null;

                // Save as last opened field
                _settingsService.Settings.LastOpenedField = NewFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Created field: {NewFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating field: {ex.Message}";
            }
        });

        // From Existing Field Dialog Commands
        ShowFromExistingFieldDialogCommand = new RelayCommand(() =>
        {
            // Populate fields list (reuse same list as field selection)
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }
            _fieldSelectionDirectory = fieldsDir;
            PopulateAvailableFields(fieldsDir);

            // Reset copy options
            CopyFlags = true;
            CopyMapping = true;
            CopyHeadland = true;
            CopyLines = true;
            FromExistingFieldName = string.Empty;
            FromExistingSelectedField = null;

            // Pre-select first field if available
            if (AvailableFields.Count > 0)
            {
                FromExistingSelectedField = AvailableFields[0];
            }

            State.UI.ShowDialog(DialogType.FromExistingField);
        });

        CancelFromExistingFieldDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            FromExistingSelectedField = null;
            FromExistingFieldName = string.Empty;
        });

        ConfirmFromExistingFieldDialogCommand = new RelayCommand(() =>
        {
            if (FromExistingSelectedField == null)
            {
                StatusMessage = "Please select a field to copy from";
                return;
            }

            var newFieldName = FromExistingFieldName.Trim();
            if (string.IsNullOrWhiteSpace(newFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            // Get the fields directory
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var sourcePath = Path.Combine(fieldsDir, FromExistingSelectedField.Name);
            var newFieldPath = Path.Combine(fieldsDir, newFieldName);

            // Check if field already exists (unless same name as source)
            if (Directory.Exists(newFieldPath) && newFieldName != FromExistingSelectedField.Name)
            {
                StatusMessage = $"Field '{newFieldName}' already exists";
                return;
            }

            try
            {
                // Create the new field directory
                Directory.CreateDirectory(newFieldPath);

                // Copy field.origin if exists
                var originFile = Path.Combine(sourcePath, "field.origin");
                if (File.Exists(originFile))
                {
                    File.Copy(originFile, Path.Combine(newFieldPath, "field.origin"), true);
                }

                // Copy boundary
                var boundaryFile = Path.Combine(sourcePath, "boundary.json");
                if (File.Exists(boundaryFile))
                {
                    File.Copy(boundaryFile, Path.Combine(newFieldPath, "boundary.json"), true);
                }

                // Copy flags if enabled
                if (CopyFlags)
                {
                    var flagsFile = Path.Combine(sourcePath, "flags.json");
                    if (File.Exists(flagsFile))
                    {
                        File.Copy(flagsFile, Path.Combine(newFieldPath, "flags.json"), true);
                    }
                }

                // Copy mapping if enabled
                if (CopyMapping)
                {
                    var mappingFile = Path.Combine(sourcePath, "mapping.json");
                    if (File.Exists(mappingFile))
                    {
                        File.Copy(mappingFile, Path.Combine(newFieldPath, "mapping.json"), true);
                    }
                }

                // Copy headland if enabled
                if (CopyHeadland)
                {
                    var headlandFile = Path.Combine(sourcePath, "headland.json");
                    if (File.Exists(headlandFile))
                    {
                        File.Copy(headlandFile, Path.Combine(newFieldPath, "headland.json"), true);
                    }
                }

                // Copy lines if enabled
                if (CopyLines)
                {
                    var linesFile = Path.Combine(sourcePath, "lines.json");
                    if (File.Exists(linesFile))
                    {
                        File.Copy(linesFile, Path.Combine(newFieldPath, "lines.json"), true);
                    }
                    var abLinesFile = Path.Combine(sourcePath, "ablines.json");
                    if (File.Exists(abLinesFile))
                    {
                        File.Copy(abLinesFile, Path.Combine(newFieldPath, "ablines.json"), true);
                    }
                }

                // Set as current field
                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                // Save as last opened field
                _settingsService.Settings.LastOpenedField = newFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Created field from existing: {newFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating field: {ex.Message}";
            }
        });

        AppendVehicleNameCommand = new RelayCommand(() =>
        {
            var vehicleName = Vehicle.VehicleTypeDisplayName;
            if (!string.IsNullOrWhiteSpace(vehicleName))
            {
                FromExistingFieldName = (FromExistingFieldName + " " + vehicleName).Trim();
            }
        });

        AppendDateCommand = new RelayCommand(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            FromExistingFieldName = (FromExistingFieldName + " " + dateStr).Trim();
        });

        AppendTimeCommand = new RelayCommand(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            FromExistingFieldName = (FromExistingFieldName + " " + timeStr).Trim();
        });

        BackspaceFieldNameCommand = new RelayCommand(() =>
        {
            if (FromExistingFieldName.Length > 0)
            {
                FromExistingFieldName = FromExistingFieldName.Substring(0, FromExistingFieldName.Length - 1);
            }
        });

        ToggleCopyFlagsCommand = new RelayCommand(() => CopyFlags = !CopyFlags);
        ToggleCopyMappingCommand = new RelayCommand(() => CopyMapping = !CopyMapping);
        ToggleCopyHeadlandCommand = new RelayCommand(() => CopyHeadland = !CopyHeadland);
        ToggleCopyLinesCommand = new RelayCommand(() => CopyLines = !CopyLines);

        // KML Import Dialog Commands
        ShowKmlImportDialogCommand = new RelayCommand(() =>
        {
            PopulateAvailableKmlFiles();
            KmlImportFieldName = string.Empty;
            KmlBoundaryPointCount = 0;
            KmlCenterLatitude = 0;
            KmlCenterLongitude = 0;
            _kmlBoundaryPoints.Clear();
            SelectedKmlFile = null;

            // Pre-select first file if available
            if (AvailableKmlFiles.Count > 0)
            {
                SelectedKmlFile = AvailableKmlFiles[0];
            }

            State.UI.ShowDialog(DialogType.KmlImport);
        });

        CancelKmlImportDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedKmlFile = null;
            KmlImportFieldName = string.Empty;
        });

        ConfirmKmlImportDialogCommand = new RelayCommand(() =>
        {
            if (SelectedKmlFile == null)
            {
                StatusMessage = "Please select a KML file";
                return;
            }

            var newFieldName = KmlImportFieldName.Trim();
            if (string.IsNullOrWhiteSpace(newFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            if (_kmlBoundaryPoints.Count < 3)
            {
                StatusMessage = "KML file must contain at least 3 boundary points";
                return;
            }

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var newFieldPath = Path.Combine(fieldsDir, newFieldName);
            if (Directory.Exists(newFieldPath))
            {
                StatusMessage = $"Field '{newFieldName}' already exists";
                return;
            }

            try
            {
                // Create field directory
                Directory.CreateDirectory(newFieldPath);

                // Save origin coordinates
                var originFile = Path.Combine(newFieldPath, "field.origin");
                File.WriteAllText(originFile, $"{KmlCenterLatitude},{KmlCenterLongitude}");

                // Create and save boundary
                var origin = new Wgs84(KmlCenterLatitude, KmlCenterLongitude);
                var sharedProps = new SharedFieldProperties();
                var localPlane = new LocalPlane(origin, sharedProps);

                var outerPolygon = new BoundaryPolygon();
                foreach (var (lat, lon) in _kmlBoundaryPoints)
                {
                    var wgs84 = new Wgs84(lat, lon);
                    var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                    outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                }

                var boundary = new Boundary { OuterBoundary = outerPolygon };
                _boundaryFileService.SaveBoundary(boundary, newFieldPath);

                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                _settingsService.Settings.LastOpenedField = newFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Imported KML: {newFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing KML: {ex.Message}";
            }
        });

        KmlAppendDateCommand = new RelayCommand(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            KmlImportFieldName = (KmlImportFieldName + " " + dateStr).Trim();
        });

        KmlAppendTimeCommand = new RelayCommand(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            KmlImportFieldName = (KmlImportFieldName + " " + timeStr).Trim();
        });

        KmlBackspaceFieldNameCommand = new RelayCommand(() =>
        {
            if (KmlImportFieldName.Length > 0)
            {
                KmlImportFieldName = KmlImportFieldName.Substring(0, KmlImportFieldName.Length - 1);
            }
        });

        // ISO-XML Import Dialog Commands
        ShowIsoXmlImportDialogCommand = new RelayCommand(() =>
        {
            PopulateAvailableIsoXmlFiles();
            IsoXmlImportFieldName = string.Empty;
            SelectedIsoXmlFile = null;

            if (AvailableIsoXmlFiles.Count > 0)
            {
                SelectedIsoXmlFile = AvailableIsoXmlFiles[0];
            }

            State.UI.ShowDialog(DialogType.IsoXmlImport);
        });

        CancelIsoXmlImportDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedIsoXmlFile = null;
            IsoXmlImportFieldName = string.Empty;
        });

        ConfirmIsoXmlImportDialogCommand = new RelayCommand(() =>
        {
            if (SelectedIsoXmlFile == null)
            {
                StatusMessage = "Please select an ISO-XML folder";
                return;
            }

            var newFieldName = IsoXmlImportFieldName.Trim();
            if (string.IsNullOrWhiteSpace(newFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var newFieldPath = Path.Combine(fieldsDir, newFieldName);
            if (Directory.Exists(newFieldPath))
            {
                StatusMessage = $"Field '{newFieldName}' already exists";
                return;
            }

            try
            {
                // TODO: Implement ISO-XML parsing when needed
                // For now, just create the field directory
                Directory.CreateDirectory(newFieldPath);

                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                _settingsService.Settings.LastOpenedField = newFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Imported ISO-XML: {newFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing ISO-XML: {ex.Message}";
            }
        });

        IsoXmlAppendDateCommand = new RelayCommand(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            IsoXmlImportFieldName = (IsoXmlImportFieldName + " " + dateStr).Trim();
        });

        IsoXmlAppendTimeCommand = new RelayCommand(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            IsoXmlImportFieldName = (IsoXmlImportFieldName + " " + timeStr).Trim();
        });

        IsoXmlBackspaceFieldNameCommand = new RelayCommand(() =>
        {
            if (IsoXmlImportFieldName.Length > 0)
            {
                IsoXmlImportFieldName = IsoXmlImportFieldName.Substring(0, IsoXmlImportFieldName.Length - 1);
            }
        });

        // Field Operations Commands
        CloseFieldCommand = new AsyncRelayCommand(async () =>
        {
            CurrentFieldName = string.Empty;
            IsFieldOpen = false;
            SetCurrentBoundary(null);

            // Clear headland
            LoadHeadlandFromField(null);

            // Clear tracks
            State.Field.Tracks.Clear();
            SavedTracks.Clear();
            SelectedTrack = null;

            // Disconnect NTRIP if connected
            if (_ntripService.IsConnected)
            {
                await _ntripService.DisconnectAsync();
            }

            // Clear field service state
            _fieldService.SetActiveField(null);

            StatusMessage = "Field closed";
        });

        DriveInCommand = new RelayCommand(() =>
        {
            // Start a new field at current GPS position
            if (Latitude != 0 && Longitude != 0)
            {
                StatusMessage = "Drive-in field started";
            }
        });

        ResumeFieldCommand = new RelayCommand(() =>
        {
            var lastField = _settingsService.Settings.LastOpenedField;
            if (string.IsNullOrEmpty(lastField))
            {
                StatusMessage = "No previous field to resume";
                return;
            }

            // Get fields directory from settings (same pattern as ConfirmFieldSelectionDialogCommand)
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrEmpty(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var fieldPath = Path.Combine(fieldsDir, lastField);

            if (!Directory.Exists(fieldPath))
            {
                StatusMessage = $"Field not found: {lastField}";
                return;
            }

            try
            {
                FieldsRootDirectory = fieldsDir;
                CurrentFieldName = lastField;
                IsFieldOpen = true;

                // Load field origin from Field.txt (for coordinate conversions)
                try
                {
                    var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                    if (fieldInfo.Origin != null)
                    {
                        _fieldOriginLatitude = fieldInfo.Origin.Latitude;
                        _fieldOriginLongitude = fieldInfo.Origin.Longitude;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"[Field] Could not load Field.txt origin: {ex.Message}");
                }

                // Load boundary from field (same pattern as ConfirmFieldSelectionDialogCommand)
                var boundary = _boundaryFileService.LoadBoundary(fieldPath);
                if (boundary != null)
                {
                    SetCurrentBoundary(boundary);
                    CenterMapOnBoundary(boundary);

                    // Debug: show boundary extents
                    if (boundary.OuterBoundary?.Points.Count > 0)
                    {
                        var pts = boundary.OuterBoundary.Points;
                        double minE = pts.Min(p => p.Easting);
                        double maxE = pts.Max(p => p.Easting);
                        double minN = pts.Min(p => p.Northing);
                        double maxN = pts.Max(p => p.Northing);
                        _logger.LogDebug($"[Boundary] Extents (local): E({minE:F1} to {maxE:F1}), N({minN:F1} to {maxN:F1})");
                    }
                }

                // Load background image from field
                LoadBackgroundImage(fieldPath, boundary);

                // Set the active field so headland and other field-specific data loads
                var field = new Field
                {
                    Name = lastField,
                    DirectoryPath = fieldPath,
                    Boundary = boundary
                };
                _fieldService.SetActiveField(field);

                // Handle NTRIP profile for this field
                _ = HandleNtripProfileForFieldAsync(lastField);

                IsJobMenuPanelVisible = false;
                StatusMessage = $"Resumed field: {lastField}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load field: {ex.Message}";
            }
        });
    }
}
