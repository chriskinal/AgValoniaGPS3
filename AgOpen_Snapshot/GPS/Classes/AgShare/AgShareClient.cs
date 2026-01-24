using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgOpenGPS.Core.Models.AgShare;
using AgOpenGPS.Core.Services.AgShare;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for AgShareClient from AgOpenGPS.Core.
    /// All operations delegate to the Core implementation.
    /// </summary>
    public class AgShareClient
    {
        private readonly Core.Services.AgShare.AgShareClient _coreClient;

        /// <summary>
        /// Constructs client with base URL and API key
        /// </summary>
        public AgShareClient(string serverUrl, string key)
        {
            _coreClient = new Core.Services.AgShare.AgShareClient(serverUrl, key);
        }

        /// <summary>
        /// Updates the API key
        /// </summary>
        public void SetApiKey(string key)
        {
            _coreClient.SetApiKey(key);
        }

        /// <summary>
        /// Updates the base URL
        /// </summary>
        public void SetBaseUrl(string url)
        {
            _coreClient.SetBaseUrl(url);
        }

        /// <summary>
        /// Checks if the API key and connection are valid
        /// </summary>
        public async Task<(bool ok, string message)> CheckApiAsync()
        {
            return await _coreClient.CheckApiAsync();
        }

        /// <summary>
        /// Uploads a field by ID with JSON payload
        /// </summary>
        public async Task<(bool ok, string message)> UploadFieldAsync(Guid fieldId, object fieldPayload)
        {
            return await _coreClient.UploadFieldAsync(fieldId, fieldPayload);
        }

        /// <summary>
        /// Retrieves a list of fields owned by the current user
        /// </summary>
        public async Task<List<AgShareGetOwnFieldDto>> GetOwnFieldsAsync()
        {
            return await _coreClient.GetOwnFieldsAsync();
        }

        /// <summary>
        /// Downloads a specific field as raw JSON string
        /// </summary>
        public async Task<string> DownloadFieldAsync(Guid fieldId)
        {
            return await _coreClient.DownloadFieldAsync(fieldId);
        }

        /// <summary>
        /// Queries public fields within a given radius around a lat/lon
        /// </summary>
        public async Task<string> GetPublicFieldsAsync(double lat, double lon, double radius = 50)
        {
            return await _coreClient.GetPublicFieldsAsync(lat, lon, radius);
        }

        /// <summary>
        /// Gets the underlying Core client for advanced operations
        /// </summary>
        public Core.Services.AgShare.AgShareClient GetCoreClient()
        {
            return _coreClient;
        }
    }
}
