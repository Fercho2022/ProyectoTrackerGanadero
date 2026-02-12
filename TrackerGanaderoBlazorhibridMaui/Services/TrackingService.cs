using TrackerGanaderoBlazorHibridMaui.Models;
using Microsoft.Extensions.Logging;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    public class TrackingService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<TrackingService> _logger;

        public TrackingService(HttpService httpService, ILogger<TrackingService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<List<LocationDto>> GetAnimalLocationHistoryAsync(int animalId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = new List<string>();
                if (startDate.HasValue) query.Add($"startDate={startDate:yyyy-MM-ddTHH:mm:ss}");
                if (endDate.HasValue) query.Add($"endDate={endDate:yyyy-MM-ddTHH:mm:ss}");

                var endpoint = $"api/Tracking/animal/{animalId}/history";
                if (query.Any())
                    endpoint += "?" + string.Join("&", query);

                var locations = await _httpService.GetAsync<List<LocationDto>>(endpoint);
                return locations ?? new List<LocationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location history for animal {AnimalId}", animalId);
                throw;
            }
        }

        public async Task<LocationDto?> GetAnimalCurrentLocationAsync(int animalId)
        {
            try
            {
                return await _httpService.GetAsync<LocationDto>($"api/Tracking/animal/{animalId}/current");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current location for animal {AnimalId}", animalId);
                throw;
            }
        }

        public async Task<List<AnimalLocationDto>> GetFarmAnimalsLocationsAsync(int farmId)
        {
            try
            {
                var locations = await _httpService.GetAsync<List<AnimalLocationDto>>($"api/Tracking/farm/{farmId}/animals");
                return locations ?? new List<AnimalLocationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animals locations for farm {FarmId}", farmId);
                throw;
            }
        }

        public async Task<List<AnimalLocationDto>> GetAllAnimalsLocationsAsync()
        {
            try
            {
                var locations = await _httpService.GetAsync<List<AnimalLocationDto>>("api/Tracking/all-animals");
                return locations ?? new List<AnimalLocationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all animals locations");
                throw;
            }
        }

        public async Task<bool> SaveLocationHistoryAsync(SaveLocationHistoryDto locationData)
        {
            try
            {
                var response = await _httpService.PostAsync<object>("api/Tracking/save-location-history", locationData);
                return response != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving location history for animal {AnimalId}", locationData.AnimalId);
                throw;
            }
        }
    }

    public class SaveLocationHistoryDto
    {
        public int AnimalId { get; set; }
        public int TrackerId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; } = 0;
        public double Speed { get; set; }
        public int ActivityLevel { get; set; } = 50;
        public double Temperature { get; set; } = 20; // Default temperature
        public int SignalStrength { get; set; } = 100;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Source { get; set; } = "Frontend";
    }
}