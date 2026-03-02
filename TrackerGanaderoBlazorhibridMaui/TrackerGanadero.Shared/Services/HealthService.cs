using TrackerGanadero.Shared.Models;
using Microsoft.Extensions.Logging;

namespace TrackerGanadero.Shared.Services
{
    public class HealthService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<HealthService> _logger;

        public HealthService(HttpService httpService, ILogger<HealthService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<List<HealthRecordDto>> GetHealthRecordsAsync(int? animalId = null)
        {
            try
            {
                var endpoint = animalId.HasValue
                    ? $"api/health/animal/{animalId}"
                    : "api/health";
                var healthRecords = await _httpService.GetAsync<List<HealthRecordDto>>(endpoint);
                return healthRecords ?? new List<HealthRecordDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health records");
                throw;
            }
        }

        public async Task<List<HealthRecordDto>> GetAnimalHealthRecordsAsync(int animalId)
        {
            try
            {
                var healthRecords = await _httpService.GetAsync<List<HealthRecordDto>>($"api/health/animal/{animalId}");
                return healthRecords ?? new List<HealthRecordDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health records for animal {AnimalId}", animalId);
                throw;
            }
        }

        public async Task<HealthRecordDto?> CreateHealthRecordAsync(CreateHealthRecordDto healthRecordDto)
        {
            try
            {
                // Convert RecordDate to UTC if it's Local time
                if (healthRecordDto.RecordDate.Kind == DateTimeKind.Local)
                {
                    healthRecordDto.RecordDate = healthRecordDto.RecordDate.ToUniversalTime();
                }
                else if (healthRecordDto.RecordDate.Kind == DateTimeKind.Unspecified)
                {
                    // Treat unspecified as local time and convert to UTC
                    healthRecordDto.RecordDate = DateTime.SpecifyKind(healthRecordDto.RecordDate, DateTimeKind.Local).ToUniversalTime();
                }

                return await _httpService.PostAsync<HealthRecordDto>("api/health", healthRecordDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating health record");
                throw;
            }
        }

        public async Task<HealthRecordDto?> UpdateHealthRecordAsync(int recordId, CreateHealthRecordDto healthRecordDto)
        {
            try
            {
                // Convert RecordDate to UTC if it's Local time
                if (healthRecordDto.RecordDate.Kind == DateTimeKind.Local)
                {
                    healthRecordDto.RecordDate = healthRecordDto.RecordDate.ToUniversalTime();
                }
                else if (healthRecordDto.RecordDate.Kind == DateTimeKind.Unspecified)
                {
                    // Treat unspecified as local time and convert to UTC
                    healthRecordDto.RecordDate = DateTime.SpecifyKind(healthRecordDto.RecordDate, DateTimeKind.Local).ToUniversalTime();
                }

                return await _httpService.PutAsync<HealthRecordDto>($"api/health/{recordId}", healthRecordDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating health record {RecordId}", recordId);
                throw;
            }
        }

        public async Task<bool> DeleteHealthRecordAsync(int recordId)
        {
            try
            {
                return await _httpService.DeleteAsync($"api/health/{recordId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting health record {RecordId}", recordId);
                throw;
            }
        }

        public async Task<List<HealthRecordDto>> GetUpcomingCheckupsAsync(int farmId, int daysAhead = 30)
        {
            try
            {
                var checkups = await _httpService.GetAsync<List<HealthRecordDto>>($"api/health/farm/{farmId}/upcoming-checkups?daysAhead={daysAhead}");
                return checkups ?? new List<HealthRecordDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting upcoming checkups for farm {FarmId}", farmId);
                throw;
            }
        }

        public async Task<decimal> GetHealthCostsAsync(int farmId, DateTime from, DateTime to)
        {
            try
            {
                var fromStr = from.ToString("yyyy-MM-dd");
                var toStr = to.ToString("yyyy-MM-dd");
                var costs = await _httpService.GetAsync<decimal>($"api/health/farm/{farmId}/costs?from={fromStr}&to={toStr}");
                return costs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health costs for farm {FarmId}", farmId);
                throw;
            }
        }
    }

    public class CreateHealthRecordDto
    {
        public int AnimalId { get; set; }
        public string RecordType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime RecordDate { get; set; }
        public string? VeterinarianName { get; set; }
        public string? Treatment { get; set; }
        public string? Medication { get; set; }
        public decimal? Cost { get; set; }
        public string? Notes { get; set; }
    }
}