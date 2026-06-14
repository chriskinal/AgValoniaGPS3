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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Field management commands - field selection, creation, import.
/// </summary>
public partial class MainViewModel
{
    private void InitializeFieldCommands()
    {
        // Start Work Session Dialog (#349 M3) — replaces the FieldSelection
        // dialog conceptually, but the legacy dialog is left wired to its
        // own button until M5 cleanup so this can revert without breaking
        // the menu.
        ShowStartWorkSessionDialogCommand = new RelayCommand(() =>
        {
            StartWorkSessionDialogVm = new StartWorkSessionDialogViewModel(
                _fieldService,
                _jobService,
                _settingsService,
                _appState,
                close: () => State.UI.CloseDialog(),
                openField: (path, name) => _ = OpenFieldOnlyAsync(path, name),
                openFieldStartingNewJob: (path, name, workType, notes, taskName) =>
                    _ = OpenFieldStartingNewJobAsync(path, name, workType, notes, taskName),
                openFieldResumingJob: (path, name, taskName) =>
                    _ = OpenFieldResumingJobAsync(path, name, taskName),
                confirm: (msg, action) => ShowConfirmationDialog("Delete Job", msg, action),
                confirmWithOption: (title, msg, checkboxLabel, defaultChecked, action) =>
                    ShowConfirmationDialog(title, msg, checkboxLabel, defaultChecked, action));
            StartWorkSessionDialogVm.Refresh();
            OpenChainDialog(DialogType.StartWorkSession);
        });

        CancelStartWorkSessionDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // Resume Job cross-field history dialog (#349 M4).
        ShowResumeJobDialogCommand = new RelayCommand(() =>
        {
            ResumeJobDialogVm = new ResumeJobDialogViewModel(
                _jobService,
                _settingsService,
                close: () => State.UI.CloseDialog(),
                openFieldResumingJob: (path, name, taskName) =>
                    _ = OpenFieldResumingJobAsync(path, name, taskName));
            ResumeJobDialogVm.Refresh();
            OpenChainDialog(DialogType.ResumeJob);
        });

        CancelResumeJobDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // Resume Last Job: one-tap reopen of the most recent job across
        // all fields. Short-circuits the picker when the operator just
        // wants to pick up where they left off.
        ResumeLastJobCommand = new RelayCommand(() =>
        {
            var mostRecent = _jobService.ListAllJobs().FirstOrDefault();
            if (mostRecent == null) return;
            var fieldsRoot = _settingsService.Settings.FieldsDirectory;
            var fieldPath = Path.Combine(fieldsRoot, mostRecent.FieldName);
            _ = OpenFieldResumingJobAsync(fieldPath, mostRecent.FieldName, mostRecent.TaskName);
        });

        // Field Selection Dialog
        ShowFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }
            _fieldSelectionDirectory = fieldsDir;
            PopulateAvailableFields(fieldsDir);
            State.UI.ShowDialog(DialogType.FieldSelection);
        });

        CancelFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedFieldInfo = null;
        });

        ConfirmFieldSelectionDialogCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedFieldInfo == null) return;

            var fieldPath = Path.Combine(_fieldSelectionDirectory, SelectedFieldInfo.Name);
            var fieldName = SelectedFieldInfo.Name;

            // Check if this is a legacy field that will be auto-converted
            bool isLegacy = !File.Exists(Path.Combine(fieldPath, "field.geojson")) &&
                            File.Exists(Path.Combine(fieldPath, "Field.txt"));

            if (isLegacy)
            {
                State.UI.CloseDialog();
                ShowConfirmationDialog(
                    "Import Legacy Field",
                    $"'{fieldName}' uses the legacy AgOpenGPS format. " +
                    "It will be imported and converted to the new format. " +
                    "The original files will be kept. Continue?",
                    () =>
                    {
                        SelectedFieldInfo = null;
                        _ = OpenFieldAsync(fieldPath, fieldName).ContinueWith(_ =>
                            _dispatcher.Post(() =>
                                IsFieldOperationsPanelVisible = false));
                    });
                return;
            }

            State.UI.CloseDialog();
            SelectedFieldInfo = null;

            await OpenFieldAsync(fieldPath, fieldName);
            IsFieldOperationsPanelVisible = false;
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

        // New Field Dialog
        ShowNewFieldDialogCommand = new RelayCommand(() =>
        {
            NewFieldLatitude = Latitude != 0 ? Latitude : 40.7128;
            NewFieldLongitude = Longitude != 0 ? Longitude : -74.0060;
            NewFieldName = string.Empty;
            OpenChainDialog(DialogType.NewField);
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

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var fieldPath = Path.Combine(fieldsDir, NewFieldName);
            if (Directory.Exists(fieldPath))
            {
                StatusMessage = $"Field '{NewFieldName}' already exists";
                return;
            }

            try
            {
                Directory.CreateDirectory(fieldPath);

                // Lat/lon must be written with InvariantCulture (period
                // decimal). FieldPlaneFileService.LoadField parses with
                // InvariantCulture; using current culture here would write
                // "42,03" in locales like fi-FI, the parser would silently
                // reject it, the field would end up with origin (0,0), and
                // FindFieldsNear would drop it from "near me" results.
                var inv = CultureInfo.InvariantCulture;
                var latStr = NewFieldLatitude.ToString("F8", inv);
                var lonStr = NewFieldLongitude.ToString("F8", inv);

                var originFile = Path.Combine(fieldPath, "field.origin");
                File.WriteAllText(originFile, $"{latStr},{lonStr}");

                var fieldTxtPath = Path.Combine(fieldPath, "Field.txt");
                var fieldTxtContent =
                    $"{DateTime.Now.ToString("yyyy-MMM-dd hh:mm:ss tt", inv)}\n" +
                    "$FieldDir\n" +
                    $"{NewFieldName}\n" +
                    "$Offsets\n" +
                    "0,0\n" +
                    "Convergence\n" +
                    "0\n" +
                    "StartFix\n" +
                    $"{latStr},{lonStr}\n";
                File.WriteAllText(fieldTxtPath, fieldTxtContent);

                CurrentFieldName = NewFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                // Set field origin for coordinate transformations
                SetFieldOrigin(NewFieldLatitude, NewFieldLongitude);

                // Create field object and set as active (required for headland/track saving)
                var field = new Field
                {
                    Name = NewFieldName,
                    DirectoryPath = fieldPath,
                    Boundary = null
                };
                _fieldService.SetActiveField(field);

                // Create elevation log header if enabled (#120)
                if (Models.Configuration.ConfigurationStore.Instance.Display.ElevationLogEnabled)
                    _elevationLogService.CreateHeader(fieldPath, NewFieldLatitude, NewFieldLongitude);

                PersistentState.LastOpenedField = NewFieldName;
                _persistentStateService.Save();

                State.UI.CloseDialog();
                IsFieldOperationsPanelVisible = false;
                StatusMessage = $"Created field: {NewFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating field: {ex.Message}";
            }
        });

        // From Existing Field Dialog
        ShowFromExistingFieldDialogCommand = new RelayCommand(() =>
        {
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }
            _fieldSelectionDirectory = fieldsDir;
            PopulateAvailableFields(fieldsDir);

            CopyFlags = true;
            CopyMapping = true;
            CopyHeadland = true;
            CopyLines = true;
            FromExistingFieldName = string.Empty;
            FromExistingSelectedField = null;

            if (AvailableFields.Count > 0)
            {
                FromExistingSelectedField = AvailableFields[0];
            }

            OpenChainDialog(DialogType.FromExistingField);
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

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var sourcePath = Path.Combine(fieldsDir, FromExistingSelectedField.Name);
            var newFieldPath = Path.Combine(fieldsDir, newFieldName);

            if (Directory.Exists(newFieldPath) && newFieldName != FromExistingSelectedField.Name)
            {
                StatusMessage = $"Field '{newFieldName}' already exists";
                return;
            }

            try
            {
                Directory.CreateDirectory(newFieldPath);

                var originFile = Path.Combine(sourcePath, "field.origin");
                if (File.Exists(originFile))
                {
                    File.Copy(originFile, Path.Combine(newFieldPath, "field.origin"), true);
                }

                var boundaryFile = Path.Combine(sourcePath, "boundary.json");
                if (File.Exists(boundaryFile))
                {
                    File.Copy(boundaryFile, Path.Combine(newFieldPath, "boundary.json"), true);
                }

                if (CopyFlags)
                {
                    var flagsFile = Path.Combine(sourcePath, "flags.json");
                    if (File.Exists(flagsFile))
                    {
                        File.Copy(flagsFile, Path.Combine(newFieldPath, "flags.json"), true);
                    }
                }

                if (CopyMapping)
                {
                    var mappingFile = Path.Combine(sourcePath, "mapping.json");
                    if (File.Exists(mappingFile))
                    {
                        File.Copy(mappingFile, Path.Combine(newFieldPath, "mapping.json"), true);
                    }
                }

                if (CopyHeadland)
                {
                    var headlandFile = Path.Combine(sourcePath, "headland.json");
                    if (File.Exists(headlandFile))
                    {
                        File.Copy(headlandFile, Path.Combine(newFieldPath, "headland.json"), true);
                    }
                }

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

                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                PersistentState.LastOpenedField = newFieldName;
                _persistentStateService.Save();

                State.UI.CloseDialog();
                IsFieldOperationsPanelVisible = false;
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

        // KML Import Dialog
        ShowKmlImportDialogCommand = new RelayCommand(() =>
        {
            _kmlImportToExistingField = false;
            PopulateAvailableKmlFiles();
            KmlImportFieldName = string.Empty;
            KmlBoundaryPointCount = 0;
            KmlCenterLatitude = 0;
            KmlCenterLongitude = 0;
            _kmlBoundaryPoints.Clear();
            _kmlParsedPolygons.Clear();
            SelectedKmlFile = null;

            if (AvailableKmlFiles.Count > 0)
            {
                SelectedKmlFile = AvailableKmlFiles[0];
            }

            OpenChainDialog(DialogType.KmlImport);
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

            if (_kmlParsedPolygons.Count == 0 || _kmlBoundaryPoints.Count < 3)
            {
                StatusMessage = "KML file must contain at least 3 boundary points";
                return;
            }

            // Import to existing field mode (opened from boundary panel)
            if (_kmlImportToExistingField)
            {
                ImportKmlToExistingField();
                return;
            }

            // Create new field mode (opened from field creation)
            var newFieldName = KmlImportFieldName.Trim();
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
                Directory.CreateDirectory(newFieldPath);

                var originFile = Path.Combine(newFieldPath, "field.origin");
                File.WriteAllText(originFile, $"{KmlCenterLatitude:F8},{KmlCenterLongitude:F8}");

                var origin = new Wgs84(KmlCenterLatitude, KmlCenterLongitude);
                var sharedProps = new SharedFieldProperties();
                var localPlane = new LocalPlane(origin, sharedProps);

                var boundary = new Boundary();

                // First polygon = outer boundary
                for (int polyIdx = 0; polyIdx < _kmlParsedPolygons.Count; polyIdx++)
                {
                    var polygon = new BoundaryPolygon();
                    foreach (var (lat, lon) in _kmlParsedPolygons[polyIdx])
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        polygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                    }

                    if (polyIdx == 0)
                        boundary.OuterBoundary = polygon;
                    else
                        boundary.InnerBoundaries.Add(polygon);
                }

                _boundaryFileService.SaveBoundary(boundary, newFieldPath);

                // Set field origin so coordinate conversions work
                SetFieldOrigin(KmlCenterLatitude, KmlCenterLongitude);

                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                // Load boundary into map renderer
                SetCurrentBoundary(boundary);
                CenterMapOnBoundary(boundary);

                // Update boundary area stats
                var boundaryAreas = new List<double> { boundary.AreaHectares * 10000 };
                _fieldStatistics.UpdateBoundaryAreas(boundaryAreas);
                OnPropertyChanged(nameof(BoundaryAreaDisplay));

                PersistentState.LastOpenedField = newFieldName;
                _persistentStateService.Save();

                RefreshBoundaryList();
                SetSimulatorCoordinates(_fieldOriginLatitude, _fieldOriginLongitude);

                State.UI.CloseDialog();
                IsFieldOperationsPanelVisible = false;
                var innerCount = _kmlParsedPolygons.Count - 1;
                var innerMsg = innerCount > 0 ? $" ({innerCount} inner boundaries)" : "";
                StatusMessage = $"Imported KML: {newFieldName}{innerMsg}";
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

            OpenChainDialog(DialogType.IsoXmlImport);
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
                Directory.CreateDirectory(newFieldPath);

                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                PersistentState.LastOpenedField = newFieldName;
                _persistentStateService.Save();

                State.UI.CloseDialog();
                IsFieldOperationsPanelVisible = false;
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

        // Field close and resume commands
        CloseFieldCommand = new AsyncRelayCommand(async () =>
        {
            async Task FinishCloseAsync()
            {
                await CloseFieldAsync();

                // Disconnect NTRIP if connected
                if (_ntripService.IsConnected)
                {
                    await _ntripService.DisconnectAsync();
                }

                StatusMessage = "Field closed";
            }

            // Warn before dropping coverage painted with no active job. If the
            // guard shows its prompt, the close runs from one of its buttons.
            if (!TryShowUnsavedCoverageGuard(() => _ = FinishCloseAsync()))
            {
                await FinishCloseAsync();
            }
        });

        // "Drive In" — AgOpen-style nearby-field shortcut. Looks for fields
        // whose origin is within 0.5 km of the operator's current GPS fix
        // (matches AgOpenGPS FormJob.btnInField_Click). One match opens
        // directly; multiple matches go through StartWorkSessionDialog
        // pre-filtered to nearby. Zero matches surface a status message.
        DriveInCommand = new RelayCommand(() =>
        {
            if (Latitude == 0 && Longitude == 0)
            {
                StatusMessage = "No GPS fix — Drive In needs current position";
                return;
            }

            var fieldsRoot = _settingsService.Settings.FieldsDirectory;
            var nearby = _fieldService.FindFieldsNear(fieldsRoot, Latitude, Longitude, maxKm: 0.5);

            if (nearby.Count == 0)
            {
                StatusMessage = "No fields within 0.5 km";
                return;
            }

            if (nearby.Count == 1)
            {
                var only = nearby[0];
                _ = OpenFieldAsync(only.DirectoryPath, only.Name);
                IsFieldOperationsPanelVisible = false;
                return;
            }

            // 2+ — open the picker with the list pre-filtered to nearby.
            StartWorkSessionDialogVm = new StartWorkSessionDialogViewModel(
                _fieldService,
                _jobService,
                _settingsService,
                _appState,
                close: () => State.UI.CloseDialog(),
                openField: (path, name) => _ = OpenFieldOnlyAsync(path, name),
                openFieldStartingNewJob: (path, name, workType, notes, taskName) =>
                    _ = OpenFieldStartingNewJobAsync(path, name, workType, notes, taskName),
                openFieldResumingJob: (path, name, taskName) =>
                    _ = OpenFieldResumingJobAsync(path, name, taskName),
                confirm: (msg, action) => ShowConfirmationDialog("Delete Job", msg, action),
                confirmWithOption: (title, msg, checkboxLabel, defaultChecked, action) =>
                    ShowConfirmationDialog(title, msg, checkboxLabel, defaultChecked, action),
                nearbyMaxKm: 0.5);
            StartWorkSessionDialogVm.Refresh();
            OpenChainDialog(DialogType.StartWorkSession);
        });

        ResumeFieldCommand = new AsyncRelayCommand(async () =>
        {
            var lastField = PersistentState.LastOpenedField;
            if (string.IsNullOrEmpty(lastField))
            {
                StatusMessage = "No previous field to resume";
                return;
            }

            // Get fields directory from settings
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

            // Check if this is a legacy field that will be auto-converted
            bool isLegacy = !File.Exists(Path.Combine(fieldPath, "field.geojson")) &&
                            File.Exists(Path.Combine(fieldPath, "Field.txt"));

            if (isLegacy)
            {
                ShowConfirmationDialog(
                    "Import Legacy Field",
                    $"'{lastField}' uses the legacy AgOpenGPS format. " +
                    "It will be imported and converted to the new format. " +
                    "The original files will be kept. Continue?",
                    () =>
                    {
                        _ = OpenFieldAsync(fieldPath, lastField).ContinueWith(_ =>
                            _dispatcher.Post(() =>
                                IsFieldOperationsPanelVisible = false));
                    });
                return;
            }

            await OpenFieldAsync(fieldPath, lastField);
            IsFieldOperationsPanelVisible = false;
        });
    }
}
