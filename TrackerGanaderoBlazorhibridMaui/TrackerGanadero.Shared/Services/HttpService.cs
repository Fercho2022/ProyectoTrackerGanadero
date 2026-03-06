using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TrackerGanadero.Shared.Services
{
    public class HttpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public HttpService(HttpClient httpClient, ILogger<HttpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint);
                return await HandleResponse<T>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GET request to {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<T?> PostAsync<T>(string endpoint, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("POST {Endpoint}", endpoint);

                var response = await _httpClient.PostAsync(endpoint, content);
                return await HandleResponse<T>(response);
            }
            catch (TaskCanceledException timeoutEx)
            {
                _logger.LogError(timeoutEx, "Request timeout for POST {Endpoint}", endpoint);
                throw new HttpRequestException($"Request timeout for {endpoint}: {timeoutEx.Message}", timeoutEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in POST request to {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<T?> PutAsync<T>(string endpoint, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(endpoint, content);
                return await HandleResponse<T>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PUT request to {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DELETE request to {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<T?> DeleteAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                return await HandleResponse<T>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DELETE request to {Endpoint}", endpoint);
                throw;
            }
        }

        private async Task<T?> HandleResponse<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP request failed with status {StatusCode}: {Content}",
                    response.StatusCode, content);
                throw new HttpRequestException($"Request failed with status {response.StatusCode}: {content}");
            }

            if (string.IsNullOrEmpty(content))
                return default;

            try
            {
                var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                _logger.LogInformation("Successfully deserialized response to type {TypeName}", typeof(T).Name);
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize response. Content: {Content}", content);
                throw new HttpRequestException($"Failed to deserialize response: {ex.Message}");
            }
        }

        // NOTE: Authorization is now handled automatically by AuthHeaderHandler
        // These methods are no longer needed as they can conflict with AuthHeaderHandler

        /*
        /// <summary>
        /// Sets the Authorization header for HTTP requests
        /// </summary>
        public void SetAuthorizationHeader(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                _logger.LogInformation("Authorization header set successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting authorization header");
                throw;
            }
        }

        /// <summary>
        /// Clears the Authorization header from HTTP requests
        /// </summary>
        public void ClearAuthorizationHeader()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                _logger.LogInformation("Authorization header cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing authorization header");
                throw;
            }
        }
        */
    }
}