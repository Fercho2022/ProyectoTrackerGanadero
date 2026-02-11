using AdminPanelLicenses.Models;

namespace AdminPanelLicenses.Services
{
    public class TrackerManagementService
    {
        private readonly ApiService _apiService;
        private readonly ILogger<TrackerManagementService> _logger;

        public TrackerManagementService(ApiService apiService, ILogger<TrackerManagementService> logger)
        {
            _apiService = apiService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los trackers disponibles para asignación (usando endpoint de Admin)
        /// </summary>
        public async Task<List<TrackerDto>> GetAvailableTrackersAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<TrackersListResponse>("/api/Admin/available-trackers");
                return response?.Trackers ?? new List<TrackerDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available trackers");
                return new List<TrackerDto>();
            }
        }

        /// <summary>
        /// Obtiene todos los trackers que están transmitiendo (para descubrimiento) (usando endpoint de Admin)
        /// </summary>
        public async Task<List<TrackerDto>> GetDetectedTrackersAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<DetectTrackersResponse>("/api/Admin/detected-trackers");
                return response?.NewTrackers ?? new List<TrackerDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting trackers");
                return new List<TrackerDto>();
            }
        }

        /// <summary>
        /// Obtiene todos los customers del sistema
        /// </summary>
        public async Task<List<CustomerDto>> GetAllCustomersAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<CustomersResponse>("/api/Admin/customers");
                return response?.Customers ?? new List<CustomerDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers");
                return new List<CustomerDto>();
            }
        }

        /// <summary>
        /// Obtiene los trackers asignados a un cliente específico
        /// </summary>
        public async Task<List<CustomerTrackerDto>> GetCustomerTrackersAsync(int customerId)
        {
            try
            {
                var response = await _apiService.GetAsync<CustomerTrackersResponse>($"/api/Admin/customers/{customerId}/trackers");
                return response?.Trackers ?? new List<CustomerTrackerDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting trackers for customer {customerId}");
                return new List<CustomerTrackerDto>();
            }
        }

        /// <summary>
        /// Asigna un tracker a un cliente (operación de administrador)
        /// </summary>
        public async Task<(bool success, string message)> AssignTrackerAsync(int trackerId, int customerId)
        {
            try
            {
                var request = new { TrackerId = trackerId, CustomerId = customerId };
                var response = await _apiService.PostAsync<object, ApiResponse<object>>("/api/Admin/assign-tracker", request);

                if (response?.Success == true)
                {
                    return (true, response.Message ?? "Tracker asignado exitosamente");
                }

                return (false, response?.Message ?? "Error al asignar tracker");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning tracker");
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Desasigna un tracker de un cliente (operación de administrador)
        /// </summary>
        public async Task<(bool success, string message)> UnassignTrackerAsync(int customerTrackerId)
        {
            try
            {
                var response = await _apiService.PostAsync<object, ApiResponse<object>>($"/api/Admin/unassign-tracker/{customerTrackerId}", new { });

                if (response?.Success == true)
                {
                    return (true, response.Message ?? "Tracker desasignado exitosamente");
                }

                return (false, response?.Message ?? "Error al desasignar tracker");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning tracker");
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene todas las asignaciones activas del sistema
        /// </summary>
        public async Task<List<CustomerTrackerDto>> GetAllAssignmentsAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<AllAssignmentsResponse>("/api/Admin/all-assignments");
                return response?.Assignments ?? new List<CustomerTrackerDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all assignments");
                return new List<CustomerTrackerDto>();
            }
        }

        /// <summary>
        /// Obtiene todos los usuarios del sistema (para dropdown al crear customer)
        /// </summary>
        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<UsersResponse>("/api/Admin/users");
                return response?.Users ?? new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return new List<UserDto>();
            }
        }

        /// <summary>
        /// Crea un nuevo customer (operación de administrador)
        /// </summary>
        public async Task<(bool success, string message)> CreateCustomerAsync(CreateCustomerRequest request)
        {
            try
            {
                var response = await _apiService.PostAsync<CreateCustomerRequest, ApiResponse<object>>("/api/Admin/customers", request);

                if (response?.Success == true)
                {
                    return (true, response.Message ?? "Customer creado exitosamente");
                }

                return (false, response?.Message ?? "Error al crear customer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return (false, $"Error: {ex.Message}");
            }
        }
    }

    // Response DTOs específicos para las respuestas de la API
    public class DetectTrackersResponse
    {
        public bool Success { get; set; }
        public List<TrackerDto> NewTrackers { get; set; } = new();
        public int Count { get; set; }
        public string? Message { get; set; }
    }

    public class CustomersResponse
    {
        public bool Success { get; set; }
        public List<CustomerDto> Customers { get; set; } = new();
    }

    public class AllAssignmentsResponse
    {
        public bool Success { get; set; }
        public List<CustomerTrackerDto> Assignments { get; set; } = new();
    }

    public class UsersResponse
    {
        public bool Success { get; set; }
        public List<UserDto> Users { get; set; } = new();
    }

    public class CreateCustomerRequest
    {
        public int UserId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Plan { get; set; } = "Trial";
        public int TrackerLimit { get; set; } = 5;
        public int FarmLimit { get; set; } = 1;
        public DateTime? SubscriptionEnd { get; set; }
    }
}
