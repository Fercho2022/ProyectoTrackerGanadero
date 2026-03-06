using TrackerGanadero.Shared.Models;
using TrackerGanadero.Shared.Services;
using Microsoft.Extensions.Logging;

namespace TrackerGanadero.Shared.Services
{
    /// <summary>
    /// Servicio para manejar la integración entre gestión de granjas y asignación de trackers
    /// </summary>
    public class FarmTrackerService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<FarmTrackerService> _logger;

        public FarmTrackerService(HttpService httpService, ILogger<FarmTrackerService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene los trackers disponibles del usuario para asignar a animales
        /// SECURITY FIX: Usa solo el endpoint correcto que devuelve CustomerTrackers reales
        /// </summary>
        public async Task<List<AvailableFarmTrackerDto>> GetAvailableTrackersAsync()
        {
            try
            {
                // SECURITY FIX: Solo usar el endpoint correcto que devuelve CustomerTrackers
                // NO usar detect-new-public que devuelve Trackers sin CustomerTracker
                _logger.LogInformation("Getting available trackers from /api/FarmTracker/available");
                var response = await _httpService.GetAsync<FarmTrackerResponse>("api/FarmTracker/available");
                var trackers = response?.trackers ?? new List<AvailableFarmTrackerDto>();
                _logger.LogInformation($"Found {trackers.Count} available CustomerTrackers for farm management");
                return trackers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available farm trackers");
                throw;
            }
        }

        /// <summary>
        /// Asigna un tracker a un animal específico
        /// </summary>
        public async Task<bool> AssignTrackerToAnimalAsync(int customerTrackerId, int animalId)
        {
            try
            {
                var request = new AssignTrackerToAnimalRequest
                {
                    CustomerTrackerId = customerTrackerId,
                    AnimalId = animalId
                };

                var response = await _httpService.PostAsync<ApiResponse>("api/FarmTracker/assign-to-animal", request);
                return response?.success ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning tracker {CustomerTrackerId} to animal {AnimalId}",
                    customerTrackerId, animalId);
                throw;
            }
        }

        /// <summary>
        /// Desasigna un tracker de un animal
        /// </summary>
        public async Task<bool> UnassignTrackerFromAnimalAsync(int animalId)
        {
            try
            {
                var response = await _httpService.PostAsync<ApiResponse>($"api/FarmTracker/unassign-from-animal/{animalId}", null);
                return response?.success ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning tracker from animal {AnimalId}", animalId);
                throw;
            }
        }

        /// <summary>
        /// Obtiene información del tracker asignado a un animal
        /// </summary>
        public async Task<AnimalTrackerInfoDto?> GetAnimalTrackerInfoAsync(int animalId)
        {
            try
            {
                var response = await _httpService.GetAsync<AnimalTrackerResponse>($"api/FarmTracker/animal/{animalId}/tracker");
                return response?.hasTracker == true ? response.tracker : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracker info for animal {AnimalId}", animalId);
                throw;
            }
        }

        /// <summary>
        /// Obtiene todos los animales de una granja con su información de tracker en una sola consulta
        /// OPTIMIZACIÓN: Elimina el problema N+1 de hacer 120+ requests individuales
        /// </summary>
        public async Task<Dictionary<int, AnimalTrackerInfoDto?>> GetFarmAnimalsWithTrackersAsync(int farmId)
        {
            try
            {
                _logger.LogInformation("Getting all animals with trackers for farm {FarmId} in single request", farmId);
                var response = await _httpService.GetAsync<FarmAnimalsWithTrackersResponse>($"api/FarmTracker/farm/{farmId}/animals-with-trackers");

                if (response?.success == true && response.animals != null)
                {
                    // Convert list to dictionary for easy lookup by animal ID using LINQ
                    var result = response.animals.ToDictionary(a => a.AnimalId, a => a.TrackerInfo);
                    _logger.LogInformation("Retrieved tracker info for {Count} animals in single request", result.Count);
                    return result;
                }

                return new Dictionary<int, AnimalTrackerInfoDto?>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animals with trackers for farm {FarmId}", farmId);
                throw;
            }
        }

