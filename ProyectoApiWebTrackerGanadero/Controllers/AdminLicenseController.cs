using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Services;
using ApiWebTrackerGanado.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/admin/licenses")]
    [ApiController]
    [Authorize(Roles = "Admin")] // Solo administradores
    public class AdminLicenseController : ControllerBase
    {
        private readonly LicenseService _licenseService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AdminLicenseController> _logger;

        public AdminLicenseController(
            LicenseService licenseService,
            IUserRepository userRepository,
            ILogger<AdminLicenseController> logger)
        {
            _licenseService = licenseService;
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los usuarios con información de sus licencias
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersWithLicenses()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                var usersWithLicenses = new List<object>();

                foreach (var user in users)
                {
                    var customer = await _licenseService.GetCurrentCustomerAsync(user.Id);

                    // DEBUG: Log para fernandoCrosio
                    if (user.Name == "fernandoCrosio")
                    {
                        _logger.LogWarning("DEBUG fernandoCrosio: UserId={UserId}, Customer={Customer}",
                            user.Id, customer != null ? $"CustomerId={customer.Id}" : "NULL");
                    }

                    if (customer == null)
                    {
                        // Usuario sin customer/licencia
                        usersWithLicenses.Add(new
                        {
                            userId = user.Id,
                            username = user.Name,
                            email = user.Email,
                            role = user.Role,
                            createdAt = user.CreatedAt,
                            hasLicense = false,
                            customerId = (int?)null,
                            companyName = (string?)null,
                            licenseType = (string?)null,
                            status = "No License",
                            expiresAt = (DateTime?)null,
                            daysRemaining = (int?)null,
                            isTrial = false,
                            isExpired = false
                        });
                    }
                    else
                    {
                        var licenses = await _licenseService.GetCustomerLicensesAsync(customer.Id);

                        // DEBUG: Log para fernandoCrosio
                        if (user.Name == "fernandoCrosio")
                        {
                            _logger.LogWarning("DEBUG fernandoCrosio: Total licenses={Count}", licenses.Count);
                            foreach (var lic in licenses)
                            {
                                _logger.LogWarning("DEBUG fernandoCrosio License: Id={Id}, Type={Type}, Status={Status}, " +
                                    "ActivatedAt={ActivatedAt}, ExpiresAt={ExpiresAt}, IsValid={IsValid}",
                                    lic.Id, lic.LicenseType, lic.Status, lic.ActivatedAt, lic.ExpiresAt, lic.IsValid);
                            }
                        }

                        var activeLicense = licenses
                            .Where(l => l.IsValid)
                            .OrderByDescending(l => l.ExpiresAt)
                            .FirstOrDefault();

                        // DEBUG: Log para fernandoCrosio
                        if (user.Name == "fernandoCrosio")
                        {
                            _logger.LogWarning("DEBUG fernandoCrosio: ActiveLicense={ActiveLicense}",
                                activeLicense != null ? $"Id={activeLicense.Id}, Type={activeLicense.LicenseType}" : "NULL");
                        }

                        usersWithLicenses.Add(new
                        {
                            userId = user.Id,
                            username = user.Name,
                            email = user.Email,
                            role = user.Role,
                            createdAt = user.CreatedAt,
                            hasLicense = activeLicense != null,
                            licenseId = activeLicense?.Id,
                            customerId = customer.Id,
                            companyName = customer.CompanyName,
                            licenseType = activeLicense?.LicenseType ?? "None",
                            status = activeLicense?.Status ?? "Inactive",
                            expiresAt = activeLicense?.ExpiresAt,
                            daysRemaining = activeLicense != null ?
                                (activeLicense.ExpiresAt - DateTime.UtcNow).Days : (int?)null,
                            isTrial = activeLicense?.LicenseType == "Trial",
                            isExpired = activeLicense != null && !activeLicense.IsValid,
                            maxTrackers = activeLicense?.MaxTrackers,
                            maxFarms = activeLicense?.MaxFarms,
                            activeTrackers = customer.CustomerTrackers.Count(ct => ct.Status == "Active")
                        });
                    }
                }

                return Ok(new
                {
                    Success = true,
                    Users = usersWithLicenses.OrderByDescending(u => ((dynamic)u).createdAt)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users with licenses");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error retrieving users with licenses"
                });
            }
        }

        /// <summary>
        /// Genera una nueva licencia para un usuario específico
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateLicense([FromBody] GenerateLicenseRequest request)
        {
            try
            {
                _logger.LogInformation("Generating license for userId: {UserId}, type: {LicenseType}",
                    request.UserId, request.LicenseType);

                // Validar que el usuario existe
                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        Message = "Usuario no encontrado"
                    });
                }

                // Obtener o crear customer para el usuario
                var customer = await _licenseService.GetCurrentCustomerAsync(request.UserId);

                if (customer == null)
                {
                    // Crear un nuevo customer si no existe
                    customer = new Customer
                    {
                        UserId = request.UserId,
                        CompanyName = request.CompanyName ?? user.Name,
                        Plan = request.LicenseType,
                        Status = "Active",
                        SubscriptionStart = DateTime.UtcNow,
                        SubscriptionEnd = CalculateExpirationDate(request.DurationMonths),
                        TrackerLimit = GetTrackerLimit(request.LicenseType),
                        FarmLimit = GetFarmLimit(request.LicenseType)
                    };

                    await _licenseService.CreateCustomerAsync(customer);
                }
                else
                {
                    // Actualizar customer existente
                    customer.Plan = request.LicenseType;
                    customer.Status = "Active";
                    customer.SubscriptionEnd = CalculateExpirationDate(request.DurationMonths);
                    customer.TrackerLimit = GetTrackerLimit(request.LicenseType);
                    customer.FarmLimit = GetFarmLimit(request.LicenseType);

                    await _licenseService.UpdateCustomerAsync(customer);
                }

                // IMPORTANTE: Revocar todas las licencias activas anteriores antes de generar la nueva
                var existingLicenses = await _licenseService.GetCustomerLicensesAsync(customer.Id);
                foreach (var existingLicense in existingLicenses.Where(l => l.IsValid && l.Status == "Active"))
                {
                    _logger.LogInformation("Revoking previous license {LicenseId} ({LicenseType}) for customer {CustomerId}",
                        existingLicense.Id, existingLicense.LicenseType, customer.Id);
                    await _licenseService.RevokeLicenseAsync(existingLicense.Id);
                }

                // Generar la nueva licencia
                var expiresAt = CalculateExpirationDate(request.DurationMonths);
                var license = await _licenseService.GenerateLicenseAsync(
                    customerId: customer.Id,
                    licenseType: request.LicenseType,
                    maxTrackers: GetTrackerLimit(request.LicenseType),
                    maxFarms: GetFarmLimit(request.LicenseType),
                    maxUsers: GetUserLimit(request.LicenseType),
                    expiresAt: expiresAt,
                    notes: $"Licencia generada por admin. Duración: {request.DurationMonths} meses"
                );

                // IMPORTANTE: Activar automáticamente la licencia generada por admin
                license.ActivatedAt = DateTime.UtcNow;
                await _licenseService.UpdateLicenseAsync(license);

                _logger.LogInformation("License generated and activated successfully: {LicenseKey}", license.LicenseKey);

                return Ok(new
                {
                    Success = true,
                    Message = "Licencia generada exitosamente",
                    LicenseKey = license.LicenseKey,
                    License = new
                    {
                        id = license.Id,
                        licenseKey = license.LicenseKey,
                        licenseType = license.LicenseType,
                        maxTrackers = license.MaxTrackers,
                        maxFarms = license.MaxFarms,
                        maxUsers = license.MaxUsers,
                        status = license.Status,
                        issuedAt = license.IssuedAt,
                        expiresAt = license.ExpiresAt,
                        daysValid = (license.ExpiresAt - DateTime.UtcNow).Days
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating license for user {UserId}", request.UserId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error generando licencia: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Revoca una licencia existente
        /// </summary>
        [HttpPut("{licenseId}/revoke")]
        public async Task<IActionResult> RevokeLicense(int licenseId)
        {
            try
            {
                var success = await _licenseService.RevokeLicenseAsync(licenseId);

                if (!success)
                {
                    return NotFound(new
                    {
                        success = false,
                        Message = "Licencia no encontrada"
                    });
                }

                _logger.LogInformation("License {LicenseId} revoked successfully", licenseId);

                return Ok(new
                {
                    Success = true,
                    Message = "Licencia revocada exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking license {LicenseId}", licenseId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error revocando licencia"
                });
            }
        }

        /// <summary>
        /// Elimina físicamente una licencia de la base de datos
        /// IMPORTANTE: Esta es una operación destructiva que no se puede deshacer
        /// </summary>
        [HttpDelete("{licenseId}")]
        public async Task<IActionResult> DeleteLicense(int licenseId)
        {
            try
            {
                var success = await _licenseService.DeleteLicenseAsync(licenseId);

                if (!success)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "Licencia no encontrada"
                    });
                }

                _logger.LogInformation("License {LicenseId} deleted successfully from database", licenseId);

                return Ok(new
                {
                    Success = true,
                    Message = "Licencia eliminada permanentemente de la base de datos"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting license {LicenseId}", licenseId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error eliminando licencia de la base de datos"
                });
            }
        }

        /// <summary>
        /// Extiende la fecha de expiración de una licencia
        /// </summary>
        [HttpPut("{licenseId}/extend")]
        public async Task<IActionResult> ExtendLicense(int licenseId, [FromBody] ExtendLicenseRequest request)
        {
            try
            {
                var license = await _licenseService.GetLicenseByIdAsync(licenseId);

                if (license == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        Message = "Licencia no encontrada"
                    });
                }

                var newExpirationDate = license.ExpiresAt.AddMonths(request.AdditionalMonths);
                var success = await _licenseService.ExtendLicenseAsync(licenseId, newExpirationDate);

                if (!success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        Message = "No se pudo extender la licencia"
                    });
                }

                _logger.LogInformation("License {LicenseId} extended by {Months} months",
                    licenseId, request.AdditionalMonths);

                return Ok(new
                {
                    Success = true,
                    Message = $"Licencia extendida por {request.AdditionalMonths} meses",
                    newExpirationDate = newExpirationDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending license {LicenseId}", licenseId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error extendiendo licencia"
                });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de licencias
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetLicenseStats()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                int totalUsers = users.Count();
                int usersWithLicense = 0;
                int trialLicenses = 0;
                int activeLicenses = 0;
                int expiredLicenses = 0;

                foreach (var user in users)
                {
                    var customer = await _licenseService.GetCurrentCustomerAsync(user.Id);
                    if (customer != null)
                    {
                        var licenses = await _licenseService.GetCustomerLicensesAsync(customer.Id);
                        var activeLicense = licenses
                            .Where(l => l.IsValid)
                            .OrderByDescending(l => l.ExpiresAt)
                            .FirstOrDefault();

                        if (activeLicense != null)
                        {
                            usersWithLicense++;
                            if (activeLicense.LicenseType == "Trial")
                                trialLicenses++;
                            else
                                activeLicenses++;
                        }
                        else if (licenses.Any())
                        {
                            expiredLicenses++;
                        }
                    }
                }

                return Ok(new
                {
                    Success = true,
                    Stats = new
                    {
                        totalUsers,
                        usersWithLicense,
                        usersWithoutLicense = totalUsers - usersWithLicense,
                        trialLicenses,
                        activeLicenses,
                        expiredLicenses
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting license stats");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error obteniendo estadísticas"
                });
            }
        }

        #region Helper Methods

        private DateTime CalculateExpirationDate(int months)
        {
            if (months == -1) // Permanente
            {
                return DateTime.UtcNow.AddYears(100);
            }
            return DateTime.UtcNow.AddMonths(months);
        }

        private int GetTrackerLimit(string licenseType)
        {
            return licenseType switch
            {
                "Trial" => 5,
                "Basic" => 20,
                "Premium" => 100,
                "Enterprise" => 99999,
                _ => 5
            };
        }

        private int GetFarmLimit(string licenseType)
        {
            return licenseType switch
            {
                "Trial" => 1,
                "Basic" => 3,
                "Premium" => 15,
                "Enterprise" => 99999,
                _ => 1
            };
        }

        private int GetUserLimit(string licenseType)
        {
            return licenseType switch
            {
                "Trial" => 1,
                "Basic" => 2,
                "Premium" => 5,
                "Enterprise" => 20,
                _ => 1
            };
        }

        #endregion
    }

    #region DTOs

    public class GenerateLicenseRequest
    {
        public int UserId { get; set; }
        public string LicenseType { get; set; } = "Basic"; // Trial, Basic, Premium, Enterprise
        public int DurationMonths { get; set; } = 12; // -1 = Permanente
        public string? CompanyName { get; set; }
    }

    public class ExtendLicenseRequest
    {
        public int AdditionalMonths { get; set; }
    }

    #endregion
}
