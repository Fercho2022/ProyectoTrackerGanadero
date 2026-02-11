using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminPanelLicenses.Services
{
    public class AdminApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminApiService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;
        private string? _authToken;

        public AdminApiService(
            IHttpClientFactory httpClientFactory,
            ILogger<AdminApiService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
            _baseUrl = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5192";
        }

        private HttpClient CreateHttpClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_baseUrl);

            if (!string.IsNullOrEmpty(_authToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _authToken);
            }

            return client;
        }

        public void SetAuthToken(string token)
        {
            _authToken = token;
        }

        #region Auth

        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            try
            {
                using var httpClient = CreateHttpClient();
                var response = await httpClient.PostAsJsonAsync("/api/users/login", new
                {
                    username = username,
                    password = password
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result != null && !string.IsNullOrEmpty(result.Token))
                    {
                        SetAuthToken(result.Token);
                    }
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return null;
            }
        }

        #endregion

        #region Users and Licenses

        public async Task<UsersWithLicensesResponse?> GetAllUsersWithLicensesAsync()
        {
            try
            {
                using var httpClient = CreateHttpClient();
                var response = await httpClient.GetAsync("/api/admin/licenses/users");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UsersWithLicensesResponse>();
                }

                _logger.LogWarning("Failed to get users with licenses. Status: {Status}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users with licenses");
                return null;
            }
        }

        public async Task<LicenseStatsResponse?> GetLicenseStatsAsync()
        {
            try
            {
                using var httpClient = CreateHttpClient();
                var response = await httpClient.GetAsync("/api/admin/licenses/stats");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<LicenseStatsResponse>();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting license stats");
                return null;
            }
        }

        public async Task<GenerateLicenseResponse?> GenerateLicenseAsync(GenerateLicenseRequest request)
        {
            try
            {
                using var httpClient = CreateHttpClient();
                var response = await httpClient.PostAsJsonAsync("/api/admin/licenses/generate", request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<GenerateLicenseResponse>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to generate license. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating license");
                return null;
            }
        }

        public async Task<bool> RevokeLicenseAsync(int licenseId)
        {
            try
            {
                using var httpClient = CreateHttpClient();
                var response = await httpClient.PutAsync($"/api/admin/licenses/{licenseId}/revoke", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking license {LicenseId}", licenseId);
                return false;
            }
        }

        /// <summary>
        /// Elimina físicamente una licencia de la base de datos
        /// </summary>
        public async Task<bool> DeleteLicenseAsync(int licenseId)
        {
            try
            {
                using var httpClient = CreateHttpClient();
                var response = await httpClient.DeleteAsync($"/api/admin/licenses/{licenseId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting license {LicenseId}", licenseId);
                return false;
            }
        }

        public async Task<bool> ExtendLicenseAsync(int licenseId, int additionalMonths)
        {
            try
            {
                using var httpClient = CreateHttpClient();
                var response = await httpClient.PutAsJsonAsync(
                    $"/api/admin/licenses/{licenseId}/extend",
                    new { additionalMonths = additionalMonths });

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending license {LicenseId}", licenseId);
                return false;
            }
        }

        #endregion
    }

    #region DTOs

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class UsersWithLicensesResponse
    {
        public bool Success { get; set; }
        public List<UserLicenseInfo> Users { get; set; } = new();
    }

    public class UserLicenseInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool HasLicense { get; set; }
        public int? LicenseId { get; set; }
        public int? CustomerId { get; set; }
        public string? CompanyName { get; set; }
        public string? LicenseType { get; set; }
        public string? Status { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? DaysRemaining { get; set; }
        public bool IsTrial { get; set; }
        public bool IsExpired { get; set; }
        public int? MaxTrackers { get; set; }
        public int? MaxFarms { get; set; }
        public int? ActiveTrackers { get; set; }
    }

    public class LicenseStatsResponse
    {
        public bool Success { get; set; }
        public LicenseStats? Stats { get; set; }
    }

    public class LicenseStats
    {
        public int TotalUsers { get; set; }
        public int UsersWithLicense { get; set; }
        public int UsersWithoutLicense { get; set; }
        public int TrialLicenses { get; set; }
        public int ActiveLicenses { get; set; }
        public int ExpiredLicenses { get; set; }
    }

    public class GenerateLicenseRequest
    {
        public int UserId { get; set; }
        public string LicenseType { get; set; } = "Basic";
        public int DurationMonths { get; set; } = 12;
        public string? CompanyName { get; set; }
    }

    public class GenerateLicenseResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? LicenseKey { get; set; }
        public LicenseInfo? License { get; set; }
    }

    public class LicenseInfo
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public string LicenseType { get; set; } = string.Empty;
        public int MaxTrackers { get; set; }
        public int MaxFarms { get; set; }
        public int MaxUsers { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int DaysValid { get; set; }
    }

    #endregion
}
