using TrackerGanaderoBlazorHibridMaui.Models;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    public class FarmService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<FarmService> _logger;

        public FarmService(HttpService httpService, ILogger<FarmService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<List<FarmDto>> GetFarmsAsync()
        {
            try
            {
                var farms = await _httpService.GetAsync<List<FarmDto>>("api/farms");
                return farms ?? new List<FarmDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting farms");
                throw;
            }
        }

        public async Task<FarmDto?> GetFarmAsync(int farmId)
        {
            try
            {
                return await _httpService.GetAsync<FarmDto>($"api/farms/{farmId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting farm {FarmId}", farmId);
                throw;
            }
        }

        public async Task<List<PastureDto>> GetFarmPasturesAsync(int farmId)
        {
            try
            {
                var pastures = await _httpService.GetAsync<List<PastureDto>>($"api/farms/{farmId}/pastures");
                return pastures ?? new List<PastureDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pastures for farm {FarmId}", farmId);
                throw;
            }
        }

        public async Task<FarmDto> CreateFarmAsync(FarmDto farm)
        {
            try
            {
                var result = await _httpService.PostAsync<FarmDto>("api/farms", farm);
                return result ?? throw new Exception("Failed to create farm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating farm");
                throw;
            }
        }

        public async Task<FarmDto> UpdateFarmAsync(FarmDto farm)
        {
            try
            {
                // Convert FarmDto to the format expected by the backend
                var updateData = new
                {
                    name = farm.Name,
                    description = farm.Description,
                    address = farm.Description, // Use description as address fallback
                    latitude = farm.Latitude,
                    longitude = farm.Longitude,
                    boundaryCoordinates = farm.BoundaryCoordinates?.Select(coord => new { lat = coord.Lat, lng = coord.Lng }).ToList()
                };

                _logger.LogInformation("Updating farm {FarmId} with data: {UpdateData}", farm.Id, System.Text.Json.JsonSerializer.Serialize(updateData));

                var result = await _httpService.PutAsync<FarmDto>($"api/farms/{farm.Id}", updateData);
                return result ?? farm; // Return original farm if result is null
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating farm {FarmId}", farm.Id);
                throw;
            }
        }

        public async Task<bool> UpdateFarmBoundariesAsync(int farmId, List<LatLngDto> boundaries)
        {
            try
            {
                var result = await _httpService.PutAsync<bool?>($"api/farms/{farmId}/boundaries", boundaries);
                return result ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating boundaries for farm {FarmId}", farmId);
                throw;
            }
        }

        public async Task<bool> DeleteFarmAsync(int farmId)
        {
            try
            {
                return await _httpService.DeleteAsync($"api/farms/{farmId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting farm {FarmId}", farmId);
                throw;
            }
        }
    }
}