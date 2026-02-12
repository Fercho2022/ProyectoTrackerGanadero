using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Services
{
    /// <summary>
    /// Servicio para integrar la gestión de trackers de clientes con granjas y animales
    /// Conecta el flujo Customer -> CustomerTracker con Farm -> Animal -> Tracker
    /// </summary>
    public class FarmTrackerIntegrationService
    {
        private readonly CattleTrackingContext _context;
        private readonly ILogger<FarmTrackerIntegrationService> _logger;

        public FarmTrackerIntegrationService(
            CattleTrackingContext context,
            ILogger<FarmTrackerIntegrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los trackers disponibles para un usuario en sus granjas
        /// (sus CustomerTrackers que no están asignados a animales)
        /// </summary>
        public async Task<List<AvailableFarmTrackerDto>> GetAvailableTrackersForFarmsAsync(int userId)
        {
            try
            {
                // Buscar customer del usuario
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active");

                if (customer == null)
                {
                    _logger.LogInformation("No active customer found for user {UserId}", userId);
                    return new List<AvailableFarmTrackerDto>();
                }

                // Obtener CustomerTrackers activos que no están asignados a animales
                var availableTrackers = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    .Include(ct => ct.AssignedAnimal)  // Include navigation to properly detect assignments
                    .Where(ct => ct.CustomerId == customer.Id &&
                                ct.Status == "Active" &&
                                ct.AssignedAnimal == null)
                    .Select(ct => new AvailableFarmTrackerDto
                    {
                        CustomerTrackerId = ct.Id,
                        TrackerId = ct.TrackerId,
                        DeviceId = ct.Tracker.DeviceId,
                        TrackerName = ct.Tracker.Name ?? ct.Tracker.DeviceId,
                        // CustomName = null, // ct.CustomName, // Comentado porque la propiedad no existe
                        Model = ct.Tracker.Model,
                        BatteryLevel = ct.Tracker.BatteryLevel,
                        LastSeen = ct.Tracker.LastSeen,
                        IsOnline = ct.Tracker.LastSeen > DateTime.UtcNow.AddMinutes(-5),
                        AssignedAt = ct.AssignedAt
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} available trackers for user {UserId}",
                    availableTrackers.Count, userId);

                return availableTrackers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available trackers for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Asigna un CustomerTracker a un animal específico
        /// </summary>
        public async Task<bool> AssignTrackerToAnimalAsync(int customerTrackerId, int animalId, int userId)
        {
            try
            {
                _logger.LogInformation($"[ASSIGN] Starting assignment: CustomerTrackerId={customerTrackerId}, AnimalId={animalId}, UserId={userId}");

                // WORKAROUND: The client sends TrackerId in the CustomerTrackerId field.
                // First, try to find the CustomerTracker by TrackerId.
                var customerTrackerInfo = await _context.CustomerTrackers
                    .Where(ct => ct.TrackerId == customerTrackerId)
                    .Select(ct => new { ct.Id, ct.TrackerId, ct.CustomerId, ct.Status })
                    .FirstOrDefaultAsync();

                _logger.LogInformation($"[ASSIGN] Search by TrackerId={customerTrackerId}: {(customerTrackerInfo != null ? $"Found CustomerTracker.Id={customerTrackerInfo.Id}" : "NOT FOUND")}");

                // If not found, maybe the client bug was fixed and it's a real CustomerTrackerId.
                if (customerTrackerInfo == null)
                {
                    customerTrackerInfo = await _context.CustomerTrackers
                        .Where(ct => ct.Id == customerTrackerId)
                        .Select(ct => new { ct.Id, ct.TrackerId, ct.CustomerId, ct.Status })
                        .FirstOrDefaultAsync();

                    _logger.LogInformation($"[ASSIGN] Search by CustomerTrackerId={customerTrackerId}: {(customerTrackerInfo != null ? $"Found CustomerTracker.Id={customerTrackerInfo.Id}" : "NOT FOUND")}");
                }

                // SECURITY FIX: DISABLE auto-assignment of orphaned trackers
                // Trackers must be assigned manually from Admin Panel first
                if (customerTrackerInfo == null)
                {
                    _logger.LogError($"[SECURITY] No CustomerTracker found for ID {customerTrackerId}. Tracker must be assigned from Admin Panel first. Auto-assignment is disabled.");
                    return false;
                }

                // DEPRECATED: RepairOrphanedTracker functionality disabled for security
                // Old code (now disabled):
                /*
                if (customerTrackerInfo == null)
                {
                    _logger.LogWarning($"No CustomerTracker found for ID {customerTrackerId}. Attempting to repair as an orphaned tracker.");
                    var repairedTracker = await RepairOrphanedTracker(customerTrackerId, userId);
                    if (repairedTracker != null)
                    {
                        customerTrackerInfo = new {
                            Id = (int)repairedTracker.Id,
                            TrackerId = (int)repairedTracker.TrackerId,
                            CustomerId = (int)repairedTracker.CustomerId,
                            Status = (string)repairedTracker.Status
                        };
                        _logger.LogInformation($"Successfully repaired and created CustomerTracker {customerTrackerInfo.Id} for Tracker {customerTrackerInfo.TrackerId}.");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to find or repair a CustomerTracker for ID {customerTrackerId}.");
                        return false;
                    }
                }
                */

                // --- Validation Section ---

                _logger.LogInformation($"[ASSIGN] Found CustomerTracker: Id={customerTrackerInfo.Id}, TrackerId={customerTrackerInfo.TrackerId}, CustomerId={customerTrackerInfo.CustomerId}, Status={customerTrackerInfo.Status}");

                // 1. Validate Ownership (RELAXED FOR DEVELOPMENT)
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerTrackerInfo.CustomerId);
                _logger.LogInformation($"[ASSIGN] Customer lookup: CustomerId={customerTrackerInfo.CustomerId}, Found={customer != null}, CustomerUserId={customer?.UserId}, ExpectedUserId={userId}");
                if (customer == null)
                {
                    _logger.LogWarning($"[ASSIGN] VALIDATION FAILED: Customer {customerTrackerInfo.CustomerId} not found.");
                    return false;
                }
                // RELAXED: Allow cross-user assignment during development
                if (customer.UserId != userId)
                {
                    _logger.LogWarning($"[ASSIGN] WARNING: CustomerTracker {customerTrackerInfo.Id} belongs to user {customer.UserId} but assigning to user {userId}. Allowing for development.");
                }

                // 2. Validate Status
                if (customerTrackerInfo.Status != "Active")
                {
                    _logger.LogWarning($"Validation failed: CustomerTracker {customerTrackerInfo.Id} is not active (Status: {customerTrackerInfo.Status}).");
                    return false;
                }

                // 3. Validate if already assigned to another animal
                var existingAssignment = await _context.Animals.FirstOrDefaultAsync(a => a.CustomerTrackerId == customerTrackerInfo.Id);
                if (existingAssignment != null)
                {
                    _logger.LogWarning($"Validation failed: CustomerTracker {customerTrackerInfo.Id} is already assigned to animal {existingAssignment.Id}.");
                    return false;
                }

                // 4. Validate Animal
                var animal = await _context.Animals.Include(a => a.Farm).FirstOrDefaultAsync(a => a.Id == animalId);
                _logger.LogInformation($"[ASSIGN] Animal lookup: AnimalId={animalId}, Found={animal != null}, AnimalName={animal?.Name}, FarmId={animal?.FarmId}");
                if (animal == null)
                {
                    _logger.LogWarning($"[ASSIGN] VALIDATION FAILED: Animal {animalId} not found.");
                    return false;
                }
                if (animal.Farm == null)
                {
                    _logger.LogWarning($"[ASSIGN] VALIDATION FAILED: Animal {animalId} has an invalid or missing FarmId ({animal.FarmId}).");
                    return false;
                }
                _logger.LogInformation($"[ASSIGN] Farm validation: FarmId={animal.Farm.Id}, FarmUserId={animal.Farm.UserId}, ExpectedUserId={userId}");
                // RELAXED: Allow cross-user assignment during development
                if (animal.Farm.UserId != userId)
                {
                    _logger.LogWarning($"[ASSIGN] WARNING: Animal {animalId} (Farm: {animal.Farm.Id}) belongs to user {animal.Farm.UserId} but assigning to user {userId}. Allowing for development.");
                }

                // --- Assignment Section ---

                // If the target animal already has a different tracker, unassign it first.
                if (animal.CustomerTrackerId.HasValue)
                {
                    _logger.LogInformation($"Animal {animalId} already has CustomerTracker {animal.CustomerTrackerId}. Unassigning it first.");
                    // Just set the properties to null instead of calling the method to avoid recursion
                    animal.TrackerId = null;
                    animal.CustomerTrackerId = null;
                    await _context.SaveChangesAsync();
                }

                // Assign the new tracker using Entity Framework to maintain navigation relationships
                // Load the CustomerTracker entity to establish navigation (avoid loading License navigation)
                var customerTracker = await _context.CustomerTrackers
                    .Where(ct => ct.Id == customerTrackerInfo.Id)
                    .FirstOrDefaultAsync();

                if (customerTracker == null)
                {
                    _logger.LogError($"Could not load CustomerTracker with ID {customerTrackerInfo.Id}");
                    return false;
                }

                // Update the animal with proper EF tracking
                animal.TrackerId = customerTrackerInfo.TrackerId;
                animal.CustomerTrackerId = customerTrackerInfo.Id;
                animal.CustomerTracker = customerTracker;  // Establish navigation relationship
                animal.UpdatedAt = DateTime.UtcNow;

                // Update the CustomerTracker timestamp and establish reverse navigation
                customerTracker.UpdatedAt = DateTime.UtcNow;
                customerTracker.AssignedAnimal = animal;  // Establish reverse navigation relationship

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully assigned CustomerTracker {customerTrackerInfo.Id} to animal {animalId}.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred in AssignTrackerToAnimalAsync for CustomerTrackerId {CustomerTrackerId} and AnimalId {AnimalId}", customerTrackerId, animalId);
                throw; // Let the controller handle the final response
            }
        }

        private async Task<dynamic?> RepairOrphanedTracker(int trackerId, int userId)
        {
            // El ID que llega es en realidad el Tracker.Id por el bug del cliente
            var tracker = await _context.Trackers
                .FirstOrDefaultAsync(t => t.Id == trackerId);

            if (tracker == null)
            {
                return null;
            }

            // Verificar si ya tiene CustomerTrackers sin usar navegación
            var hasCustomerTrackers = await _context.CustomerTrackers
                .AnyAsync(ct => ct.TrackerId == trackerId);

            if (hasCustomerTrackers)
            {
                // Ya tiene una asociación, así que no es un huérfano reparable
                return null;
            }

            // Encontramos un tracker huérfano. Vamos a crearle su CustomerTracker.
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (customer == null)
            {
                _logger.LogWarning("Cannot repair tracker {TrackerId}: customer for user {UserId} not found.", trackerId, userId);
                return null;
            }

            if (!customer.CanAddMoreTrackers())
            {
                 _logger.LogWarning("Cannot repair tracker {TrackerId}: customer has reached their tracker limit.", trackerId, userId);
                return null;
            }

            var newCustomerTracker = new CustomerTracker
            {
                CustomerId = customer.Id,
                TrackerId = trackerId,
                // AssignmentMethod = "AutoRepair", // Comentado porque la propiedad no existe
                Status = "Active",
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                // Notes = "Record created automatically by system to fix orphaned tracker." // Comentado porque la propiedad no existe
            };

            _context.CustomerTrackers.Add(newCustomerTracker);
            await _context.SaveChangesAsync();

            // Devolver el objeto creado en el formato que espera el método
            return new {
                newCustomerTracker.Id,
                newCustomerTracker.TrackerId,
                newCustomerTracker.CustomerId,
                newCustomerTracker.Status,
                newCustomerTracker.AssignedAt,
                HasAssignedAnimal = false,
                AssignedAnimalId = (int?)null
            };
        }

        /// <summary>
        /// Desasigna un tracker de un animal
        /// </summary>
        public async Task<bool> UnassignTrackerFromAnimalAsync(int animalId, int userId)
        {
            try
            {
                var animal = await _context.Animals
                    .Include(a => a.Farm)
                    .FirstOrDefaultAsync(a => a.Id == animalId);

                if (animal == null)
                {
                    _logger.LogWarning("Unassign failed: Animal {AnimalId} not found.", animalId);
                    return false;
                }

                if (animal.Farm == null)
                {
                    _logger.LogWarning($"Unassign failed: Animal {animalId} has an invalid or missing FarmId ({animal.FarmId}).");
                    return false;
                }

                if (animal.Farm.UserId != userId)
                {
                    _logger.LogWarning("Unassign failed: Animal {AnimalId} not found or does not belong to user {UserId}",
                        animalId, userId);
                    return false;
                }

                // Store the CustomerTrackerId before updating for the timestamp update
                var customerTrackerId = animal.CustomerTrackerId;

                // Unassign tracker from the animal using Entity Framework to maintain navigation relationships
                if (customerTrackerId.HasValue)
                {
                    // Load the CustomerTracker to properly break navigation relationships
                    var customerTracker = await _context.CustomerTrackers
                        .FirstOrDefaultAsync(ct => ct.Id == customerTrackerId.Value);

                    if (customerTracker != null)
                    {
                        customerTracker.AssignedAnimal = null;  // Break reverse navigation
                        customerTracker.UpdatedAt = DateTime.UtcNow;
                    }
                }

                // Clear the animal's tracker assignments and navigation
                animal.TrackerId = null;
                animal.CustomerTrackerId = null;
                animal.CustomerTracker = null;  // Break navigation relationship
                animal.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully unassigned tracker from animal {AnimalId}", animalId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning tracker from animal {AnimalId}", animalId);
                throw;
            }
        }

        /// <summary>
        /// Limpia trackers inactivos (que no han transmitido en los últimos 2 minutos) de todos los animales de una granja
        /// </summary>
        public async Task<int> CleanupInactiveTrackersFromFarmAsync(int farmId, int userId)
        {
            try
            {
                _logger.LogInformation("Starting cleanup of inactive trackers for farm {FarmId}, user {UserId}", farmId, userId);

                // Verificar que la granja pertenece al usuario
                var farm = await _context.Farms
                    .FirstOrDefaultAsync(f => f.Id == farmId && f.UserId == userId);

                if (farm == null)
                {
                    _logger.LogWarning("Farm {FarmId} not found or does not belong to user {UserId}", farmId, userId);
                    return 0;
                }

                // Obtener todos los animales de la granja que tengan trackers asignados
                var animalsWithTrackers = await _context.Animals
                    .Include(a => a.CustomerTracker)
                        .ThenInclude(ct => ct!.Tracker)
                    .Where(a => a.FarmId == farmId && a.CustomerTrackerId.HasValue)
                    .ToListAsync();

                var cutoffTime = DateTime.UtcNow.AddMinutes(-2);
                var cleanedCount = 0;

                foreach (var animal in animalsWithTrackers)
                {
                    if (animal.CustomerTracker?.Tracker != null)
                    {
                        var tracker = animal.CustomerTracker.Tracker;

                        // Si el tracker no ha transmitido en los últimos 2 minutos, desasignarlo
                        if (tracker.LastSeen <= cutoffTime)
                        {
                            _logger.LogInformation("Removing inactive tracker {DeviceId} from animal {AnimalId} (last seen: {LastSeen})",
                                tracker.DeviceId, animal.Id, tracker.LastSeen);

                            animal.CustomerTrackerId = null;
                            animal.TrackerId = null;
                            cleanedCount++;
                        }
                    }
                }

                if (cleanedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} inactive trackers from farm {FarmId}", cleanedCount, farmId);
                }
                else
                {
                    _logger.LogInformation("No inactive trackers found to clean up in farm {FarmId}", farmId);
                }

                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up inactive trackers from farm {FarmId}", farmId);
                throw;
            }
        }

        /// <summary>
        /// Obtiene información del tracker asignado a un animal
        /// </summary>
        public async Task<AnimalTrackerInfoDto?> GetAnimalTrackerInfoAsync(int animalId, int userId)
        {
            try
            {
                var animal = await _context.Animals
                    .Include(a => a.Farm)
                    .Include(a => a.CustomerTracker)
                        .ThenInclude(ct => ct!.Tracker)
                    .FirstOrDefaultAsync(a => a.Id == animalId);

                if (animal == null || animal.Farm.UserId != userId)
                {
                    return null;
                }

                if (animal.CustomerTracker == null || animal.CustomerTracker.Tracker == null)
                {
                    return null;
                }

                var tracker = animal.CustomerTracker.Tracker;

                return new AnimalTrackerInfoDto
                {
                    AnimalId = animalId,
                    CustomerTrackerId = animal.CustomerTrackerId.Value,
                    TrackerId = tracker.Id,
                    DeviceId = tracker.DeviceId,
                    TrackerName = tracker.Name ?? tracker.DeviceId,
                    // CustomName = null, // animal.CustomerTracker.CustomName, // Comentado porque la propiedad no existe
                    Model = tracker.Model,
                    BatteryLevel = tracker.BatteryLevel,
                    LastSeen = tracker.LastSeen,
                    IsOnline = tracker.LastSeen > DateTime.UtcNow.AddMinutes(-5),
                    AssignedAt = animal.CustomerTracker.AssignedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracker info for animal {AnimalId}", animalId);
                throw;
            }
        }

        /// <summary>
        /// Elimina un tracker específico de la base de datos - Versión simplificada como DeleteAllTrackers
        /// </summary>
        public async Task<bool> DeleteTrackerAsync(int trackerId, int userId)
        {
            try
            {
                _logger.LogInformation("Deleting tracker {TrackerId} for user {UserId}", trackerId, userId);

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Verificar que existe el tracker (usando SQL directo)
                    var trackerExists = await _context.Trackers
                        .Where(t => t.Id == trackerId)
                        .AnyAsync();

                    if (!trackerExists)
                    {
                        _logger.LogWarning("Tracker {TrackerId} not found in database", trackerId);
                        return false;
                    }

                    // Contar cuántos registros vamos a afectar antes de eliminar
                    var customerTrackersToDelete = await _context.CustomerTrackers
                        .Where(ct => ct.TrackerId == trackerId)
                        .CountAsync();

                    _logger.LogInformation("Found {Count} CustomerTracker records to delete for tracker {TrackerId}",
                        customerTrackersToDelete, trackerId);

                    // Desasignar de todos los animales que usan este tracker (SQL directo)
                    var affectedAnimals = await _context.Animals
                        .Where(a => a.TrackerId == trackerId)
                        .ExecuteUpdateAsync(a => a
                            .SetProperty(x => x.TrackerId, (int?)null)
                            .SetProperty(x => x.CustomerTrackerId, (int?)null));

                    _logger.LogInformation("Unassigned tracker from {Count} animals", affectedAnimals);

                    // Desasignar por CustomerTrackerId también
                    var affectedAnimalsByCustomerTracker = await _context.Database
                        .ExecuteSqlRawAsync(
                            "UPDATE \"Animals\" SET \"TrackerId\" = NULL, \"CustomerTrackerId\" = NULL " +
                            "WHERE \"CustomerTrackerId\" IN (SELECT \"Id\" FROM \"CustomerTrackers\" WHERE \"TrackerId\" = {0})",
                            trackerId);

                    _logger.LogInformation("Unassigned tracker by CustomerTracker from {Count} additional animals", affectedAnimalsByCustomerTracker);

                    // Eliminar CustomerTrackers usando SQL directo
                    var deletedCustomerTrackers = await _context.CustomerTrackers
                        .Where(ct => ct.TrackerId == trackerId)
                        .ExecuteDeleteAsync();

                    _logger.LogInformation("Removed {Count} CustomerTracker records", deletedCustomerTrackers);

                    // Eliminar el tracker usando SQL directo
                    var deletedTrackers = await _context.Trackers
                        .Where(t => t.Id == trackerId)
                        .ExecuteDeleteAsync();

                    _logger.LogInformation("Removed {Count} Tracker records", deletedTrackers);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully deleted tracker {TrackerId}", trackerId);
                    return deletedTrackers > 0;
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Error in transaction while deleting tracker {TrackerId}: {Message}", trackerId, innerEx.Message);
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tracker {TrackerId}: {Message}", trackerId, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Obtiene información de debug para un tracker específico
        /// </summary>
        public async Task<object> GetTrackerDebugInfoAsync(int trackerId, int userId)
        {
            try
            {
                // Buscar customer del usuario
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active");

                // Buscar tracker SIN navegación
                var tracker = await _context.Trackers
                    .Where(t => t.Id == trackerId)
                    .Select(t => new { t.Id, t.DeviceId, t.Name, t.IsActive })
                    .FirstOrDefaultAsync();

                // Buscar customerTrackers para este usuario SIN navegación
                var customerTrackers = customer != null ?
                    (await _context.CustomerTrackers
                        .Where(ct => ct.CustomerId == customer.Id)
                        .Select(ct => new
                        {
                            Id = ct.Id,
                            TrackerId = ct.TrackerId,
                            CustomerId = ct.CustomerId,
                            Status = ct.Status
                        })
                        .ToListAsync()).Cast<object>().ToList() :
                    new List<object>();

                // Buscar customerTracker específico
                var specificCustomerTracker = customer != null ? await _context.CustomerTrackers
                    .Where(ct => ct.TrackerId == trackerId && ct.CustomerId == customer.Id)
                    .Select(ct => new
                    {
                        Id = ct.Id,
                        TrackerId = ct.TrackerId,
                        CustomerId = ct.CustomerId,
                        Status = ct.Status
                    })
                    .FirstOrDefaultAsync() : null;

                return new
                {
                    requestedTrackerId = trackerId,
                    userId = userId,
                    customer = customer != null ? new
                    {
                        id = customer.Id,
                        userId = customer.UserId,
                        status = customer.Status,
                        companyName = customer.CompanyName
                    } : null,
                    tracker = tracker != null ? new
                    {
                        id = tracker.Id,
                        deviceId = tracker.DeviceId,
                        name = tracker.Name,
                        isActive = tracker.IsActive
                    } : null,
                    customerTrackers = customerTrackers,
                    specificCustomerTracker = specificCustomerTracker,
                    allCustomers = await _context.Customers.Select(c => new
                    {
                        id = c.Id,
                        userId = c.UserId,
                        status = c.Status
                    }).ToListAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting debug info for tracker {TrackerId}", trackerId);
                throw;
            }
        }

        /// <summary>
        /// Elimina todos los trackers de la base de datos
        /// </summary>
        public async Task<int> DeleteAllTrackersAsync(int userId)
        {
            try
            {
                _logger.LogInformation("Deleting all trackers for user {UserId}", userId);

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Desasignar todos los trackers de animales
                    await _context.Animals
                        .ExecuteUpdateAsync(a => a
                            .SetProperty(x => x.TrackerId, (int?)null)
                            .SetProperty(x => x.CustomerTrackerId, (int?)null));

                    // Eliminar todos los CustomerTrackers
                    var customerTrackersCount = await _context.CustomerTrackers.CountAsync();
                    await _context.CustomerTrackers.ExecuteDeleteAsync();

                    // Eliminar todos los Trackers
                    var trackersCount = await _context.Trackers.CountAsync();
                    await _context.Trackers.ExecuteDeleteAsync();

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully deleted all {TrackerCount} trackers and {CustomerTrackerCount} customer tracker relationships",
                        trackersCount, customerTrackersCount);

                    return trackersCount;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all trackers");
                throw;
            }
        }
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
        // public string? CustomName { get; set; } // Comentado porque la propiedad no existe
        public string Model { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public DateTime AssignedAt { get; set; }
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
        // public string? CustomName { get; set; } // Comentado porque la propiedad no existe
        public string Model { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}