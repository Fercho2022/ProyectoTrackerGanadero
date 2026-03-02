using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TrackerGanadero.Shared.Services;

public class LicenseService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LicenseService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public LicenseService(HttpClient httpClient, ILogger<LicenseService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Debug: Log the BaseAddress configuration
        _logger.LogInformation($"LicenseService HttpClient BaseAddress: {_httpClient.BaseAddress}");
        Debug.WriteLine($"[LicenseService] HttpClient BaseAddress: {_httpClient.BaseAddress}");
    }

    public async Task<LicenseActivationResult> ActivateLicenseAsync(string licenseKey, string? hardwareId = null)
    {
        try
        {
            // Verificar que el HttpClient esté disponible
            if (_httpClient == null)
            {
                return new LicenseActivationResult
                {
                    IsSuccess = false,
                    Message = "HttpClient no configurado"
                };
            }

            var request = new ActivateLicenseRequest
            {
                LicenseKey = licenseKey,
                HardwareId = hardwareId ?? Environment.MachineName
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Usar URI del endpoint simplificado que evita problemas de Entity Framework
            var uri = "api/TestLicense/activate-simple";
            _logger.LogInformation($"Making request to: {uri}");
            _logger.LogInformation($"HttpClient BaseAddress: {_httpClient.BaseAddress}");
            _logger.LogInformation($"Full URL: {_httpClient.BaseAddress}{uri}");
            _logger.LogInformation($"Request payload: {json}");

            // Configurar timeout para la petición
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            _logger.LogInformation("Sending HTTP POST request...");
            Debug.WriteLine("[LicenseService] Sending HTTP POST request...");
            var response = await _httpClient.PostAsync(uri, content, cts.Token);
            _logger.LogInformation($"Response received. Status: {response.StatusCode}");
            Debug.WriteLine($"[LicenseService] Response received. Status: {response.StatusCode}");

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Response body: {responseJson}");
            Debug.WriteLine($"[LicenseService] Response body: {responseJson}");

            _logger.LogInformation($"Response Status: {response.StatusCode}");
            _logger.LogInformation($"Response Content: {responseJson}");

            if (response.IsSuccessStatusCode)
            {
                // La API devuelve {success: true, message: "...", license: {}, customer: {}}
                Debug.WriteLine("[LicenseService] Attempting to deserialize response...");
                var apiResponse = JsonSerializer.Deserialize<ApiLicenseResponse>(responseJson, _jsonOptions);
                Debug.WriteLine($"[LicenseService] Deserialized success: {apiResponse?.Success}, message: {apiResponse?.Message}");

                // Después de activación exitosa, cargar info del customer
                CustomerInfoDto? customerInfo = null;
                if (apiResponse?.Success == true && apiResponse.Customer != null)
                {
                    try
                    {
                        var customerJson = JsonSerializer.Serialize(apiResponse.Customer, _jsonOptions);
                        customerInfo = JsonSerializer.Deserialize<CustomerInfoDto>(customerJson, _jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not deserialize customer info from activation response");
                    }
                }

                return new LicenseActivationResult
                {
                    IsSuccess = apiResponse?.Success ?? false,
                    Message = apiResponse?.Message ?? "Error desconocido",
                    Customer = customerInfo
                };
            }
            else
            {
                // En caso de error, la API devuelve {success: false, message: "..."}
                var errorResponse = JsonSerializer.Deserialize<ApiLicenseResponse>(responseJson, _jsonOptions);
                return new LicenseActivationResult
                {
                    IsSuccess = false,
                    Message = errorResponse?.Message ?? $"Error HTTP {response.StatusCode}: {responseJson}"
                };
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP Request error activating license");
            _logger.LogError($"HttpClient BaseAddress: {_httpClient.BaseAddress}");
            _logger.LogError($"Exception details: {httpEx}");
            Debug.WriteLine($"[LicenseService] HTTP Request error: {httpEx.Message}");
            Debug.WriteLine($"[LicenseService] HttpClient BaseAddress: {_httpClient.BaseAddress}");
            Debug.WriteLine($"[LicenseService] Full exception: {httpEx}");
            return new LicenseActivationResult
            {
                IsSuccess = false,
                Message = $"Error de conexión: {httpEx.Message}. Base Address: {_httpClient.BaseAddress}. Verifica que la API esté ejecutándose."
            };
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            _logger.LogError(tcEx, "Timeout activating license");
            return new LicenseActivationResult
            {
                IsSuccess = false,
                Message = "Timeout: La petición tardó demasiado tiempo"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating license");
            return new LicenseActivationResult
            {
                IsSuccess = false,
                Message = $"Error inesperado: {ex.Message}"
            };
        }
    }

    public async Task<List<LicenseDto>> GetMyLicensesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/License/my-licenses");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<LicenseDto>>(json, _jsonOptions) ?? new List<LicenseDto>();
            }
            return new List<LicenseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my licenses");
            return new List<LicenseDto>();
        }
    }

    public async Task<CustomerInfoDto?> GetCustomerInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/License/customer-info");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();

                // Primero deserializar a un objeto dinámico para verificar hasCustomer
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Si hasCustomer es false, devolver null (sin licencia)
                if (root.TryGetProperty("hasCustomer", out var hasCustomerProp) &&
                    !hasCustomerProp.GetBoolean())
                {
                    _logger.LogInformation("No customer/license found - API returned hasCustomer=false");
                    return null;
                }

                // Si hasCustomer es true, deserializar el objeto customer
                if (root.TryGetProperty("customer", out var customerProp))
                {
                    return JsonSerializer.Deserialize<CustomerInfoDto>(customerProp.GetRawText(), _jsonOptions);
                }

                return null;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer info");
            return null;
        }
    }
}

public class ActivateLicenseRequest
{
    public string? LicenseKey { get; set; }
    public string? HardwareId { get; set; }
}

public class LicenseActivationResult
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public CustomerInfoDto? Customer { get; set; }
}

public class LicenseDto
{
    public int Id { get; set; }
    public string? LicenseKey { get; set; }
    public string? Plan { get; set; }
    public int TrackerLimit { get; set; }
    public string? Status { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ActivatedAt { get; set; }
}

public class CustomerInfoDto
{
    public int Id { get; set; }
    public string? CompanyName { get; set; }
    public string? Plan { get; set; }
    public int TrackerLimit { get; set; }
    public string? Status { get; set; }
    public int CurrentTrackerCount { get; set; }
    public bool CanAddMoreTrackers { get; set; }
}

public class ApiLicenseResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? License { get; set; }
    public object? Customer { get; set; }
}