        /// <summary>
        /// Asignación masiva: vincula trackers libres con animales sin tracker en orden numérico
        /// </summary>
        public async Task<BulkAssignResultDto> BulkAssignTrackersAsync(int farmId)
        {
            try
            {
                _logger.LogInformation("Bulk assign trackers for farm {FarmId}", farmId);
                var response = await _httpService.PostAsync<BulkAssignResultDto>($"api/FarmTracker/bulk-assign/{farmId}", null);
                return response ?? new BulkAssignResultDto { Message = "Error en la respuesta del servidor" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk assign for farm {FarmId}", farmId);
                throw;
            }
        }

        /// <summary>
        /// Limpia trackers inactivos (sin transmisión en los últimos 2 minutos) de una granja
        /// </summary>
        public async Task<CleanupResponse> CleanupInactiveTrackersAsync(int farmId)
        {
            try
            {
                var response = await _httpService.PostAsync<CleanupResponse>($"api/FarmTracker/cleanup-inactive/{farmId}", null);
                return response ?? new CleanupResponse { success = false, message = "Error en la respuesta del servidor" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up inactive trackers for farm {FarmId}", farmId);
                throw;
            }
        }

        /// <summary>
        /// Elimina un tracker específico de la base de datos
        /// </summary>
        public async Task<ApiResponse> DeleteTrackerAsync(int trackerId)
        {
            try
            {
                var response = await _httpService.DeleteAsync<ApiResponse>($"api/FarmTracker/delete-tracker/{trackerId}");
                return response ?? new ApiResponse { success = false, message = "Error en la respuesta del servidor" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tracker {TrackerId}", trackerId);
                throw;
            }
        }

        /// <summary>
        /// Elimina todos los trackers de la base de datos
        /// </summary>
        public async Task<DeleteAllResponse> DeleteAllTrackersAsync()
        {
            try
            {
                var response = await _httpService.DeleteAsync<DeleteAllResponse>("api/FarmTracker/delete-all-trackers");
                return response ?? new DeleteAllResponse { success = false, message = "Error en la respuesta del servidor" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all trackers");
                throw;
            }
        }
    }

    /// <summary>
    /// Respuesta para operación de eliminación de todos los trackers
    /// </summary>
    public class DeleteAllResponse
    {
        public bool success { get; set; }
        public int deletedCount { get; set; }
        public string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta para operación de limpieza de trackers
    /// </summary>
    public class CleanupResponse
    {
        public bool success { get; set; }
        public int cleanedCount { get; set; }
        public string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta de la API para trackers disponibles en granjas
    /// </summary>
    public class FarmTrackerResponse
    {
        public bool success { get; set; }
        public List<AvailableFarmTrackerDto> trackers { get; set; } = new();
        public int count { get; set; }
        public string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta de la API para información de tracker de animal
    /// </summary>
    public class AnimalTrackerResponse
    {
        public bool success { get; set; }
        public bool hasTracker { get; set; }
        public AnimalTrackerInfoDto? tracker { get; set; }
        public string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request para asignar tracker a animal
    /// </summary>
    public class AssignTrackerToAnimalRequest
    {
        public int CustomerTrackerId { get; set; }
        public int AnimalId { get; set; }
    }

    /// <summary>
    /// Respuesta genérica de la API
    /// </summary>
    public class ApiResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para trackers disponibles para asignación en granjas
    /// </summary>
    public class AvailableFarmTrackerDto
    {
        public int CustomerTrackerId { get; set; }
        public int TrackerId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string TrackerName { get; set; } = string.Empty;
        public string? CustomName { get; set; }
        public string Model { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public DateTime AssignedAt { get; set; }

        public string DisplayName => !string.IsNullOrEmpty(CustomName) ? CustomName : TrackerName;
        public string StatusText => IsOnline ? "En línea" : "Desconectado";
        public string LastSeenText => $"Visto: {LastSeen:dd/MM/yyyy HH:mm}";
        public string BatteryText => $"Batería: {BatteryLevel}%";
    }

    /// <summary>
    /// DTO para información del tracker asignado a un animal
    /// </summary>
    public class AnimalTrackerInfoDto
    {
        public int AnimalId { get; set; }
        public int CustomerTrackerId { get; set; }
        public int TrackerId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string TrackerName { get; set; } = string.Empty;
        public string? CustomName { get; set; }
        public string Model { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public DateTime AssignedAt { get; set; }

        public string DisplayName => !string.IsNullOrEmpty(CustomName) ? CustomName : TrackerName;
        public string StatusText => IsOnline ? "En línea" : "Desconectado";
        public string LastSeenText => $"Visto: {LastSeen:dd/MM/yyyy HH:mm}";
        public string BatteryText => $"Batería: {BatteryLevel}%";
    }

    /// <summary>
    /// DTO para animal con información de su tracker (optimizado para consultas masivas)
    /// </summary>
    public class AnimalWithTrackerDto
    {
        public int AnimalId { get; set; }
        public string AnimalName { get; set; } = string.Empty;
        public string? AnimalTag { get; set; }
        public AnimalTrackerInfoDto? TrackerInfo { get; set; }
    }

    /// <summary>
    /// Respuesta de la API para animales con trackers de una granja
    /// </summary>
    public class FarmAnimalsWithTrackersResponse
    {
        public bool success { get; set; }
        public int farmId { get; set; }
        public List<AnimalWithTrackerDto> animals { get; set; } = new();
        public int count { get; set; }
        public string message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resultado de asignación masiva de trackers
    /// </summary>
    public class BulkAssignResultDto
    {
        public bool Success { get; set; }
        public int AssignedCount { get; set; }
        public int TotalFreeTrackers { get; set; }
        public int TotalAnimalsWithoutTracker { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<BulkAssignItemDto> Assignments { get; set; } = new();
    }

    /// <summary>
    /// Detalle de cada asignación individual en el bulk
    /// </summary>
    public class BulkAssignItemDto
    {
        public int AnimalId { get; set; }
        public string AnimalName { get; set; } = string.Empty;
        public string? AnimalTag { get; set; }
        public int CustomerTrackerId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }
}