using ApiWebTrackerGanado.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LicenseController : ControllerBase
    {
        private readonly LicenseService _licenseService;
        private readonly ILogger<LicenseController> _logger;

        public LicenseController(
            LicenseService licenseService,
            ILogger<LicenseController> logger)
        {
            _licenseService = licenseService;
            _logger = logger;
        }

        /// <summary>
        /// Valida y activa una licencia para el usuario actual
        /// </summary>
        [HttpPost("activate")]
        public async Task<IActionResult> ActivateLicense([FromBody] ActivateLicenseRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized("Usuario no autenticado");

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                var result = await _licenseService.ValidateAndActivateLicenseAsync(
                    request.LicenseKey,
                    userId.Value,
                    ipAddress,
                    request.HardwareId);

                if (!result.IsValid)
                {
                    return BadRequest(new {
                        success = false,
                        message = result.ErrorMessage
                    });
                }

                return Ok(new {
                    success = true,
                    message = result.Message,
                    license = new {
                        licenseType = result.License?.LicenseType,
                        maxTrackers = result.License?.MaxTrackers,
                        maxFarms = result.License?.MaxFarms,
                        expiresAt = result.License?.ExpiresAt
                    },
                    customer = new {
                        companyName = result.Customer?.CompanyName,
                        plan = result.Customer?.Plan,
                        trackerLimit = result.Customer?.TrackerLimit,
                        farmLimit = result.Customer?.FarmLimit
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating license {LicenseKey}", request.LicenseKey);
                return BadRequest(new {
                    success = false,
                    message = "Error interno del servidor"
                });
            }
        }

        /// <summary>
        /// Obtiene las licencias del cliente actual
        /// </summary>
        [HttpGet("my-licenses")]
        public async Task<IActionResult> GetMyLicenses()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized("Usuario no autenticado");

                var customer = await _licenseService.GetCurrentCustomerAsync(userId.Value);
                if (customer == null)
                {
                    return Ok(new {
                        success = true,
                        licenses = new List<object>(),
                        message = "No hay licencias activas. Active una licencia para comenzar."
                    });
                }

                var licenses = await _licenseService.GetCustomerLicensesAsync(customer.Id);

                return Ok(new {
                    success = true,
                    licenses = licenses.Select(l => new {
                        id = l.Id,
                        licenseKey = MaskLicenseKey(l.LicenseKey),
                        licenseType = l.LicenseType,
                        maxTrackers = l.MaxTrackers,
                        maxFarms = l.MaxFarms,
                        status = l.Status,
                        isValid = l.IsValid,
                        issuedAt = l.IssuedAt,
                        activatedAt = l.ActivatedAt,
                        expiresAt = l.ExpiresAt,
                        daysUntilExpiration = l.DaysUntilExpiration
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving licenses for user {UserId}", GetCurrentUserId());
                return BadRequest(new {
                    success = false,
                    message = "Error obteniendo licencias"
                });
            }
        }

        /// <summary>
        /// Obtiene información del cliente actual
        /// </summary>
        [HttpGet("customer-info")]
        public async Task<IActionResult> GetCustomerInfo()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized("Usuario no autenticado");

                _logger.LogWarning("DEBUG customer-info: UserId={UserId}", userId);

                var customer = await _licenseService.GetCurrentCustomerAsync(userId.Value);
                if (customer == null)
                {
                    _logger.LogWarning("DEBUG customer-info: No customer found for UserId={UserId}", userId);
                    return Ok(new {
                        success = true,
                        hasCustomer = false,
                        message = "No hay información de cliente. Active una licencia para comenzar."
                    });
                }

                _logger.LogWarning("DEBUG customer-info: Found customer={CustomerId}, Licenses count={Count}",
                    customer.Id, customer.Licenses?.Count ?? 0);

                var activeTrackers = customer.CustomerTrackers.Count(ct => ct.Status == "Active");
                var activeLicense = customer.Licenses
                    .Where(l => l.IsValid())
                    .OrderByDescending(l => l.ExpiresAt)
                    .FirstOrDefault();

                _logger.LogWarning("DEBUG customer-info: ActiveLicense={ActiveLicense}",
                    activeLicense != null ? $"Type={activeLicense.LicenseType}, Id={activeLicense.Id}" : "NULL");

                // IMPORTANTE: Si no hay licencia activa válida, devolver hasCustomer=false
                // Esto asegura que la app MAUI bloquee el acceso a funciones de trackers
                if (activeLicense == null)
                {
                    _logger.LogWarning("DEBUG customer-info: No active license found, returning hasCustomer=false");
                    return Ok(new {
                        success = true,
                        hasCustomer = false,
                        message = "No hay licencia activa. Active una licencia para acceder a las funciones de trackers."
                    });
                }

                return Ok(new {
                    success = true,
                    hasCustomer = true,
                    customer = new {
                        id = customer.Id,
                        companyName = customer.CompanyName,
                        plan = customer.Plan,
                        status = customer.Status,
                        trackerLimit = customer.TrackerLimit,
                        farmLimit = customer.FarmLimit,
                        activeTrackers = activeTrackers,
                        availableTrackers = customer.TrackerLimit - activeTrackers,
                        subscriptionStart = customer.SubscriptionStart,
                        subscriptionEnd = customer.SubscriptionEnd,
                        isSubscriptionActive = customer.IsSubscriptionActive()
                    },
                    activeLicense = activeLicense != null ? new {
                        licenseType = activeLicense.LicenseType,
                        expiresAt = activeLicense.ExpiresAt,
                        daysUntilExpiration = (activeLicense.ExpiresAt - DateTime.UtcNow).Days
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer info for user {UserId}", GetCurrentUserId());
                return BadRequest(new {
                    success = false,
                    message = "Error obteniendo información del cliente"
                });
            }
        }

        /// <summary>
        /// Verifica si el cliente puede realizar una acción específica
        /// </summary>
        [HttpGet("can-perform/{action}")]
        public async Task<IActionResult> CanPerformAction(string action)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized("Usuario no autenticado");

                var customer = await _licenseService.GetCurrentCustomerAsync(userId.Value);
                if (customer == null)
                {
                    return Ok(new {
                        canPerform = false,
                        reason = "No hay licencia activa"
                    });
                }

                var canPerform = await _licenseService.CanPerformActionAsync(customer.Id, action);

                return Ok(new {
                    canPerform = canPerform,
                    reason = canPerform ? null : GetActionLimitationReason(customer, action)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permissions for action {Action}", action);
                return BadRequest(new {
                    success = false,
                    message = "Error verificando permisos"
                });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = HttpContext.User.FindFirst("sub")?.Value ??
                             HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private string MaskLicenseKey(string licenseKey)
        {
            if (licenseKey.Length <= 8)
                return licenseKey;

            var parts = licenseKey.Split('-');
            if (parts.Length > 2)
            {
                return $"{parts[0]}-{parts[1]}-****-****-****";
            }

            return $"{licenseKey.Substring(0, 4)}****{licenseKey.Substring(licenseKey.Length - 4)}";
        }

        /// <summary>
        /// Activa una licencia sin requerir autenticación previa (para usuarios nuevos)
        /// </summary>
        [HttpPost("activate-public")]
        public async Task<IActionResult> ActivateLicensePublic([FromBody] ActivateLicenseRequest request)
        {
            try
            {
                _logger.LogInformation("Starting license activation for key: {LicenseKey}", request.LicenseKey);

                // Usar userId = 1 por defecto para pruebas
                var userId = 1;
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                _logger.LogInformation("Using userId: {UserId}, IP: {IpAddress}, HardwareId: {HardwareId}",
                    userId, ipAddress, request.HardwareId);

                var result = await _licenseService.ValidateAndActivateLicenseAsync(
                    request.LicenseKey,
                    userId,
                    ipAddress,
                    request.HardwareId);

                _logger.LogInformation("License validation result: IsValid={IsValid}, Message={Message}",
                    result.IsValid, result.Message);

                if (!result.IsValid)
                {
                    return BadRequest(new {
                        success = false,
                        message = result.ErrorMessage
                    });
                }

                return Ok(new {
                    success = true,
                    message = result.Message,
                    license = new {
                        licenseType = result.License?.LicenseType,
                        maxTrackers = result.License?.MaxTrackers,
                        maxFarms = result.License?.MaxFarms,
                        expiresAt = result.License?.ExpiresAt
                    },
                    customer = new {
                        companyName = result.Customer?.CompanyName,
                        plan = result.Customer?.Plan,
                        trackerLimit = result.Customer?.TrackerLimit,
                        farmLimit = result.Customer?.FarmLimit
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating license {LicenseKey}: {ErrorMessage}",
                    request.LicenseKey, ex.Message);
                return BadRequest(new {
                    success = false,
                    message = $"Error interno del servidor: {ex.Message}"
                });
            }
        }

        private string GetActionLimitationReason(Models.Customer customer, string action)
        {
            return action.ToLower() switch
            {
                "add_tracker" => $"Ha alcanzado el límite de {customer.TrackerLimit} trackers",
                "add_farm" => $"Ha alcanzado el límite de {customer.FarmLimit} granjas",
                "view_reports" => "Su plan no incluye reportes avanzados",
                "realtime_map" => "Su plan no incluye mapa en tiempo real",
                "alerts" => "Su plan no incluye alertas",
                _ => "Acción no permitida por su plan actual"
            };
        }
    }

    public class ActivateLicenseRequest
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string? HardwareId { get; set; }
    }
}