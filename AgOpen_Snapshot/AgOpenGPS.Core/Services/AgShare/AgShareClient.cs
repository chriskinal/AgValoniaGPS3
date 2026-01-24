using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AgOpenGPS.Core.Models.AgShare;

namespace AgOpenGPS.Core.Services.AgShare
{
    /// <summary>
    /// HTTP client for communicating with the AgShare API using API key authentication.
    /// Supports field upload, download, status checks, and querying both public and own fields.
    /// </summary>
    public class AgShareClient
    {
        private HttpClient client;
        private string baseUrl;
        private string apiKey;

        /// <summary>
        /// Constructs client with base URL and API key
        /// </summary>
        public AgShareClient(string serverUrl, string key)
        {
            baseUrl = serverUrl.TrimEnd('/');
            apiKey = key;
            BuildClient();
        }

        /// <summary>
        /// Rebuilds the HttpClient with updated headers
        /// </summary>
        private void BuildClient()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
            client.Timeout = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Updates the API key
        /// </summary>
        public void SetApiKey(string key)
        {
            apiKey = key;
            BuildClient();
        }

        /// <summary>
        /// Updates the base URL
        /// </summary>
        public void SetBaseUrl(string url)
        {
            baseUrl = url.TrimEnd('/');
            BuildClient();
        }

        /// <summary>
        /// Checks if the API key and connection are valid
        /// </summary>
        public async Task<(bool ok, string message)> CheckApiAsync()
        {
            try
            {
                using (var tempClient = new HttpClient())
                {
                    tempClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    tempClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);

                    string requestUrl = $"{baseUrl}/api/fields";
                    var response = await tempClient.GetAsync(requestUrl);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                        return (true, "Connection OK");
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        return (false, "Invalid API key");
                    else
                        return (false, $"Status {response.StatusCode}: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Uploads a field by ID with JSON payload
        /// </summary>
        public async Task<(bool ok, string message)> UploadFieldAsync(Guid fieldId, object fieldPayload)
        {
            try
            {
                var json = JsonConvert.SerializeObject(fieldPayload, Formatting.Indented);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"{baseUrl}/api/fields/{fieldId}", content);

                if (response.IsSuccessStatusCode)
                    return (true, "Upload successful");
                else
                    return (false, $"Upload failed: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a list of fields owned by the current user
        /// </summary>
        public async Task<List<AgShareGetOwnFieldDto>> GetOwnFieldsAsync()
        {
            var url = $"{baseUrl}/api/fields/";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<AgShareGetOwnFieldDto>>(json);
        }

        /// <summary>
        /// Downloads a specific field as raw JSON string
        /// </summary>
        public async Task<string> DownloadFieldAsync(Guid fieldId)
        {
            var response = await client.GetAsync($"{baseUrl}/api/fields/{fieldId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Queries public fields within a given radius around a lat/lon
        /// </summary>
        public async Task<string> GetPublicFieldsAsync(double lat, double lon, double radius = 50)
        {
            var url = $"{baseUrl}/api/fields/public?lat={lat}&lon={lon}&radius={radius}";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
