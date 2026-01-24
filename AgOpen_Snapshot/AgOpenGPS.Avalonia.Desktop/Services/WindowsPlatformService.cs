using AgOpenGPS.Core.Interfaces.Services;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AgOpenGPS.Avalonia.Desktop.Services
{
    /// <summary>
    /// Windows-specific implementation of platform services.
    /// </summary>
    public class WindowsPlatformService : IPlatformService
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        private readonly Window? _mainWindow;

        public WindowsPlatformService(Window? mainWindow = null)
        {
            _mainWindow = mainWindow;
        }

        public string GetConfigurationPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configPath = Path.Combine(appData, "AgOpenGPS");

            if (!Directory.Exists(configPath))
                Directory.CreateDirectory(configPath);

            return configPath;
        }

        public string GetDataPath()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dataPath = Path.Combine(documents, "AgOpenGPS");

            if (!Directory.Exists(dataPath))
                Directory.CreateDirectory(dataPath);

            return dataPath;
        }

        public bool BringWindowToFront(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    var handle = processes[0].MainWindowHandle;
                    ShowWindow(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                    return true;
                }
            }
            catch
            {
                // Ignore errors
            }

            return false;
        }

        public IEnumerable<string> GetSerialPorts()
        {
            try
            {
                return SerialPort.GetPortNames().OrderBy(p => p);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        public string? ShowOpenFileDialog(string title, string filter, string initialDirectory)
        {
            if (_mainWindow == null)
                return null;

            // Avalonia file dialogs are async, so we need to use Task.Run
            var task = Task.Run(async () =>
            {
                var storageProvider = _mainWindow.StorageProvider;
                if (storageProvider == null)
                    return null;

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                };

                // Parse filter (e.g., "Text files|*.txt|All files|*.*")
                if (!string.IsNullOrEmpty(filter))
                {
                    options.FileTypeFilter = ParseFileFilter(filter);
                }

                var result = await storageProvider.OpenFilePickerAsync(options);
                return result.FirstOrDefault()?.Path.LocalPath;
            });

            return task.Result;
        }

        public string? ShowSaveFileDialog(string title, string filter, string initialDirectory)
        {
            if (_mainWindow == null)
                return null;

            var task = Task.Run(async () =>
            {
                var storageProvider = _mainWindow.StorageProvider;
                if (storageProvider == null)
                    return null;

                var options = new FilePickerSaveOptions
                {
                    Title = title
                };

                if (!string.IsNullOrEmpty(filter))
                {
                    options.FileTypeChoices = ParseFileFilter(filter);
                }

                var result = await storageProvider.SaveFilePickerAsync(options);
                return result?.Path.LocalPath;
            });

            return task.Result;
        }

        private List<FilePickerFileType> ParseFileFilter(string filter)
        {
            var result = new List<FilePickerFileType>();

            // Parse Windows-style filter: "Text files|*.txt|All files|*.*"
            var parts = filter.Split('|');
            for (int i = 0; i < parts.Length - 1; i += 2)
            {
                var name = parts[i];
                var patterns = parts[i + 1].Split(';').Select(p => p.Trim()).ToArray();

                result.Add(new FilePickerFileType(name)
                {
                    Patterns = patterns
                });
            }

            return result;
        }
    }
}