using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AdminPanelLicenses.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(HttpClient httpClient, IConfiguration configuration, ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5192";
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                _logger.LogInformation($"GET {endpoint}");
                var response = await _httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<T>(content, _jsonOptions);
                }

                _logger.LogWarning($"GET {endpoint} failed with status {response.StatusCode}");
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GET {endpoint}");
                throw;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                _logger.LogInformation($"POST {endpoint}");
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<TResponse>(responseContent, _jsonOptions);
                }

                _logger.LogWarning($"POST {endpoint} failed with status {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Error response: {errorContent}");
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in POST {endpoint}");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            try
            {
                _logger.LogInformation($"DELETE {endpoint}");
                var response = await _httpClient.DeleteAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in DELETE {endpoint}");
                throw;
            }
        }
    }
}
