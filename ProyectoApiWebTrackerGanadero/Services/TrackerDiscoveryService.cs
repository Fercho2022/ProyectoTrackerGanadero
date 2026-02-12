using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ApiWebTrackerGanado.Services
{
    /// <summary>
    /// Servicio para detectar y gestionar trackers disponibles para asignación
    /// </summary>
    public class TrackerDiscoveryService
    {
        private readonly CattleTrackingContext _context;
        private readonly ILogger<TrackerDiscoveryService> _logger;

        public TrackerDiscoveryService(
            CattleTrackingContext context,
            ILogger<TrackerDiscoveryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los trackers disponibles para asignación
        /// </summary>
        public async Task<List<TrackerDiscoveryDto>> GetAvailableTrackersAsync()
        {
            try
            {
                var availableTrackers = await _context.Trackers
                    .Include(t => t.CustomerTrackers)
                    .ThenInclude(ct => ct.Customer)
                    .Where(t => t.IsActive && t.IsAvailableForAssignment)
                    .Where(t => !t.CustomerTrackers.Any(ct => ct.Status == "Active"))
                    .Select(t => new TrackerDiscoveryDto
                    {
                        Id = t.Id,
                        DeviceId = t.DeviceId,
                        Name = t.Name,
                        Model = t.Model,
                        Manufacturer = t.Manufacturer,
                        SerialNumber = t.SerialNumber,
                        Status = t.Status
                    })
                    .OrderBy(t => t.DeviceId)
                    .ToListAsync();

                _logger.LogInformation($"Found {availableTrackers.Count} available trackers");
                return availableTrackers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available trackers");
                throw;
            }
        }

        /// <summary>
        /// Obtiene trackers asignados a un cliente específico
        /// </summary>
        public async Task<List<CustomerTrackerDto>> GetCustomerTrackersAsync(int customerId)
        {
            try
            {
                var customerTrackers = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    // .Include(ct => ct.License) // Comentado porque la propiedad no existe
                    // .Include(ct => ct.AssignedByUser) // Comentado porque la propiedad no existe
                    .Where(ct => ct.CustomerId == customerId && ct.Status == "Active")
                    .Select(ct => new CustomerTrackerDto
                    {
                        Id = ct.Id,
                        TrackerId = ct.TrackerId,
                        DeviceId = ct.Tracker.DeviceId,
                        TrackerName = ct.Tracker.Name,
                        // CustomName = null, // ct.CustomName, // Comentado porque la propiedad no existe
                        Model = ct.Tracker.Model,
                        BatteryLevel = ct.Tracker.BatteryLevel,
                        LastSeen = ct.Tracker.LastSeen,
                        IsOnline = ct.Tracker.LastSeen > DateTime.UtcNow.AddMinutes(-10),
                        AssignedAt = ct.AssignedAt,
                        // AssignmentMethod = "", // ct.AssignmentMethod, // Comentado porque la propiedad no existe
                        LicenseKey = null, // ct.License != null ? ct.License.LicenseKey : null, // Comentado porque la propiedad no existe
                        AssignedByUser = null, // ct.AssignedByUser != null ? ct.AssignedByUser.Name : null, // Comentado porque la propiedad no existe
                        Notes = null // ct.Notes // Comentado porque la propiedad no existe
                    })
                    .OrderBy(ct => ct.DeviceId)
                    .ToListAsync();

                _logger.LogInformation($"Found {customerTrackers.Count} trackers for customer {customerId}");
                return customerTrackers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer trackers for customer {CustomerId}", customerId);
                throw;
            }
        }

        /// <summary>
        /// Asigna un tracker a un cliente
        /// </summary>
        public async Task<bool> AssignTrackerToCustomerAsync(
            int trackerId,
            int customerId,
            int userId,
            int? licenseId = null,
            string? customName = null,
            string? notes = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Verificar que el tracker existe y está disponible
                var tracker = await _context.Trackers
                    .Include(t => t.CustomerTrackers)
                    .FirstOrDefaultAsync(t => t.Id == trackerId);

                if (tracker == null)
                {
                    _logger.LogWarning($"Tracker with ID {trackerId} not found");
                    return false;
                }

                if (!tracker.CanBeAssigned())
                {
                    _logger.LogWarning($"Tracker {tracker.DeviceId} is not available for assignment");
                    return false;
                }

                // Verificar que el cliente existe y puede agregar más trackers
                var customer = await _context.Customers
                    .Include(c => c.CustomerTrackers)
                    .FirstOrDefaultAsync(c => c.Id == customerId);

                if (customer == null)
                {
                    _logger.LogWarning($"Customer with ID {customerId} not found");
                    return false;
                }

                if (!customer.CanAddMoreTrackers())
                {
                    _logger.LogWarning($"Customer {customer.CompanyName} has reached tracker limit");
                    return false;
                }

                // Verificar licencia si se especifica
                License? license = null;
                if (licenseId.HasValue)
                {
                    license = await _context.Licenses
                        .FirstOrDefaultAsync(l => l.Id == licenseId.Value && l.CustomerId == customerId);

                    if (license == null || !license.IsValid())
                    {
                        _logger.LogWarning($"Invalid license {licenseId} for customer {customerId}");
                        return false;
                    }
                }

                // Crear la asignación
                var customerTracker = new CustomerTracker
                {
                    CustomerId = customerId,
                    TrackerId = trackerId,
                    // AssignmentMethod = "Manual", // Comentado porque la propiedad no existe
                    Status = "Active",
                    AssignedAt = DateTime.UtcNow,
                    // AssignedByUserId = userId, // Comentado porque la propiedad no existe
                    // LicenseId = licenseId, // Comentado porque la propiedad no existe
                    // CustomName = customName, // Comentado porque la propiedad no existe
                    // Notes = notes // Comentado porque la propiedad no existe
                };

                _context.CustomerTrackers.Add(customerTracker);

                // Actualizar el tracker
                tracker.IsAvailableForAssignment = false;
                tracker.LastSeen = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Successfully assigned tracker {tracker.DeviceId} to customer {customer.CompanyName}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error assigning tracker {TrackerId} to customer {CustomerId}", trackerId, customerId);
                throw;
            }
        }

        /// <summary>
        /// Desasigna un tracker de un cliente
        /// </summary>
        public async Task<bool> UnassignTrackerAsync(int customerTrackerId, int userId)
        {
            try
            {
                var customerTracker = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    .FirstOrDefaultAsync(ct => ct.Id == customerTrackerId);

                if (customerTracker == null || !customerTracker.IsActive())
                {
                    _logger.LogWarning($"Customer tracker {customerTrackerId} not found or inactive");
                    return false;
                }

                customerTracker.Unassign(userId);

                // Hacer el tracker disponible nuevamente
                if (customerTracker.Tracker != null)
                {
                    customerTracker.Tracker.IsAvailableForAssignment = true;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully unassigned tracker {customerTracker.Tracker?.DeviceId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning customer tracker {CustomerTrackerId}", customerTrackerId);
                throw;
            }
        }

        /// <summary>
        /// Obtiene trackers activos que están transmitiendo datos GPS recientes
        /// </summary>
        public async Task<List<TrackerDiscoveryDto>> GetActiveTransmittingTrackersAsync()
        {
            try
            {
                _logger.LogInformation("Starting GetActiveTransmittingTrackersAsync...");

                var cutoffTime = DateTime.UtcNow.AddMinutes(-30);
                _logger.LogInformation($"Looking for GPS data newer than: {cutoffTime}");

                // Obtener DeviceIds que han transmitido recientemente
                var recentDeviceIds = await _context.LocationHistories
                    .Where(lh => lh.Timestamp > cutoffTime)
                    .Where(lh => !string.IsNullOrEmpty(lh.DeviceId))
                    .Select(lh => lh.DeviceId!)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation($"Found {recentDeviceIds.Count} recent device IDs: {string.Join(", ", recentDeviceIds)}");

                // Para detección, crear DTOs para TODOS los device IDs activos, registrados o no
                var activeTrackers = new List<TrackerDiscoveryDto>();

                foreach (var deviceId in recentDeviceIds)
                {
                    // Buscar si ya está registrado
                    var existingTracker = await _context.Trackers
                        .FirstOrDefaultAsync(t => t.DeviceId == deviceId);

                    // Obtener la última ubicación para determinar si está online
                    var lastLocation = await _context.LocationHistories
                        .Where(lh => lh.DeviceId == deviceId)
                        .OrderByDescending(lh => lh.Timestamp)
                        .FirstOrDefaultAsync();

                    if (existingTracker != null)
                    {
                        // Tracker ya registrado
                        activeTrackers.Add(new TrackerDiscoveryDto
                        {
                            Id = existingTracker.Id,
                            DeviceId = existingTracker.DeviceId,
                            Name = existingTracker.Name,
                            Model = existingTracker.Model,
                            Manufacturer = existingTracker.Manufacturer,
                            SerialNumber = existingTracker.SerialNumber,
                            Status = existingTracker.Status,
                            BatteryLevel = existingTracker.BatteryLevel,
                            LastSeen = lastLocation?.Timestamp ?? existingTracker.LastSeen,
                            IsOnline = lastLocation != null && lastLocation.Timestamp > DateTime.UtcNow.AddMinutes(-10)
                        });
                    }
                    else
                    {
                        // Tracker detectado pero no registrado
                        activeTrackers.Add(new TrackerDiscoveryDto
                        {
                            Id = 0, // Indicates not registered
                            DeviceId = deviceId,
                            Name = $"Tracker {deviceId}",
                            Model = "Unknown",
                            Status = "Detected",
                            BatteryLevel = 100,
                            LastSeen = lastLocation?.Timestamp ?? DateTime.UtcNow,
                            IsOnline = lastLocation != null && lastLocation.Timestamp > DateTime.UtcNow.AddMinutes(-10)
                        });
                    }
                }

                _logger.LogInformation($"Found {activeTrackers.Count} active transmitting trackers");
                return activeTrackers.OrderBy(t => t.DeviceId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active transmitting trackers");
                throw;
            }
        }

        /// <summary>
        /// SECURITY FIX: Obtiene trackers disponibles (transmitiendo y NO asignados a ningún cliente)
        /// </summary>
        public async Task<List<TrackerDiscoveryDto>> GetAvailableUnassignedTrackersAsync()
        {
            try
            {
                _logger.LogInformation("[GetAvailableUnassignedTrackersAsync] Starting...");

                var cutoffTime = DateTime.UtcNow.AddMinutes(-30);
                _logger.LogInformation($"[GetAvailableUnassignedTrackersAsync] Looking for GPS data newer than: {cutoffTime}");

                // Obtener DeviceIds que han transmitido recientemente
                var recentDeviceIds = await _context.LocationHistories
                    .Where(lh => lh.Timestamp > cutoffTime)
                    .Where(lh => !string.IsNullOrEmpty(lh.DeviceId))
                    .Select(lh => lh.DeviceId!)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation($"[GetAvailableUnassignedTrackersAsync] Found {recentDeviceIds.Count} recent device IDs: {string.Join(", ", recentDeviceIds)}");

                // Obtener IDs de trackers ya asignados a clientes (con status Active)
                var assignedTrackerIds = await _context.CustomerTrackers
                    .Where(ct => ct.Status == "Active")
                    .Select(ct => ct.TrackerId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation($"[GetAvailableUnassignedTrackersAsync] Found {assignedTrackerIds.Count} already assigned tracker IDs");

                var availableTrackers = new List<TrackerDiscoveryDto>();

                foreach (var deviceId in recentDeviceIds)
                {
                    // Buscar si ya está registrado
                    var existingTracker = await _context.Trackers
                        .FirstOrDefaultAsync(t => t.DeviceId == deviceId);

                    // Obtener la última ubicación para determinar si está online
                    var lastLocation = await _context.LocationHistories
                        .Where(lh => lh.DeviceId == deviceId)
                        .OrderByDescending(lh => lh.Timestamp)
                        .FirstOrDefaultAsync();

                    if (existingTracker != null)
                    {
                        // SECURITY CHECK: Verificar que NO esté asignado a ningún cliente
                        if (assignedTrackerIds.Contains(existingTracker.Id))
                        {
                            _logger.LogInformation($"[GetAvailableUnassignedTrackersAsync] Tracker {deviceId} is already assigned, skipping");
                            continue; // Skip trackers asignados
                        }

                        // Tracker registrado y DISPONIBLE
                        availableTrackers.Add(new TrackerDiscoveryDto
                        {
                            Id = existingTracker.Id,
                            DeviceId = existingTracker.DeviceId,
                            Name = existingTracker.Name,
                            Model = existingTracker.Model,
                            Manufacturer = existingTracker.Manufacturer,
                            SerialNumber = existingTracker.SerialNumber,
                            Status = "Available",
                            BatteryLevel = existingTracker.BatteryLevel,
                            LastSeen = lastLocation?.Timestamp ?? existingTracker.LastSeen,
                            IsOnline = lastLocation != null && lastLocation.Timestamp > DateTime.UtcNow.AddMinutes(-10)
                        });
                    }
                    else
                    {
                        // Tracker detectado pero no registrado (siempre disponible)
                        availableTrackers.Add(new TrackerDiscoveryDto
                        {
                            Id = 0, // Indicates not registered
                            DeviceId = deviceId,
                            Name = $"Tracker {deviceId}",
                            Model = "Unknown",
                            Status = "Detected",
                            BatteryLevel = 100,
                            LastSeen = lastLocation?.Timestamp ?? DateTime.UtcNow,
                            IsOnline = lastLocation != null && lastLocation.Timestamp > DateTime.UtcNow.AddMinutes(-10)
                        });
                    }
                }

                _logger.LogInformation($"[GetAvailableUnassignedTrackersAsync] Found {availableTrackers.Count} available (unassigned) trackers");
                return availableTrackers.OrderBy(t => t.DeviceId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetAvailableUnassignedTrackersAsync] Error getting available unassigned trackers");
                throw;
            }
        }

        /// <summary>
        /// Detecta nuevos trackers basado en las últimas señales recibidas
        /// </summary>
        public async Task<List<TrackerDiscoveryDto>> DetectNewTrackersAsync()
        {
            try
            {
                _logger.LogInformation("Starting DetectNewTrackersAsync...");

                // Obtener device IDs únicos de los últimos registros de ubicación (últimos 30 minutos)
                var cutoffTime = DateTime.UtcNow.AddMinutes(-30);
                _logger.LogInformation($"Looking for GPS data newer than: {cutoffTime} (discovery scan window)");

                var recentDeviceIds = await _context.LocationHistories
                    .Where(lh => lh.Timestamp > cutoffTime)
                    .Where(lh => !string.IsNullOrEmpty(lh.DeviceId))
                    .Select(lh => lh.DeviceId!)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation($"Found {recentDeviceIds.Count} recent device IDs: {string.Join(", ", recentDeviceIds)}");

                // Encontrar device IDs que no están registrados como trackers
                var existingDeviceIds = await _context.Trackers
                    .Select(t => t.DeviceId)
                    .ToListAsync();

                _logger.LogInformation($"Found {existingDeviceIds.Count} existing tracker device IDs: {string.Join(", ", existingDeviceIds)}");

                var newDeviceIds = recentDeviceIds
                    .Where(did => !existingDeviceIds.Contains(did))
                    .ToList();

                _logger.LogInformation($"Found {newDeviceIds.Count} NEW device IDs: {string.Join(", ", newDeviceIds)}");

                var newTrackers = new List<TrackerDiscoveryDto>();

                foreach (var deviceId in newDeviceIds)
                {
                    // Obtener la última ubicación para extraer información del dispositivo
                    var latestLocation = await _context.LocationHistories
                        .Where(lh => lh.DeviceId == deviceId)
                        .OrderByDescending(lh => lh.Timestamp)
                        .FirstOrDefaultAsync();

                    if (latestLocation != null)
                    {
                        newTrackers.Add(new TrackerDiscoveryDto
                        {
                            DeviceId = deviceId,
                            Name = $"Tracker {deviceId}",
                            Model = "Unknown",
                            LastSeen = latestLocation.Timestamp,
                            IsOnline = latestLocation.Timestamp > DateTime.UtcNow.AddMinutes(-10),
                            Status = "Detected",
                            BatteryLevel = 100 // Default value
                        });
                    }
                }

                _logger.LogInformation($"Detected {newTrackers.Count} new trackers");
                return newTrackers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting new trackers");
                throw;
            }
        }

        /// <summary>
        /// Registra automáticamente un nuevo tracker detectado
        /// </summary>
        public async Task<int?> RegisterDetectedTrackerAsync(string deviceId, string? model = null)
        {
            try
            {
                // Verificar que no existe ya
                var existing = await _context.Trackers
                    .FirstOrDefaultAsync(t => t.DeviceId == deviceId);

                if (existing != null)
                {
                    _logger.LogWarning($"Tracker with device ID {deviceId} already exists");
                    return existing.Id;
                }

                var tracker = new Tracker
                {
                    DeviceId = deviceId,
                    Name = $"Tracker {deviceId}",
                    Model = model ?? "Auto-detected",
                    Status = "Active",
                    IsActive = true,
                    IsAvailableForAssignment = true,
                    LastSeen = DateTime.UtcNow,
                    BatteryLevel = 100
                };

                _context.Trackers.Add(tracker);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully registered new tracker {deviceId} with ID {tracker.Id}");
                return tracker.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering tracker {DeviceId}", deviceId);
                throw;
            }
        }
    }

    // DTOs para el servicio
    public class TrackerDiscoveryDto
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Manufacturer { get; set; }
        public string? SerialNumber { get; set; }
        public string? FirmwareVersion { get; set; }
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerTrackerDto
    {
        public int Id { get; set; }
        public int TrackerId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string TrackerName { get; set; } = string.Empty;
        // public string? CustomName { get; set; } // Comentado porque la propiedad no existe
        public string? Model { get; set; }
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public DateTime AssignedAt { get; set; }
        // public string AssignmentMethod { get; set; } = string.Empty; // Comentado porque la propiedad no existe
        public string? LicenseKey { get; set; }
        public string? AssignedByUser { get; set; }
        public string? Notes { get; set; }
    }
}