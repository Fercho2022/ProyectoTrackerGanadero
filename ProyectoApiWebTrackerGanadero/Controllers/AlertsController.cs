using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiWebTrackerGanado.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlertsController : ControllerBase
    {
        private readonly IAlertService _alertService;
        private readonly IAlertRepository _alertRepository;
        private readonly CattleTrackingContext _context;

        public AlertsController(IAlertService alertService, IAlertRepository alertRepository, CattleTrackingContext context)
        {
            _alertService = alertService;
            _alertRepository = alertRepository;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AlertDto>>> GetAllAlerts([FromQuery] bool? isResolved = null)
        {
            try
            {
                // SECURITY FIX: Filter alerts by current user's farms
                var userId = GetCurrentUserId();

                // Get all alerts that belong to the user's farms
                var alerts = await _context.Alerts
                    .Include(a => a.Animal)
                        .ThenInclude(an => an.Farm)
                    .Where(a => a.Animal.Farm.UserId == userId)
                    .ToListAsync();

                var alertDtos = alerts.Select(a => new AlertDto
                {
                    Id = a.Id,
                    Type = a.Type,
                    Title = GetAlertTitle(a.Type, a.Severity),
                    Severity = a.Severity,
                    Message = a.Message,
                    AnimalId = a.AnimalId,
                    FarmId = a.Animal?.FarmId,
                    AnimalName = a.Animal?.Name ?? "N/A",
                    FarmName = a.Animal?.Farm?.Name,
                    IsRead = a.IsRead,
                    IsResolved = a.IsResolved,
                    CreatedAt = a.CreatedAt,
                    ResolvedAt = a.ResolvedAt
                });

                if (isResolved.HasValue)
                    alertDtos = alertDtos.Where(a => a.IsResolved == isResolved.Value);

                return Ok(alertDtos.OrderByDescending(a => a.CreatedAt));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error getting alerts: {ex.Message}" });
            }
        }

        [HttpGet("farm/{farmId}")]
        public async Task<ActionResult<IEnumerable<AlertDto>>> GetFarmAlerts(int farmId, [FromQuery] bool onlyActive = true)
        {
            try
            {
                // SECURITY FIX: Verify farm ownership
                var userId = GetCurrentUserId();
                var hasAccess = await VerifyFarmOwnershipAsync(farmId, userId);

                if (!hasAccess)
                {
                    return Forbid();
                }

                var alerts = await _alertService.GetActiveAlertsAsync(farmId);
                return Ok(alerts);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error getting farm alerts: {ex.Message}" });
            }
        }

        [HttpGet("farm/{farmId}/critical")]
        public async Task<ActionResult<IEnumerable<AlertDto>>> GetCriticalAlerts(int farmId)
        {
            try
            {
                // SECURITY FIX: Verify farm ownership
                var userId = GetCurrentUserId();
                var hasAccess = await VerifyFarmOwnershipAsync(farmId, userId);

                if (!hasAccess)
                {
                    return Forbid();
                }

                var criticalAlerts = await _alertRepository.GetCriticalAlertsAsync(farmId);
                var alertDtos = criticalAlerts.Select(a => new AlertDto
                {
                    Id = a.Id,
                    Type = a.Type,
                    Severity = a.Severity,
                    Message = a.Message,
                    AnimalId = a.AnimalId,
                    AnimalName = a.Animal.Name,
                    IsRead = a.IsRead,
                    IsResolved = a.IsResolved,
                    CreatedAt = a.CreatedAt
                });

                return Ok(alertDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error getting critical alerts: {ex.Message}" });
            }
        }

        [HttpGet("farm/{farmId}/unread-count")]
        public async Task<ActionResult<int>> GetUnreadAlertsCount(int farmId)
        {
            try
            {
                // SECURITY FIX: Verify farm ownership
                var userId = GetCurrentUserId();
                var hasAccess = await VerifyFarmOwnershipAsync(farmId, userId);

                if (!hasAccess)
                {
                    return Forbid();
                }

                var count = await _alertRepository.GetUnreadAlertsCountAsync(farmId);
                return Ok(count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error getting unread alerts count: {ex.Message}" });
            }
        }

        [HttpPut("{alertId}/read")]
        public async Task<IActionResult> MarkAsRead(int alertId)
        {
            try
            {
                // SECURITY FIX: Verify alert ownership
                var userId = GetCurrentUserId();
                var hasAccess = await VerifyAlertOwnershipAsync(alertId, userId);

                if (!hasAccess)
                {
                    return Forbid();
                }

                await _alertService.MarkAlertAsReadAsync(alertId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error marking alert as read: {ex.Message}" });
            }
        }

        [HttpPut("{alertId}/resolve")]
        public async Task<IActionResult> ResolveAlert(int alertId)
        {
            try
            {
                // SECURITY FIX: Verify alert ownership
                var userId = GetCurrentUserId();
                var hasAccess = await VerifyAlertOwnershipAsync(alertId, userId);

                if (!hasAccess)
                {
                    return Forbid();
                }

                await _alertService.ResolveAlertAsync(alertId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error resolving alert: {ex.Message}" });
            }
        }

        [HttpGet("animal/{animalId}")]
        public async Task<ActionResult<IEnumerable<AlertDto>>> GetAnimalAlerts(int animalId, [FromQuery] bool onlyActive = true)
        {
            try
            {
                // SECURITY FIX: Verify animal ownership
                var userId = GetCurrentUserId();
                var hasAccess = await VerifyAnimalOwnershipAsync(animalId, userId);

                if (!hasAccess)
                {
                    return Forbid();
                }

                var alerts = await _alertRepository.GetAnimalAlertsAsync(animalId, onlyActive);
                var alertDtos = alerts.Select(a => new AlertDto
                {
                    Id = a.Id,
                    Type = a.Type,
                    Severity = a.Severity,
                    Message = a.Message,
                    AnimalId = a.AnimalId,
                    AnimalName = a.Animal?.Name ?? "Unknown",
                    IsRead = a.IsRead,
                    IsResolved = a.IsResolved,
                    CreatedAt = a.CreatedAt
                });

                return Ok(alertDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error getting animal alerts: {ex.Message}" });
            }
        }

        private static string GetAlertTitle(string type, string severity)
        {
            return type switch
            {
                "OutOfBounds" => "🚨 Animal Fuera del Área",
                "LowActivity" => "😴 Baja Actividad",
                "HighActivity" => "🏃 Alta Actividad",
                "Immobility" => "🛑 Animal Inmóvil",
                "PossibleHeat" => "🔥 Posible Celo",
                _ => $"⚠️ Alerta {severity}"
            };
        }

        // SECURITY: Helper methods to extract user context from JWT token
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                userIdClaim = User.FindFirst("sub")?.Value;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            throw new UnauthorizedAccessException("Could not determine user ID from token claims.");
        }

        private async Task<int> GetCustomerIdByUserIdAsync(int userId)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null)
            {
                throw new InvalidOperationException($"No customer found for user ID {userId}");
            }

            return customer.Id;
        }

        // SECURITY: Verify farm ownership
        private async Task<bool> VerifyFarmOwnershipAsync(int farmId, int userId)
        {
            var farm = await _context.Farms
                .FirstOrDefaultAsync(f => f.Id == farmId && f.UserId == userId);

            return farm != null;
        }

        // SECURITY: Verify animal ownership
        private async Task<bool> VerifyAnimalOwnershipAsync(int animalId, int userId)
        {
            var animal = await _context.Animals
                .Include(a => a.Farm)
                .FirstOrDefaultAsync(a => a.Id == animalId && a.Farm.UserId == userId);

            return animal != null;
        }

        // SECURITY: Verify alert ownership
        private async Task<bool> VerifyAlertOwnershipAsync(int alertId, int userId)
        {
            var alert = await _context.Alerts
                .Include(a => a.Animal)
                    .ThenInclude(an => an.Farm)
                .FirstOrDefaultAsync(a => a.Id == alertId && a.Animal.Farm.UserId == userId);

            return alert != null;
        }
    }
}
