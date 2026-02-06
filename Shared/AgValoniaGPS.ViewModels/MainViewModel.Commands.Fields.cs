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
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Field management commands - field selection, creation, import.
/// </summary>
public partial class MainViewModel
{
    private void InitializeFieldCommands()
    {
        // Field Selection Dialog
        ShowFieldSelectionDialogCommand = ReactiveCommand.Create(() =>
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

        CancelFieldSelectionDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            SelectedFieldInfo = null;
        });

        ConfirmFieldSelectionDialogCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (SelectedFieldInfo == null) return;

            var fieldPath = Path.Combine(_fieldSelectionDirectory, SelectedFieldInfo.Name);
            var fieldName = SelectedFieldInfo.Name;

            State.UI.CloseDialog();
            SelectedFieldInfo = null;

            await OpenFieldAsync(fieldPath, fieldName);
            IsJobMenuPanelVisible = false;
        });

        DeleteSelectedFieldCommand = ReactiveCommand.Create(() =>
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

        SortFieldsCommand = ReactiveCommand.Create(() =>
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
        ShowNewFieldDialogCommand = ReactiveCommand.Create(() =>
        {
            NewFieldLatitude = Latitude != 0 ? Latitude : 40.7128;
            NewFieldLongitude = Longitude != 0 ? Longitude : -74.0060;
            NewFieldName = string.Empty;
            State.UI.ShowDialog(DialogType.NewField);
        });

        CancelNewFieldDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            NewFieldName = string.Empty;
        });

        ConfirmNewFieldDialogCommand = ReactiveCommand.Create(() =>
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

                var originFile = Path.Combine(fieldPath, "field.origin");
                File.WriteAllText(originFile, $"{NewFieldLatitude:F8},{NewFieldLongitude:F8}");

                var fieldTxtPath = Path.Combine(fieldPath, "Field.txt");
                var fieldTxtContent = $"{DateTime.Now:yyyy-MMM-dd hh:mm:ss tt}\n" +
                                      "$FieldDir\n" +
                                      $"{NewFieldName}\n" +
                                      "$Offsets\n" +
                                      "0,0\n" +
                                      "Convergence\n" +
                                      "0\n" +
                                      "StartFix\n" +
                                      $"{NewFieldLatitude:F8},{NewFieldLongitude:F8}\n";
                File.WriteAllText(fieldTxtPath, fieldTxtContent);

                CurrentFieldName = NewFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;
                _simulatorLocalPlane = null;

                // Set field origin for coordinate transformations
                _fieldOriginLatitude = NewFieldLatitude;
                _fieldOriginLongitude = NewFieldLongitude;

                // Create field object and set as active (required for headland/track saving)
                var field = new Field
                {
                    Name = NewFieldName,
                    DirectoryPath = fieldPath,
                    Boundary = null
                };
                _fieldService.SetActiveField(field);

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

        // From Existing Field Dialog
        ShowFromExistingFieldDialogCommand = ReactiveCommand.Create(() =>
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

            State.UI.ShowDialog(DialogType.FromExistingField);
        });

        CancelFromExistingFieldDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            FromExistingSelectedField = null;
            FromExistingFieldName = string.Empty;
        });

        ConfirmFromExistingFieldDialogCommand = ReactiveCommand.Create(() =>
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
        AppendVehicleNameCommand = ReactiveCommand.Create(() =>
        {
            var vehicleName = Vehicle.VehicleTypeDisplayName;
            if (!string.IsNullOrWhiteSpace(vehicleName))
            {
                FromExistingFieldName = (FromExistingFieldName + " " + vehicleName).Trim();
            }
        });

        AppendDateCommand = ReactiveCommand.Create(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            FromExistingFieldName = (FromExistingFieldName + " " + dateStr).Trim();
        });

        AppendTimeCommand = ReactiveCommand.Create(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            FromExistingFieldName = (FromExistingFieldName + " " + timeStr).Trim();
        });

        BackspaceFieldNameCommand = ReactiveCommand.Create(() =>
        {
            if (FromExistingFieldName.Length > 0)
            {
                FromExistingFieldName = FromExistingFieldName.Substring(0, FromExistingFieldName.Length - 1);
            }
        });

        ToggleCopyFlagsCommand = ReactiveCommand.Create(() => CopyFlags = !CopyFlags);
        ToggleCopyMappingCommand = ReactiveCommand.Create(() => CopyMapping = !CopyMapping);
        ToggleCopyHeadlandCommand = ReactiveCommand.Create(() => CopyHeadland = !CopyHeadland);
        ToggleCopyLinesCommand = ReactiveCommand.Create(() => CopyLines = !CopyLines);

        // KML Import Dialog
        ShowKmlImportDialogCommand = ReactiveCommand.Create(() =>
        {
            PopulateAvailableKmlFiles();
            KmlImportFieldName = string.Empty;
            KmlBoundaryPointCount = 0;
            KmlCenterLatitude = 0;
            KmlCenterLongitude = 0;
            _kmlBoundaryPoints.Clear();
            SelectedKmlFile = null;

            if (AvailableKmlFiles.Count > 0)
            {
                SelectedKmlFile = AvailableKmlFiles[0];
            }

            State.UI.ShowDialog(DialogType.KmlImport);
        });

        CancelKmlImportDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            SelectedKmlFile = null;
            KmlImportFieldName = string.Empty;
        });

        ConfirmKmlImportDialogCommand = ReactiveCommand.Create(() =>
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
                Directory.CreateDirectory(newFieldPath);

                var originFile = Path.Combine(newFieldPath, "field.origin");
                File.WriteAllText(originFile, $"{KmlCenterLatitude:F8},{KmlCenterLongitude:F8}");

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

        KmlAppendDateCommand = ReactiveCommand.Create(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            KmlImportFieldName = (KmlImportFieldName + " " + dateStr).Trim();
        });

        KmlAppendTimeCommand = ReactiveCommand.Create(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            KmlImportFieldName = (KmlImportFieldName + " " + timeStr).Trim();
        });

        KmlBackspaceFieldNameCommand = ReactiveCommand.Create(() =>
        {
            if (KmlImportFieldName.Length > 0)
            {
                KmlImportFieldName = KmlImportFieldName.Substring(0, KmlImportFieldName.Length - 1);
            }
        });

        // ISO-XML Import Dialog
        ShowIsoXmlImportDialogCommand = ReactiveCommand.Create(() =>
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

        CancelIsoXmlImportDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            SelectedIsoXmlFile = null;
            IsoXmlImportFieldName = string.Empty;
        });

        ConfirmIsoXmlImportDialogCommand = ReactiveCommand.Create(() =>
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

        IsoXmlAppendDateCommand = ReactiveCommand.Create(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            IsoXmlImportFieldName = (IsoXmlImportFieldName + " " + dateStr).Trim();
        });

        IsoXmlAppendTimeCommand = ReactiveCommand.Create(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            IsoXmlImportFieldName = (IsoXmlImportFieldName + " " + timeStr).Trim();
        });

        IsoXmlBackspaceFieldNameCommand = ReactiveCommand.Create(() =>
        {
            if (IsoXmlImportFieldName.Length > 0)
            {
                IsoXmlImportFieldName = IsoXmlImportFieldName.Substring(0, IsoXmlImportFieldName.Length - 1);
            }
        });

        // Field close and resume commands
        CloseFieldCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await CloseFieldAsync();

            // Disconnect NTRIP if connected
            if (_ntripService.IsConnected)
            {
                await _ntripService.DisconnectAsync();
            }

            StatusMessage = "Field closed";
        });

        DriveInCommand = ReactiveCommand.Create(() =>
        {
            // Start a new field at current GPS position
            if (Latitude != 0 && Longitude != 0)
            {
                StatusMessage = "Drive-in field started";
            }
        });

        ResumeFieldCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var lastField = _settingsService.Settings.LastOpenedField;
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

            await OpenFieldAsync(fieldPath, lastField);
            IsJobMenuPanelVisible = false;
        });
    }
}
