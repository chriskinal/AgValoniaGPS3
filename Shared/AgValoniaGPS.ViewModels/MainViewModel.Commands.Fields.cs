using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing field management command initialization.
/// </summary>
public partial class MainViewModel
{
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
                Console.WriteLine($"[Field] Could not load Field.txt origin: {ex.Message}");
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
                Simulator.ResetLocalPlane();

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

        // Field name helper commands
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

        // Field Commands (Close, DriveIn, Resume)
        CloseFieldCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen) return;

            // Clear field state
            CurrentFieldName = string.Empty;
            FieldsRootDirectory = string.Empty;
            IsFieldOpen = false;
            _fieldOriginLatitude = 0;
            _fieldOriginLongitude = 0;

            // Clear boundary
            SetCurrentBoundary(null);

            // Clear headland
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
            HasHeadland = false;
            IsHeadlandOn = false;

            // Clear tracks
            Tracks.SelectedTrack = null;

            // Clear the active field in service
            _fieldService.SetActiveField(null);

            // Clear background image
            _mapService.SetBackgroundImage(null, 0, 0, 0, 0);

            StatusMessage = "Field closed";
        });

        DriveInCommand = new RelayCommand(() =>
        {
            StatusMessage = "Drive-In mode not yet implemented";
        });

        ResumeFieldCommand = new RelayCommand(() =>
        {
            // Resume the last opened field
            var lastField = _settingsService.Settings.LastOpenedField;
            if (string.IsNullOrEmpty(lastField))
            {
                StatusMessage = "No previous field to resume";
                return;
            }

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
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

            // Load the field
            FieldsRootDirectory = fieldsDir;
            CurrentFieldName = lastField;
            IsFieldOpen = true;

            // Load field origin from Field.txt
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
                Console.WriteLine($"[Field] Could not load Field.txt origin: {ex.Message}");
            }

            // Try to load boundary
            var boundary = _boundaryFileService.LoadBoundary(fieldPath);
            if (boundary != null)
            {
                SetCurrentBoundary(boundary);
                CenterMapOnBoundary(boundary);
            }

            // Try to load background image
            LoadBackgroundImage(fieldPath, boundary);

            // Set the active field
            var field = new Field
            {
                Name = lastField,
                DirectoryPath = fieldPath,
                Boundary = boundary
            };
            _fieldService.SetActiveField(field);

            IsJobMenuPanelVisible = false;
            StatusMessage = $"Resumed field: {lastField}";
        });

        InitializeKmlCommands();
        InitializeIsoXmlCommands();
    }

    private void InitializeKmlCommands()
    {
        // KML Import Dialog
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
    }

    private void InitializeIsoXmlCommands()
    {
        // ISO-XML Import Dialog
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
    }
}
