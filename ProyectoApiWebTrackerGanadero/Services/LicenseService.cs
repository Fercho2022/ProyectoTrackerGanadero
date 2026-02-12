using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ApiWebTrackerGanado.Services
{
    /// <summary>
    /// Servicio para gestión de licencias y validación de clientes
    /// </summary>
    public class LicenseService
    {
        private readonly CattleTrackingContext _context;
        private readonly ILogger<LicenseService> _logger;

        public LicenseService(
            CattleTrackingContext context,
            ILogger<LicenseService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Valida y activa una licencia para un usuario
        /// </summary>
        public async Task<LicenseValidationResult> ValidateAndActivateLicenseAsync(
            string licenseKey,
            int userId,
            string ipAddress,
            string? hardwareId = null)
        {
            try
            {
                var result = new LicenseValidationResult();

                // Buscar la licencia
                var license = await _context.Licenses
                    .Include(l => l.Customer)
                    .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey);

                if (license == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Clave de licencia no encontrada";
                    return result;
                }

                // Verificar si ya está activada
                if (license.ActivatedAt.HasValue)
                {
                    // IMPORTANTE: Verificar si es el mismo usuario
                    // La licencia puede estar activada Y tener un Customer asociado
                    if (license.Customer != null && license.Customer.UserId == userId)
                    {
                        _logger.LogInformation("License {LicenseKey} already activated for user {UserId}", licenseKey, userId);
                        result.IsValid = true;
                        result.License = license;
                        result.Message = "Licencia ya activada para este usuario";
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("License {LicenseKey} activation rejected: already activated by user {OtherUserId}, attempted by user {UserId}",
                            licenseKey, license.Customer?.UserId ?? -1, userId);
                        result.IsValid = false;
                        result.ErrorMessage = "Esta licencia ya está activada por otro usuario";
                        return result;
                    }
                }

                // NUEVA VALIDACIÓN: Verificar si la licencia ya tiene un Customer asignado (licencia creada por admin para usuario específico)
                if (license.CustomerId > 0 && license.Customer != null)
                {
                    // La licencia fue creada para un usuario específico, verificar que coincida
                    if (license.Customer.UserId != userId)
                    {
                        _logger.LogWarning("License {LicenseKey} activation rejected: created for user {TargetUserId}, attempted by user {UserId}",
                            licenseKey, license.Customer.UserId, userId);
                        result.IsValid = false;
                        result.ErrorMessage = "Esta licencia fue creada para otro usuario. Por favor solicite una licencia propia.";
                        return result;
                    }
                }

                // Verificar si puede ser activada
                if (!license.CanBeActivated())
                {
                    result.IsValid = false;
                    result.ErrorMessage = "La licencia no puede ser activada (expirada o inactiva)";
                    return result;
                }

                // Crear o actualizar el customer
                var customer = await GetOrCreateCustomerAsync(userId, license);

                // Activar la licencia
                license.Activate(ipAddress, hardwareId);
                license.CustomerId = customer.Id;

                await _context.SaveChangesAsync();

                result.IsValid = true;
                result.License = license;
                result.Customer = customer;
                result.Message = "Licencia activada exitosamente";

                _logger.LogInformation($"License {licenseKey} activated successfully for user {userId}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating license {LicenseKey} for user {UserId}", licenseKey, userId);
                throw;
            }
        }

        /// <summary>
        /// Obtiene o crea un customer para el usuario
        /// </summary>
        private async Task<Customer> GetOrCreateCustomerAsync(int userId, License license)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    throw new ArgumentException($"User {userId} not found");

                customer = new Customer
                {
                    UserId = userId,
                    CompanyName = user.Name,
                    Plan = license.LicenseType,
                    TrackerLimit = license.MaxTrackers,
                    FarmLimit = license.MaxFarms,
                    Status = "Active",
                    SubscriptionStart = DateTime.UtcNow,
                    SubscriptionEnd = license.ExpiresAt
                };

                _context.Customers.Add(customer);
            }
            else
            {
                // Actualizar límites según la nueva licencia
                customer.Plan = license.LicenseType;
                customer.TrackerLimit = license.MaxTrackers;
                customer.FarmLimit = license.MaxFarms;
                customer.Status = "Active";
                customer.SubscriptionEnd = license.ExpiresAt;
                customer.UpdatedAt = DateTime.UtcNow;
            }

            return customer;
        }

        /// <summary>
        /// Obtiene las licencias de un cliente
        /// </summary>
        public async Task<List<LicenseDto>> GetCustomerLicensesAsync(int customerId)
        {
            try
            {
                var licenses = await _context.Licenses
                    .Where(l => l.CustomerId == customerId)
                    .Select(l => new LicenseDto
                    {
                        Id = l.Id,
                        LicenseKey = l.LicenseKey,
                        LicenseType = l.LicenseType,
                        MaxTrackers = l.MaxTrackers,
                        MaxFarms = l.MaxFarms,
                        MaxUsers = l.MaxUsers,
                        Features = l.Features,
                        Status = l.Status,
                        IssuedAt = l.IssuedAt,
                        ActivatedAt = l.ActivatedAt,
                        ExpiresAt = l.ExpiresAt,
                        IsValid = l.IsValid(),
                        DaysUntilExpiration = (l.ExpiresAt - DateTime.UtcNow).Days
                    })
                    .OrderByDescending(l => l.IssuedAt)
                    .ToListAsync();

                return licenses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving licenses for customer {CustomerId}", customerId);
                throw;
            }
        }

        /// <summary>
        /// Genera una nueva licencia para un cliente (solo para administradores)
        /// </summary>
        public async Task<License> GenerateLicenseAsync(
            int customerId,
            string licenseType,
            int maxTrackers,
            int maxFarms,
            int maxUsers,
            DateTime expiresAt,
            string? features = null,
            string? notes = null)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                    throw new ArgumentException($"Customer {customerId} not found");

                var license = new License
                {
                    CustomerId = customerId,
                    LicenseKey = License.GenerateLicenseKey(),
                    LicenseType = licenseType,
                    MaxTrackers = maxTrackers,
                    MaxFarms = maxFarms,
                    MaxUsers = maxUsers,
                    Features = features,
                    Status = "Active",
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    Notes = notes
                };

                _context.Licenses.Add(license);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Generated new license {license.LicenseKey} for customer {customerId}");
                return license;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating license for customer {CustomerId}", customerId);
                throw;
            }
        }

        /// <summary>
        /// Obtiene el customer actual del usuario
        /// </summary>
        public async Task<Customer?> GetCurrentCustomerAsync(int userId)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.User)
                    .Include(c => c.Licenses)
                    .Include(c => c.CustomerTrackers)
                    .ThenInclude(ct => ct.Tracker)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Verifica si un customer puede realizar una acción específica
        /// </summary>
        public async Task<bool> CanPerformActionAsync(int customerId, string action, object? parameters = null)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.CustomerTrackers)
                    .Include(c => c.Licenses)
                    .FirstOrDefaultAsync(c => c.Id == customerId);

                if (customer == null || !customer.IsSubscriptionActive())
                    return false;

                return action.ToLower() switch
                {
                    "add_tracker" => customer.CanAddMoreTrackers(),
                    "add_farm" => await CanAddFarmAsync(customer),
                    "view_reports" => HasFeature(customer, "reports"),
                    "realtime_map" => HasFeature(customer, "realTimeMap"),
                    "alerts" => HasFeature(customer, "alerts"),
                    _ => true // Default allow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permissions for customer {CustomerId}, action {Action}", customerId, action);
                return false;
            }
        }

        /// <summary>
        /// Verifica si el customer puede agregar más granjas
        /// </summary>
        private async Task<bool> CanAddFarmAsync(Customer customer)
        {
            var farmCount = await _context.Farms
                .CountAsync(f => f.UserId == customer.UserId);

            return farmCount < customer.FarmLimit;
        }

        /// <summary>
        /// Verifica si el customer tiene una funcionalidad específica
        /// </summary>
        private bool HasFeature(Customer customer, string featureName)
        {
            var activeLicense = customer.Licenses
                .Where(l => l.IsValid())
                .OrderByDescending(l => l.ExpiresAt)
                .FirstOrDefault();

            if (activeLicense?.Features == null)
                return true; // Default allow if no features specified

            try
            {
                var features = JsonSerializer.Deserialize<Dictionary<string, object>>(activeLicense.Features);
                return features.ContainsKey(featureName) && Convert.ToBoolean(features[featureName]);
            }
            catch
            {
                return true; // Default allow on parse error
            }
        }

        /// <summary>
        /// Crea un nuevo customer
        /// </summary>
        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            try
            {
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new customer {CompanyName} for user {UserId}",
                    customer.CompanyName, customer.UserId);

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer for user {UserId}", customer.UserId);
                throw;
            }
        }

        /// <summary>
        /// Actualiza un customer existente
        /// </summary>
        public async Task<Customer> UpdateCustomerAsync(Customer customer)
        {
            try
            {
                customer.UpdatedAt = DateTime.UtcNow;
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated customer {CustomerId}", customer.Id);

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {CustomerId}", customer.Id);
                throw;
            }
        }

        /// <summary>
        /// Obtiene una licencia por su ID
        /// </summary>
        public async Task<License?> GetLicenseByIdAsync(int licenseId)
        {
            try
            {
                return await _context.Licenses
                    .Include(l => l.Customer)
                    .FirstOrDefaultAsync(l => l.Id == licenseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting license {LicenseId}", licenseId);
                throw;
            }
        }

        /// <summary>
        /// Revoca una licencia
        /// </summary>
        public async Task<bool> RevokeLicenseAsync(int licenseId)
        {
            try
            {
                var license = await _context.Licenses.FindAsync(licenseId);

                if (license == null)
                {
                    _logger.LogWarning("DEBUG RevokeLicense: License {LicenseId} not found", licenseId);
                    return false;
                }

                _logger.LogWarning("DEBUG RevokeLicense: Revoking license {LicenseId}, Type={Type}, CustomerId={CustomerId}, OldStatus={OldStatus}",
                    licenseId, license.LicenseType, license.CustomerId, license.Status);

                license.Status = "Revoked";
                license.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogWarning("DEBUG RevokeLicense: License {LicenseId} revoked successfully, NewStatus={NewStatus}",
                    licenseId, license.Status);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking license {LicenseId}", licenseId);
                throw;
            }
        }

        /// <summary>
        /// Elimina físicamente una licencia de la base de datos
        /// IMPORTANTE: Esto es una operación destructiva que no se puede deshacer
        /// </summary>
        public async Task<bool> DeleteLicenseAsync(int licenseId)
        {
            try
            {
                var license = await _context.Licenses.FindAsync(licenseId);

                if (license == null)
                {
                    _logger.LogWarning("DeleteLicense: License {LicenseId} not found", licenseId);
                    return false;
                }

                _logger.LogWarning("DeleteLicense: Deleting license {LicenseId}, Type={Type}, CustomerId={CustomerId}, Status={Status}",
                    licenseId, license.LicenseType, license.CustomerId, license.Status);

                // ELIMINAR FÍSICAMENTE de la base de datos
                _context.Licenses.Remove(license);
                await _context.SaveChangesAsync();

                _logger.LogInformation("DeleteLicense: License {LicenseId} deleted successfully from database", licenseId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting license {LicenseId}", licenseId);
                throw;
            }
        }

        /// <summary>
        /// Extiende la fecha de expiración de una licencia
        /// </summary>
        public async Task<bool> ExtendLicenseAsync(int licenseId, DateTime newExpirationDate)
        {
            try
            {
                var license = await _context.Licenses.FindAsync(licenseId);

                if (license == null)
                    return false;

                license.ExpiresAt = newExpirationDate;
                license.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Extended license {LicenseId} to {NewDate}",
                    licenseId, newExpirationDate);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending license {LicenseId}", licenseId);
                throw;
            }
        }

        /// <summary>
        /// Crea un customer de desarrollo para testing
        /// </summary>
        public async Task<Customer?> CreateDevelopmentCustomerAsync(int userId)
        {
            try
            {
                // Verificar si ya existe un customer para este usuario
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (existingCustomer != null)
                    return existingCustomer;

                // Crear usuario si no existe
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    user = new User
                    {
                        Id = userId,
                        Name = "Usuario Desarrollo",
                        Email = "dev@test.com",
                        PasswordHash = "dev_hash",
                        Role = "User",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                // Crear customer de desarrollo
                var customer = new Customer
                {
                    UserId = userId,
                    CompanyName = "Empresa de Desarrollo",
                    Plan = "Premium",
                    TrackerLimit = 100,
                    FarmLimit = 10,
                    Status = "Active",
                    SubscriptionStart = DateTime.UtcNow,
                    SubscriptionEnd = DateTime.UtcNow.AddYears(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Development customer created for userId {UserId}", userId);
                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating development customer for userId {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Actualiza una licencia existente
        /// </summary>
        public async Task<License> UpdateLicenseAsync(License license)
        {
            try
            {
                _context.Licenses.Update(license);
                await _context.SaveChangesAsync();
                return license;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating license {LicenseId}", license.Id);
                throw;
            }
        }
    }

    // DTOs para el servicio
    public class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }
        public License? License { get; set; }
        public Customer? Customer { get; set; }
    }

    public class LicenseDto
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public string LicenseType { get; set; } = string.Empty;
        public int MaxTrackers { get; set; }
        public int MaxFarms { get; set; }
        public int MaxUsers { get; set; }
        public string? Features { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsValid { get; set; }
        public int DaysUntilExpiration { get; set; }
    }
}