using ApiWebTrackerGanado.Services;
using ApiWebTrackerGanado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrackerManagementController : ControllerBase
    {
        private readonly TrackerDiscoveryService _trackerDiscoveryService;
        private readonly LicenseService _licenseService;
        private readonly ILogger<TrackerManagementController> _logger;
        private readonly Data.CattleTrackingContext _context;

        public TrackerManagementController(
            TrackerDiscoveryService trackerDiscoveryService,
            LicenseService licenseService,
            ILogger<TrackerManagementController> logger,
            Data.CattleTrackingContext context)
        {
            _trackerDiscoveryService = trackerDiscoveryService;
            _licenseService = licenseService;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Obtiene todos los trackers disponibles para asignación
        /// </summary>
        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableTrackers()
        {
            try
            {
                var userId = GetCurrentUserId();
                var customer = await _licenseService.GetCurrentCustomerAsync(userId);
                if (customer == null)
                {
                    return Ok(new {
                        success = false,
                        message = "Active una licencia para ver trackers disponibles",
                        trackers = new List<object>()
                    });
                }

                var availableTrackers = await _trackerDiscoveryService.GetAvailableTrackersAsync();

                return Ok(new {
                    success = true,
                    trackers = availableTrackers.Select(t => new {
                        id = t.Id,
                        deviceId = t.DeviceId,
                        name = t.Name,
                        model = t.Model,
                        manufacturer = t.Manufacturer,
                        serialNumber = t.SerialNumber,
                        firmwareVersion = t.FirmwareVersion,
                        batteryLevel = t.BatteryLevel,
                        lastSeen = t.LastSeen,
                        isOnline = t.IsOnline,
                        status = t.Status
                    }),
                    canAddMore = customer.CanAddMoreTrackers(),
                    currentCount = customer.CustomerTrackers.Count(ct => ct.Status == "Active"),
                    maxTrackers = customer.TrackerLimit
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Authentication error in GetAvailableTrackers");
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available trackers");
                return BadRequest(new {
                    success = false,
                    message = "Error obteniendo trackers disponibles"
                });
            }
        }

        /// <summary>
        /// Obtiene los trackers asignados al cliente actual
        /// </summary>
        [HttpGet("my-trackers")]
        public async Task<IActionResult> GetMyTrackers()
        {
            try
            {
                var userId = GetCurrentUserId();

                _logger.LogInformation($"[GetMyTrackersV2] Looking for customer for userId: {userId}");

                var customer = await _licenseService.GetCurrentCustomerAsync(userId);
                if (customer == null)
                {
                    _logger.LogWarning($"[GetMyTrackersV2] No customer found for userId: {userId}");
                    return Ok(new { success = true, trackers = new List<object>(), message = "No hay licencia activa" });
                }

                _logger.LogInformation($"[GetMyTrackersV2] Found customerId: {customer.Id}. Getting trackers directly from context.");

                var customerTrackers = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    .Include(ct => ct.AssignedAnimal)
                        .ThenInclude(a => a.Farm)
                    .Where(ct => ct.CustomerId == customer.Id) // NO filtrar por status 'Active' aquí
                    .OrderByDescending(ct => ct.AssignedAt)
                    .ToListAsync();

                _logger.LogInformation($"[GetMyTrackersV2] Found {customerTrackers.Count} total trackers for customerId: {customer.Id}");

                var trackersResponse = customerTrackers.Select(ct => {
                    var animal = ct.AssignedAnimal;
                    var farm = animal?.Farm;
                    var tracker = ct.Tracker;

                    // Determinar el status basado en la lógica de negocio completa
                    string computedStatus = "Unknown";
                    if (ct.IsActive())
                    {
                        computedStatus = IsTrackerOnline(tracker) ? "Online" : "Offline";
                    }
                    else if (ct.Status == "Inactive")
                    {
                        computedStatus = "Inactive";
                    }
                    else if (ct.Status == "Transferred")
                    {
                        computedStatus = "Transferred";
                    }
                    else
                    {
                        computedStatus = ct.Status; // Fallback al status de la DB
                    }

                    return new {
                        id = ct.Id,
                        trackerId = ct.TrackerId,
                        trackerName = tracker?.Name ?? tracker?.DeviceId ?? "Unknown",
                        deviceId = tracker?.DeviceId,
                        farmId = farm?.Id,
                        farmName = farm?.Name ?? "N/A",
                        animalName = animal?.Name ?? "Not Assigned",
                        status = computedStatus,
                        assignedAt = ct.AssignedAt
                    };
                }).ToList();

                return Ok(new {
                    success = true,
                    trackers = trackersResponse
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Authentication error in GetMyTrackersV2");
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer trackers v2");
                return BadRequest(new {
                    success = false,
                    message = "Error obteniendo trackers del cliente (v2)"
                });
            }
        }

        /// <summary>
        /// Asigna un tracker al cliente actual
        /// </summary>
        [HttpPost("assign")]
        public async Task<IActionResult> AssignTracker([FromBody] AssignTrackerRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
        
                var customer = await _licenseService.GetCurrentCustomerAsync(userId);
                if (customer == null)
                {
                    return BadRequest(new {
                        success = false,
                        message = "No hay licencia activa. Active una licencia primero."
                    });
                }
        
                if (!customer.CanAddMoreTrackers())
                {
                    return BadRequest(new {
                        success = false,
                        message = $"Ha alcanzado el límite de {customer.TrackerLimit} trackers"
                    });
                }
        
                var success = await _trackerDiscoveryService.AssignTrackerToCustomerAsync(
                    request.TrackerId,
                    customer.Id,
                    userId,
                    request.LicenseId,
                    null, // request.CustomName, // Comentado porque la propiedad no existe
                    request.Notes);
        
                if (!success)
                {
                    return BadRequest(new {
                        success = false,
                        message = "No se pudo asignar el tracker. Verifique que esté disponible."
                    });
                }
        
                return Ok(new {
                    success = true,
                    message = "Tracker asignado exitosamente",
                    trackerId = request.TrackerId
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Authentication error in AssignTracker");
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning tracker {TrackerId}", request.TrackerId);
                return BadRequest(new {
                    success = false,
                    message = "Error interno al asignar el tracker"
                });
            }
        }

        /// <summary>
        /// Desasigna un tracker del cliente actual
        /// </summary>
        [HttpPost("unassign/{customerTrackerId}")]
        public async Task<IActionResult> UnassignTracker(int customerTrackerId)
        {
            try
            {
                var userId = GetCurrentUserId();
        
                var success = await _trackerDiscoveryService.UnassignTrackerAsync(customerTrackerId, userId);
        
                if (!success)
                {
                    return BadRequest(new {
                        success = false,
                        message = "No se pudo desasignar el tracker"
                    });
                }
        
                return Ok(new {
                    success = true,
                    message = "Tracker desasignado exitosamente"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Authentication error in UnassignTracker");
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning tracker {CustomerTrackerId}", customerTrackerId);
                return BadRequest(new {
                    success = false,
                    message = "Error interno al desasignar el tracker"
                });
            }
        }

        /// <summary>
        /// Detecta trackers que están transmitiendo datos GPS activamente
        /// </summary>
        [HttpGet("detect-new")]
        public async Task<IActionResult> DetectNewTrackers()
        {
            try
            {
                var userId = GetCurrentUserId();
        
                var customer = await _licenseService.GetCurrentCustomerAsync(userId);
                if (customer == null)
                {
                    return Ok(new {
                        success = false,
                        message = "Active una licencia para detectar trackers",
                        newTrackers = new List<object>()
                    });
                }
        
                var newTrackers = await _trackerDiscoveryService.GetActiveTransmittingTrackersAsync();
        
                return Ok(new {
                    success = true,
                    newTrackers = newTrackers.Select(t => new {
                        id = t.Id,
                        deviceId = t.DeviceId,
                        name = t.Name,
                        model = t.Model,
                        manufacturer = t.Manufacturer,
                        serialNumber = t.SerialNumber,
                        batteryLevel = t.BatteryLevel,
                        lastSeen = t.LastSeen,
                        isOnline = t.IsOnline,
                        status = t.Status
                    }),
                    count = newTrackers.Count,
                    message = newTrackers.Count > 0
                        ? $"Se encontraron {newTrackers.Count} trackers transmitiendo datos GPS"
                        : "No se encontraron trackers transmitiendo datos GPS actualmente"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Authentication error in DetectNewTrackers");
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting new trackers");
                return BadRequest(new {
                    success = false,
                    message = "Error detectando nuevos trackers"
                });
            }
        }

        /// <summary>
        /// Registra un tracker detectado automáticamente
        /// </summary>
        [HttpPost("register-detected")]
        public async Task<IActionResult> RegisterDetectedTracker([FromBody] RegisterTrackerRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
        
                var customer = await _licenseService.GetCurrentCustomerAsync(userId);
                if (customer == null)
                {
                    // Para desarrollo - crear customer automáticamente
                    try
                    {
                        customer = await _licenseService.CreateDevelopmentCustomerAsync(userId);
                        if (customer == null)
                        {
                            return BadRequest(new {
                                success = false,
                                message = "No se pudo crear customer de desarrollo"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new {
                            success = false,
                            message = $"Error creando customer: {ex.Message}"
                        });
                    }
                }
        
                // Verificar si el cliente puede agregar más trackers ANTES de registrarlo
                if (!customer.CanAddMoreTrackers())
                {
                    return BadRequest(new {
                        success = false,
                        message = $"Límite de trackers alcanzado ({customer.TrackerLimit}). No se puede registrar un nuevo tracker."
                    });
                }
        
                var trackerId = await _trackerDiscoveryService.RegisterDetectedTrackerAsync(
                    request.DeviceId,
                    request.Model);
        
                if (trackerId == null)
                {
                    return BadRequest(new {
                        success = false,
                        message = "No se pudo registrar el tracker. Es posible que ya exista."
                    });
                }
        
                // Si auto_assign es true, asignarlo automáticamente al cliente
                if (request.AutoAssign)
                {
                    await _trackerDiscoveryService.AssignTrackerToCustomerAsync(
                        trackerId.Value,
                        customer.Id,
                        userId,
                        null,
                        null, // request.CustomName, // Comentado porque la propiedad no existe
                        "Asignación automática al detectar"
                    );
        
                    return Ok(new {
                        success = true,
                        message = "Tracker registrado y asignado automáticamente",
                        trackerId = trackerId.Value,
                        assigned = true
                    });
                }
        
                return Ok(new {
                    success = true,
                    message = "Tracker registrado exitosamente, pero no asignado al cliente.",
                    trackerId = trackerId.Value,
                    assigned = false
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Authentication error in RegisterDetectedTracker");
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering tracker {DeviceId}", request.DeviceId);
                return BadRequest(new {
                    success = false,
                    message = "Error registrando el tracker"
                });
            }
        }

        /// <summary>
        /// Endpoint público para detectar trackers transmitiendo disponibles (no asignados)
        /// SECURITY FIX: Ahora solo devuelve trackers NO asignados a ningún cliente
        /// </summary>
        [HttpGet("detect-new-public")]
        public async Task<IActionResult> DetectNewTrackersPublic()
        {
            try
            {
                _logger.LogInformation("[DetectNewTrackersPublic] Starting tracker detection...");

                // Obtener SOLO trackers disponibles (no asignados a ningún cliente)
                var availableTrackers = await _trackerDiscoveryService.GetAvailableUnassignedTrackersAsync();

                _logger.LogInformation($"[DetectNewTrackersPublic] Found {availableTrackers.Count} available trackers");

                return Ok(new {
                    success = true,
                    newTrackers = availableTrackers.Select(t => new {
                        id = t.Id,
                        deviceId = t.DeviceId,
                        name = t.Name,
                        model = t.Model,
                        manufacturer = t.Manufacturer,
                        serialNumber = t.SerialNumber,
                        batteryLevel = t.BatteryLevel,
                        lastSeen = t.LastSeen,
                        isOnline = t.IsOnline,
                        status = t.Status
                    }),
                    count = availableTrackers.Count,
                    message = availableTrackers.Count > 0
                        ? $"Se encontraron {availableTrackers.Count} trackers disponibles para asignar"
                        : "No se encontraron trackers disponibles actualmente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting new trackers (public)");
                return BadRequest(new {
                    success = false,
                    message = "Error detectando nuevos trackers"
                });
            }
        }

        private int GetCurrentUserId()
        {
            _logger.LogInformation($"[GetCurrentUserId] Checking user authentication...");
            _logger.LogInformation($"[GetCurrentUserId] User.Identity.IsAuthenticated: {HttpContext.User.Identity?.IsAuthenticated}");
        
            var userIdClaim = HttpContext.User.FindFirst("sub")?.Value ??
                             HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
            _logger.LogInformation($"[GetCurrentUserId] Found userIdClaim: {userIdClaim}");
        
            if (int.TryParse(userIdClaim, out var userId))
            {
                _logger.LogInformation($"[GetCurrentUserId] Parsed userId: {userId}");
                return userId;
            }
        
            _logger.LogWarning("[GetCurrentUserId] Could not determine user ID from token claims.");
            throw new UnauthorizedAccessException("Could not determine user ID from token claims.");
        }

        /// <summary>
        /// Determina si un tracker está online basándose en su última actividad
        /// </summary>
        private static bool IsTrackerOnline(Tracker? tracker)
        {
            if (tracker == null) return false;

            // Consideramos que un tracker está online si su última actividad fue hace menos de 30 minutos
            var cutoffTime = DateTime.UtcNow.AddMinutes(-30);
            return tracker.LastSeen >= cutoffTime && tracker.IsActive;
        }
    }

    public class AssignTrackerRequest
    {
        public int TrackerId { get; set; }
        public int? LicenseId { get; set; }
        // public string? CustomName { get; set; } // Comentado porque la propiedad no existe
        public string? Notes { get; set; }
    }

    public class RegisterTrackerRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? Model { get; set; }
        // public string? CustomName { get; set; } // Comentado porque la propiedad no existe
        /// <summary>
        /// SECURITY FIX: Changed default to false to prevent auto-assignment.
        /// Trackers must be assigned manually from Admin Panel.
        /// </summary>
        public bool AutoAssign { get; set; } = false;
    }
}
