using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TrackerGanadero.Shared.Services;

public class TrackerManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TrackerManagementService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TrackerManagementService(HttpClient httpClient, ILogger<TrackerManagementService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<TrackerDto>> GetAvailableTrackersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/TrackerManagement/available");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<TrackerDto>>(json, _jsonOptions) ?? new List<TrackerDto>();
            }
            return new List<TrackerDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available trackers");
            return new List<TrackerDto>();
        }
    }

    public async Task<List<CustomerTrackerDto>> GetMyTrackersAsync()
    {
        try
        {
            _logger.LogInformation("Calling GET /api/trackers/my-trackers");
            var response = await _httpClient.GetAsync("/api/trackers/my-trackers");
            _logger.LogInformation($"Response status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Response JSON: {json}");

                var result = JsonSerializer.Deserialize<List<CustomerTrackerDto>>(json, _jsonOptions) ?? new List<CustomerTrackerDto>();
                _logger.LogInformation($"Parsed {result.Count} trackers");
                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Failed to get trackers: {response.StatusCode} - {errorContent}");
            }
            return new List<CustomerTrackerDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my trackers");
            return new List<CustomerTrackerDto>();
        }
    }

    public async Task<List<TrackerDiscoveryDto>> GetDetectedTrackersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/TrackerManagement/detect-new-public");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<TrackerDiscoveryResponse>(json, _jsonOptions);
                return result?.NewTrackers ?? new List<TrackerDiscoveryDto>();
            }
            return new List<TrackerDiscoveryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving detected trackers");
            return new List<TrackerDiscoveryDto>();
        }
    }

    public async Task<bool> AssignTrackerAsync(int trackerId, int farmId, string? animalName = null)
    {
        try
        {
            var request = new AssignTrackerRequest
            {
                TrackerId = trackerId,
                FarmId = farmId,
                AnimalName = animalName
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/TrackerManagement/assign", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning tracker");
            return false;
        }
    }

    public async Task<bool> RegisterDetectedTrackerAsync(string deviceId, string? name = null, string? serialNumber = null)
    {
        try
        {
            var request = new RegisterDetectedTrackerRequest
            {
                DeviceId = deviceId,
                Name = name,
                SerialNumber = serialNumber
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // SECURITY FIX: Solo registrar el tracker SIN asignarlo automáticamente
            // El tracker debe ser asignado manualmente desde el Panel Admin
            _logger.LogInformation($"Registering tracker {deviceId} WITHOUT auto-assignment");
            var response = await _httpClient.PostAsync("/api/DebugTracker/simple-register", content);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Tracker {deviceId} registered successfully without assignment");
                return true;
            }

            // Fallback: Intentar endpoint original
            _logger.LogWarning($"Fallback: trying original endpoint for {deviceId}");
            response = await _httpClient.PostAsync("/api/TrackerManagement/register-detected", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering detected tracker");
            return false;
        }
    }

}

public class TrackerDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? DeviceId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public bool IsActive { get; set; }
}

public class CustomerTrackerDto
{
    public int Id { get; set; }
    public int TrackerId { get; set; }
    public string? TrackerName { get; set; }
    public string? DeviceId { get; set; }
    public int? FarmId { get; set; }
    public string? FarmName { get; set; }
    public string? AnimalName { get; set; }
    public string? Status { get; set; }
    public DateTime AssignedAt { get; set; }
}

public class TrackerDiscoveryDto
{
    public int Id { get; set; }
    public string? DeviceId { get; set; }
    public string? Name { get; set; }
    public string? Model { get; set; }
    public string? Manufacturer { get; set; }
    public string? SerialNumber { get; set; }
    public int BatteryLevel { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsOnline { get; set; }
    public string? Status { get; set; }
    public DateTime FirstSeen { get; set; }
    public int SignalCount { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
}

public class AssignTrackerRequest
{
    public int TrackerId { get; set; }
    public int FarmId { get; set; }
    public string? AnimalName { get; set; }
}

public class RegisterDetectedTrackerRequest
{
    public string? DeviceId { get; set; }
    public string? Name { get; set; }
    public string? SerialNumber { get; set; }
}

public class TrackerDiscoveryResponse
{
    public bool Success { get; set; }
    public List<TrackerDiscoveryDto> NewTrackers { get; set; } = new();
    public int Count { get; set; }
    public string? Message { get; set; }
}
