using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FarmTrackerController : ControllerBase
    {
        private readonly FarmTrackerIntegrationService _farmTrackerService;
        private readonly ILogger<FarmTrackerController> _logger;
        private readonly CattleTrackingContext _context;

        public FarmTrackerController(
            FarmTrackerIntegrationService farmTrackerService,
            ILogger<FarmTrackerController> logger,
            CattleTrackingContext context)
        {
            _farmTrackerService = farmTrackerService;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Obtiene los trackers disponibles del usuario para asignar a granjas/animales
        /// </summary>
        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableTrackers()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    // Para desarrollo - usar usuario por defecto
                    userId = 1;
                }

                // SECURITY FIX: DISABLED auto-creation of CustomerTrackers
                // Trackers must be assigned manually from Admin Panel
                // Old code (disabled):
                // await EnsureCustomerTrackersExist(userId.Value);

                var availableTrackers = await _farmTrackerService.GetAvailableTrackersForFarmsAsync(userId.Value);

                return Ok(new
                {
                    success = true,
                    trackers = availableTrackers,
                    count = availableTrackers.Count,
                    message = availableTrackers.Count > 0
                        ? $"Se encontraron {availableTrackers.Count} trackers disponibles para asignar"
                        : "No hay trackers disponibles. Primero asigne trackers desde la gestión de trackers."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available farm trackers");
                return BadRequest(new
                {
                    success = false,
                    message = "Error obteniendo trackers disponibles"
                });
            }
        }

        /// <summary>
        /// Asigna un CustomerTracker a un animal específico
        /// </summary>
        [HttpPost("assign-to-animal")]
        public async Task<IActionResult> AssignTrackerToAnimal([FromBody] AssignTrackerToAnimalRequest request)
        {
            try
            {
                Console.WriteLine($"[AssignTrackerToAnimal] ===== ASSIGNMENT REQUEST DETAILS =====");
                Console.WriteLine($"[AssignTrackerToAnimal] CustomerTrackerId: {request.CustomerTrackerId}");
                Console.WriteLine($"[AssignTrackerToAnimal] AnimalId: {request.AnimalId}");
                Console.WriteLine($"[AssignTrackerToAnimal] Raw JSON: {System.Text.Json.JsonSerializer.Serialize(request)}");

                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    // Para desarrollo - usar usuario por defecto
                    userId = 1;
                }

                Console.WriteLine($"[AssignTrackerToAnimal] Using UserId={userId}");

                var (success, message) = await _farmTrackerService.AssignTrackerToAnimalAsync(
                    request.CustomerTrackerId,
                    request.AnimalId,
                    userId.Value);

                Console.WriteLine($"[AssignTrackerToAnimal] Assignment result: success={success}, message={message}");

                if (!success)
                {
                    Console.WriteLine($"[AssignTrackerToAnimal] Assignment failed - {message}");
                    return BadRequest(new
                    {
                        success = false,
                        message = message
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssignTrackerToAnimal] EXCEPTION: {ex.Message}");
                Console.WriteLine($"[AssignTrackerToAnimal] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[AssignTrackerToAnimal] Inner exception: {ex.InnerException.Message}");
                }

                _logger.LogError(ex, "Error assigning tracker to animal");
                return BadRequest(new
                {
                    success = false,
                    message = "Error interno al asignar el tracker"
                });
            }
        }

        /// <summary>
        /// Desasigna un tracker de un animal
        /// </summary>
        [HttpPost("unassign-from-animal/{animalId}")]
        public async Task<IActionResult> UnassignTrackerFromAnimal(int animalId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    // Para desarrollo - usar usuario por defecto
                    userId = 1;
                }

                var success = await _farmTrackerService.UnassignTrackerFromAnimalAsync(animalId, userId.Value);

                if (!success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No se pudo desasignar el tracker del animal"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Tracker desasignado del animal exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning tracker from animal {AnimalId}", animalId);
                return BadRequest(new
                {
                    success = false,
                    message = "Error interno al desasignar el tracker"
                });
            }
        }

        /// <summary>
        /// Obtiene información del tracker asignado a un animal
        /// </summary>
        [HttpGet("animal/{animalId}/tracker")]
        public async Task<IActionResult> GetAnimalTrackerInfo(int animalId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    // Para desarrollo - usar usuario por defecto
                    userId = 1;
                }

                var trackerInfo = await _farmTrackerService.GetAnimalTrackerInfoAsync(animalId, userId.Value);

                if (trackerInfo == null)
                {
                    return Ok(new
                    {
                        success = true,
                        hasTracker = false,
                        message = "El animal no tiene tracker asignado"
                    });
                }

                return Ok(new
                {
                    success = true,
                    hasTracker = true,
                    tracker = trackerInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracker info for animal {AnimalId}", animalId);
                return BadRequest(new
                {
                    success = false,
                    message = "Error obteniendo información del tracker"
                });
            }
        }

        /// <summary>
        /// Obtiene todos los animales de una granja con su información de tracker en una sola consulta
        /// OPTIMIZACIÓN: Elimina el problema N+1 de hacer 120+ requests individuales
        /// </summary>
        [HttpGet("farm/{farmId}/animals-with-trackers")]
        public async Task<IActionResult> GetFarmAnimalsWithTrackers(int farmId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    // Para desarrollo - usar usuario por defecto
                    userId = 1;
                }

                var animalsWithTrackers = await _farmTrackerService.GetFarmAnimalsWithTrackersAsync(farmId, userId.Value);

                return Ok(new
                {
                    success = true,
                    farmId = farmId,
                    animals = animalsWithTrackers,
                    count = animalsWithTrackers.Count,
                    message = $"Se obtuvieron {animalsWithTrackers.Count} animales con información de trackers"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animals with trackers for farm {FarmId}", farmId);
                return BadRequest(new
                {
                    success = false,
                    message = "Error obteniendo animales con trackers"
                });
            }
        }

        /// <summary>
        /// Asignación masiva: vincula trackers libres con animales sin tracker en orden numérico
        /// </summary>
        [HttpPost("bulk-assign/{farmId}")]
        public async Task<IActionResult> BulkAssignTrackers(int farmId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) userId = 1;

                var result = await _farmTrackerService.BulkAssignTrackersAsync(farmId, userId.Value);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk assign for farm {FarmId}", farmId);
                return BadRequest(new { Success = false, Message = "Error interno en asignación masiva" });
            }
        }

        /// <summary>
        /// Elimina un tracker específico de la base de datos
        /// </summary>
        [HttpDelete("delete-tracker/{trackerId}")]
        public async Task<IActionResult> DeleteTracker(int trackerId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    userId = 1; // Para desarrollo
                }

                _logger.LogInformation("DeleteTracker called with trackerId={TrackerId}, userId={UserId}", trackerId, userId.Value);

                var success = await _farmTrackerService.DeleteTrackerAsync(trackerId, userId.Value);

                if (!success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No se pudo eliminar el tracker. Verifique que le pertenezca."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Tracker eliminado exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tracker {TrackerId}", trackerId);
                return BadRequest(new
                {
                    success = false,
                    message = "Error interno al eliminar el tracker"
                });
            }
        }

        /// <summary>
        /// Elimina todos los trackers de la base de datos
        /// </summary>
        [HttpDelete("delete-all-trackers")]
        public async Task<IActionResult> DeleteAllTrackers()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    userId = 1; // Para desarrollo
                }

                var deletedCount = await _farmTrackerService.DeleteAllTrackersAsync(userId.Value);

                return Ok(new
                {
                    success = true,
                    deletedCount = deletedCount,
                    message = $"Se eliminaron {deletedCount} trackers exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all trackers");
                return BadRequest(new
                {
                    success = false,
                    message = "Error interno al eliminar todos los trackers"
                });
            }
        }

        /// <summary>
        /// Limpia trackers inactivos (sin transmisión en los últimos 2 minutos) de una granja
        /// </summary>
        [HttpPost("cleanup-inactive/{farmId}")]
        public async Task<IActionResult> CleanupInactiveTrackers(int farmId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    // Para desarrollo - usar usuario por defecto
                    userId = 1;
                }

                var cleanedCount = await _farmTrackerService.CleanupInactiveTrackersFromFarmAsync(farmId, userId.Value);

                return Ok(new
                {
                    success = true,
                    cleanedCount = cleanedCount,
                    message = cleanedCount > 0
                        ? $"Se limpiaron {cleanedCount} trackers inactivos de la granja"
                        : "No se encontraron trackers inactivos para limpiar"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up inactive trackers from farm {FarmId}", farmId);
                return BadRequest(new
                {
                    success = false,
                    message = "Error limpiando trackers inactivos"
                });
            }
        }

        /// <summary>
        /// Endpoint de diagnóstico para listar todos los CustomerTrackers
        /// </summary>
        [HttpGet("debug-all-customer-trackers")]
        public async Task<IActionResult> DebugAllCustomerTrackers()
        {
            try
            {
                var userId = GetCurrentUserId() ?? 1;

                // Buscar customer del usuario
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active");

                // Obtener TODOS los CustomerTrackers (no solo del usuario) para debug
                var allCustomerTrackers = await _context.CustomerTrackers
                    .Select(ct => new {
                        ct.Id,
                        ct.TrackerId,
                        ct.CustomerId,
                        ct.Status,
                        ct.AssignedAt,
                        HasAssignedAnimal = ct.AssignedAnimal != null,
                        AssignedAnimalId = ct.AssignedAnimal != null ? ct.AssignedAnimal.Id : (int?)null,
                        TrackerDeviceId = ct.Tracker.DeviceId,
                        TrackerName = ct.Tracker.Name
                    })
                    .ToListAsync();

                // También obtener todos los Trackers para comparar
                var allTrackers = await _context.Trackers
                    .Select(t => new {
                        t.Id,
                        t.DeviceId,
                        t.Name,
                        t.IsActive
                    })
                    .ToListAsync();

                return Ok(new {
                    userId = userId,
                    customer = customer != null ? new { customer.Id, customer.UserId, customer.Status } : null,
                    allCustomerTrackers = allCustomerTrackers,
                    customerTrackersCount = allCustomerTrackers.Count,
                    allTrackers = allTrackers,
                    trackersCount = allTrackers.Count,
                    specific = new {
                        customerTracker80Exists = allCustomerTrackers.Any(ct => ct.Id == 80),
                        tracker80Exists = allTrackers.Any(t => t.Id == 80),
                        customerTrackerWithTrackerId80 = allCustomerTrackers.Any(ct => ct.TrackerId == 80)
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Crea CustomerTrackers para todos los trackers que no tienen uno asociado
        /// </summary>
        /// <summary>
        /// DEPRECATED: Este endpoint está deshabilitado por seguridad.
        /// Los trackers NO deben asignarse automáticamente.
        /// Use el Panel Admin para asignar trackers manualmente.
        /// </summary>
        [HttpPost("create-missing-customer-trackers")]
        public IActionResult CreateMissingCustomerTrackers()
        {
            return BadRequest(new {
                success = false,
                error = "SECURITY ERROR: Auto-assignment is disabled. Trackers must be assigned manually from Admin Panel.",
                code = "AUTO_ASSIGNMENT_DISABLED"
            });
        }

        /// <summary>
        /// Endpoint de diagnóstico para verificar datos de tracker
        /// </summary>
        [HttpGet("debug-tracker/{trackerId}")]
        public async Task<IActionResult> DebugTracker(int trackerId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    userId = 1; // Para desarrollo
                }

                var debugInfo = await _farmTrackerService.GetTrackerDebugInfoAsync(trackerId, userId.Value);

                return Ok(debugInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting debug info for tracker {TrackerId}", trackerId);
                return BadRequest(new
                {
                    success = false,
                    message = "Error obteniendo información de debug"
                });
            }
        }

        private async Task EnsureCustomerTrackersExist(int userId)
        {
            try
            {
                // Buscar customer activo para el usuario
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active");

                if (customer == null) return;

                // Encontrar trackers que no tienen CustomerTracker
                var trackersWithoutCustomerTracker = await _context.Trackers
                    .Where(t => !_context.CustomerTrackers.Any(ct => ct.TrackerId == t.Id))
                    .ToListAsync();

                if (trackersWithoutCustomerTracker.Any())
                {
                    foreach (var tracker in trackersWithoutCustomerTracker)
                    {
                        var customerTracker = new CustomerTracker
                        {
                            CustomerId = customer.Id,
                            TrackerId = tracker.Id,
                            // AssignmentMethod = "AutoGenerated", // Comentado porque la propiedad no existe
                            Status = "Active",
                            AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                        };

                        _context.CustomerTrackers.Add(customerTracker);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Auto-created {trackersWithoutCustomerTracker.Count} CustomerTrackers for user {userId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-creating CustomerTrackers for user {UserId}", userId);
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = HttpContext.User.FindFirst("sub")?.Value ??
                             HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    /// <summary>
    /// Request para asignar tracker a animal
    /// </summary>
    public class AssignTrackerToAnimalRequest
    {
        public int CustomerTrackerId { get; set; }
        public int AnimalId { get; set; }
    }
}