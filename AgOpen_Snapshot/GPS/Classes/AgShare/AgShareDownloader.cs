using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgOpenGPS.Properties;
using AgOpenGPS.Core.Models.AgShare;
using AgOpenGPS.Core.Services.AgShare;
using AgLibrary.Logging;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for AgShare download functionality.
    /// Delegates to Core AgShareDownloaderService.
    /// </summary>
    public class CAgShareDownloader
    {
        private readonly AgShareClient client;
        private readonly AgShareDownloaderService downloaderService;

        public CAgShareDownloader()
        {
            // Initialize AgShare client using stored settings
            client = new AgShareClient(Settings.Default.AgShareServer, Settings.Default.AgShareApiKey);
            downloaderService = new AgShareDownloaderService(client.GetCoreClient());
        }

        /// <summary>
        /// Downloads a field and saves it to disk
        /// </summary>
        public async Task<bool> DownloadAndSaveAsync(Guid fieldId)
        {
            try
            {
                var result = await downloaderService.DownloadAndSaveAsync(fieldId, RegistrySettings.fieldsDirectory);

                if (!result.success)
                {
                    Log.EventWriter($"[AgShare] Download failed for fieldId={fieldId}: {result.message}");
                }

                return result.success;
            }
            catch (Exception ex)
            {
                Log.EventWriter($"[AgShare] Download failed for fieldId={fieldId}: {ex.GetType().Name} - {ex.Message}");
                Log.EventWriter(ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Retrieves a list of user-owned fields
        /// </summary>
        public async Task<List<AgShareGetOwnFieldDto>> GetOwnFieldsAsync()
        {
            return await downloaderService.GetOwnFieldsAsync();
        }

        /// <summary>
        /// Downloads a field DTO for preview only
        /// </summary>
        public async Task<AgShareFieldDto> DownloadFieldPreviewAsync(Guid fieldId)
        {
            return await downloaderService.DownloadFieldPreviewAsync(fieldId);
        }

        /// <summary>
        /// Downloads all user fields with progress reporting
        /// </summary>
        public async Task<(int Downloaded, int Skipped)> DownloadAllAsync(
            bool forceOverwrite = false,
            IProgress<int> progress = null)
        {
            return await downloaderService.DownloadAllAsync(
                RegistrySettings.fieldsDirectory,
                forceOverwrite,
                progress
            );
        }
    }
}
