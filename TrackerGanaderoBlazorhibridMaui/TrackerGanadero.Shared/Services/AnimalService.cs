using TrackerGanadero.Shared.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace TrackerGanadero.Shared.Services
{
    public class AnimalService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<AnimalService> _logger;

        public AnimalService(HttpService httpService, ILogger<AnimalService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<List<AnimalDto>> GetAnimalsAsync(int? farmId = null)
        {
            try
            {
                var endpoint = farmId.HasValue ? $"api/animals?farmId={farmId}" : "api/animals";
                var animals = await _httpService.GetAsync<List<AnimalDto>>(endpoint);
                return animals ?? new List<AnimalDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animals");
                throw;
            }
        }

        public async Task<AnimalDto?> GetAnimalAsync(int animalId)
        {
            try
            {
                return await _httpService.GetAsync<AnimalDto>($"api/animals/{animalId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animal {AnimalId}", animalId);
                throw;
            }
        }

        public async Task<AnimalDto?> CreateAnimalAsync(CreateAnimalDto animalDto)
        {
            try
            {
                return await _httpService.PostAsync<AnimalDto>("api/animals", animalDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating animal");
                throw;
            }
        }

        public async Task<bool> UpdateAnimalAsync(int animalId, CreateAnimalDto animalDto)
        {
            try
            {
                await _httpService.PutAsync<object>($"api/animals/{animalId}", animalDto);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating animal {AnimalId}", animalId);
                throw;
            }
        }

        public async Task<bool> DeleteAnimalAsync(int animalId)
        {
            try
            {
                return await _httpService.DeleteAsync($"api/animals/{animalId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting animal {AnimalId}", animalId);
                throw;
            }
        }

        public async Task<List<WeightRecordDto>> GetAnimalWeightHistoryAsync(int animalId)
        {
            try
            {
                var weights = await _httpService.GetAsync<List<WeightRecordDto>>($"api/animals/{animalId}/weight-history");
                return weights ?? new List<WeightRecordDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weight history for animal {AnimalId}", animalId);
                throw;
            }
        }

        public async Task<WeightRecordDto?> AddWeightRecordAsync(int animalId, WeightRecordDto weightRecord)
        {
            try
            {
                return await _httpService.PostAsync<WeightRecordDto>($"api/animals/{animalId}/weight", weightRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding weight record for animal {AnimalId}", animalId);
                throw;
            }
        }
    }

    public class CreateAnimalDto
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        public string Name { get; set; } = string.Empty;

        public string? Tag { get; set; }

        [Required(ErrorMessage = "La fecha de nacimiento es requerida")]
        public DateTime BirthDate { get; set; } = DateTime.Now.AddYears(-1);

        [Required(ErrorMessage = "El género es requerido")]
        public string Gender { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "La raza es requerida")]
        public string Breed { get; set; } = string.Empty;

        [Range(0.1, double.MaxValue, ErrorMessage = "El peso debe ser mayor a 0")]
        public decimal Weight { get; set; }

        [Required(ErrorMessage = "El estado es requerido")]
        public string Status { get; set; } = "Healthy";

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una granja válida")]
        public int FarmId { get; set; }

        public int? TrackerId { get; set; }
    }
}