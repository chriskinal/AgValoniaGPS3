using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;
using System;
using System.IO;
using System.Text.Json;

namespace AgValoniaGPS.Services
{
    /// <summary>
    /// Service for managing application settings persistence using JSON
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "appsettings.json";
        private readonly string _settingsDirectory;
        private readonly string _settingsFilePath;

        public AppSettings Settings { get; private set; }

        public event EventHandler<AppSettings>? SettingsLoaded;
        public event EventHandler<AppSettings>? SettingsSaved;

        public SettingsService()
        {
            // Store settings in Documents/AgValoniaGPS (same as Fields)
            // This works consistently across Desktop, iOS, and Android
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Fallback to Personal if MyDocuments is empty (some platforms)
            if (string.IsNullOrEmpty(documentsPath))
            {
                documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            }

            // Last resort fallback
            if (string.IsNullOrEmpty(documentsPath))
            {
                documentsPath = Environment.CurrentDirectory;
            }

            _settingsDirectory = Path.Combine(documentsPath, "AgValoniaGPS");
            _settingsFilePath = Path.Combine(_settingsDirectory, SettingsFileName);

            // Initialize with defaults
            Settings = new AppSettings();
        }

        public bool Load()
        {
            try
            {

                if (!File.Exists(_settingsFilePath))
                {
                    // First run - use defaults and set up fields directory
                    Settings = new AppSettings { IsFirstRun = true };
                    InitializeFieldsDirectory();
                    return false;
                }

                var json = File.ReadAllText(_settingsFilePath);

                // Use same options as Save to match camelCase property names and handle NaN/Infinity
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                };

                var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json, options);

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    Settings.IsFirstRun = false;
                    Settings.LastRunDate = DateTime.Now;

                    // Ensure fields directory is set
                    if (string.IsNullOrEmpty(Settings.FieldsDirectory))
                    {
                        InitializeFieldsDirectory();
                    }

                    SettingsLoaded?.Invoke(this, Settings);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Settings = new AppSettings();
                return false;
            }
        }

        /// <summary>
        /// Initialize fields directory to default location
        /// </summary>
        private void InitializeFieldsDirectory()
        {
            // Default to Documents/AgValoniaGPS/Fields (cross-platform compatible)
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Settings.FieldsDirectory = Path.Combine(documentsPath, "AgValoniaGPS", "Fields");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(Settings.FieldsDirectory))
            {
                Directory.CreateDirectory(Settings.FieldsDirectory);
            }
        }

        public bool Save()
        {
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(_settingsDirectory))
                {
                    Directory.CreateDirectory(_settingsDirectory);
                }

                // Update last run date
                Settings.LastRunDate = DateTime.Now;

                // Serialize with indentation for readability
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                };

                var json = JsonSerializer.Serialize(Settings, options);

                // Write directly (simpler, works across all platforms)
                File.WriteAllText(_settingsFilePath, json);

                SettingsSaved?.Invoke(this, Settings);
                return true;
            }
            catch (Exception ex)
            {
                // Write error to a debug file for diagnosis
                try
                {
                    var errorPath = Path.Combine(_settingsDirectory, "save_error.txt");
                    File.WriteAllText(errorPath, $"Path: {_settingsFilePath}\nError: {ex.Message}\n{ex.StackTrace}");
                }
                catch { }
                return false;
            }
        }

        public void ResetToDefaults()
        {
            Settings = new AppSettings
            {
                IsFirstRun = false,
                LastRunDate = DateTime.Now
            };
        }

        public string GetSettingsFilePath()
        {
            return _settingsFilePath;
        }
    }
}
