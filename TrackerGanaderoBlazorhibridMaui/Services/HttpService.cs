using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TrackerGanaderoBlazorHibridMaui.Services
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

                Console.WriteLine($"=== HTTP REQUEST DETAILS ===");
                Console.WriteLine($"POST Request to: {_httpClient.BaseAddress}{endpoint}");
                Console.WriteLine($"Full URL: {_httpClient.BaseAddress}{endpoint}");
                Console.WriteLine($"Request Body: {json}");
                Console.WriteLine($"Content-Type: application/json");
                Console.WriteLine($"HttpClient Timeout: {_httpClient.Timeout}");
                Console.WriteLine($"=== SENDING REQUEST ===");

                _logger.LogInformation("POST Request to: {BaseAddress}{Endpoint}", _httpClient.BaseAddress, endpoint);
                _logger.LogInformation("Request Body: {RequestBody}", json);

                var response = await _httpClient.PostAsync(endpoint, content);

                Console.WriteLine($"=== HTTP RESPONSE DETAILS ===");
                Console.WriteLine($"Response Status: {response.StatusCode} ({(int)response.StatusCode})");
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response Content: {responseContent}");
                Console.WriteLine($"Response Headers: {response.Headers}");
                Console.WriteLine($"=== END RESPONSE ===");

                _logger.LogInformation("Response Status: {StatusCode}", response.StatusCode);
                _logger.LogInformation("Response Content: {ResponseContent}", responseContent);

                return await HandleResponse<T>(response);
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"=== HTTP REQUEST EXCEPTION ===");
                Console.WriteLine($"HttpRequestException in POST to {endpoint}:");
                Console.WriteLine($"Message: {httpEx.Message}");
                Console.WriteLine($"Data: {httpEx.Data}");
                Console.WriteLine($"Source: {httpEx.Source}");
                Console.WriteLine($"StackTrace: {httpEx.StackTrace}");
                Console.WriteLine($"=== END HTTP EXCEPTION ===");
                _logger.LogError(httpEx, "HttpRequestException in POST request to {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException timeoutEx)
            {
                Console.WriteLine($"=== TIMEOUT EXCEPTION ===");
                Console.WriteLine($"Request to {endpoint} timed out:");
                Console.WriteLine($"Message: {timeoutEx.Message}");
                Console.WriteLine($"Timeout: {_httpClient.Timeout}");
                Console.WriteLine($"=== END TIMEOUT EXCEPTION ===");
                _logger.LogError(timeoutEx, "Request timeout for {Endpoint}", endpoint);
                throw new HttpRequestException($"Request timeout for {endpoint}: {timeoutEx.Message}", timeoutEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== GENERAL EXCEPTION ===");
                Console.WriteLine($"General exception in POST to {endpoint}:");
                Console.WriteLine($"Type: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                Console.WriteLine($"=== END GENERAL EXCEPTION ===");
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