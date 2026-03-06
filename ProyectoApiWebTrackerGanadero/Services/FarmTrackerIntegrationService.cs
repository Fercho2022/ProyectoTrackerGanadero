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
                    .AsNoTracking()
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
        /// OPTIMIZADO: Queries secuenciales eficientes (EF no soporta paralelas en mismo DbContext)
        /// </summary>
        public async Task<(bool success, string message)> AssignTrackerToAnimalAsync(int customerTrackerId, int animalId, int userId)
        {
            try
            {
                _logger.LogInformation("[ASSIGN] CT={CustomerTrackerId}, Animal={AnimalId}, User={UserId}", customerTrackerId, animalId, userId);

                // Query 1: CustomerTracker (try by Id, fallback by TrackerId) + Animal + existingAssignment en una sola query
                var customerTracker = await _context.CustomerTrackers
                    .FirstOrDefaultAsync(ct => ct.Id == customerTrackerId || ct.TrackerId == customerTrackerId);

                if (customerTracker == null)
                {
                    return (false, $"No se encontró CustomerTracker con ID {customerTrackerId}. Asigne el tracker desde el Panel Admin primero.");
                }

                // Query 2: Validate Status
                if (customerTracker.Status != "Active")
                {
                    return (false, $"El CustomerTracker no está activo (Estado: {customerTracker.Status}).");
                }

                // Query 3: Check if already assigned to another animal
                var existingAssignment = await _context.Animals
                    .Where(a => a.CustomerTrackerId == customerTracker.Id && a.Id != animalId)
                    .Select(a => new { a.Id, a.Name })
                    .FirstOrDefaultAsync();

                if (existingAssignment != null)
                {
                    return (false, $"El tracker ya está asignado al animal {existingAssignment.Name} (ID: {existingAssignment.Id}).");
                }

                // Query 4: Load Animal with Farm
                var animal = await _context.Animals
                    .Include(a => a.Farm)
                    .FirstOrDefaultAsync(a => a.Id == animalId);

                if (animal == null)
                {
                    return (false, $"Animal con ID {animalId} no encontrado.");
                }
                if (animal.Farm == null)
                {
                    return (false, $"El animal no tiene una granja válida asociada.");
                }

                // --- Assignment Section ---

                // If the target animal already has a different tracker, unassign it first.
                if (animal.CustomerTrackerId.HasValue && animal.CustomerTrackerId.Value != customerTracker.Id)
                {
                    animal.TrackerId = null;
                    animal.CustomerTrackerId = null;
                }

                // Update the animal
                animal.TrackerId = customerTracker.TrackerId;
                animal.CustomerTrackerId = customerTracker.Id;
                animal.CustomerTracker = customerTracker;
                animal.UpdatedAt = DateTime.UtcNow;

                // Update the CustomerTracker
                customerTracker.UpdatedAt = DateTime.UtcNow;
                customerTracker.AssignedAnimal = animal;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Assigned CT {CustomerTrackerId} to animal {AnimalId}", customerTracker.Id, animalId);
                return (true, $"Tracker asignado exitosamente al animal {animal.Name}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in AssignTrackerToAnimalAsync CT={CustomerTrackerId} Animal={AnimalId}", customerTrackerId, animalId);
                throw;
            }
        }

        /// <summary>
        /// Asignación masiva rápida: vincula trackers libres con animales sin tracker, en orden numérico
        /// </summary>
        public async Task<BulkAssignResultDto> BulkAssignTrackersAsync(int farmId, int userId)
        {
            var result = new BulkAssignResultDto();

            try
            {
                // 1. Get user's customer
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active");

                if (customer == null)
                {
                    result.Message = "El usuario no tiene un Customer activo.";
                    return result;
                }

                // 2. Get all unassigned CustomerTrackers for this customer, ordered by DeviceId
                var assignedCustomerTrackerIds = await _context.Animals
                    .Where(a => a.CustomerTrackerId.HasValue)
                    .Select(a => a.CustomerTrackerId!.Value)
                    .ToListAsync();

                var freeTrackers = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    .Where(ct => ct.CustomerId == customer.Id &&
                                ct.Status == "Active" &&
                                !assignedCustomerTrackerIds.Contains(ct.Id))
                    .OrderBy(ct => ct.Tracker!.DeviceId)
                    .ToListAsync();

                // 3. Get all animals without tracker in the farm, ordered by Tag
                var animalsWithoutTracker = await _context.Animals
                    .Where(a => a.FarmId == farmId && !a.CustomerTrackerId.HasValue)
                    .OrderBy(a => a.Tag)
                    .ThenBy(a => a.Name)
                    .ToListAsync();

                result.TotalFreeTrackers = freeTrackers.Count;
                result.TotalAnimalsWithoutTracker = animalsWithoutTracker.Count;

                // 4. Assign in order: first free tracker to first animal without tracker
                var pairsToAssign = Math.Min(freeTrackers.Count, animalsWithoutTracker.Count);

                for (int i = 0; i < pairsToAssign; i++)
                {
                    var ct = freeTrackers[i];
                    var animal = animalsWithoutTracker[i];

                    animal.TrackerId = ct.TrackerId;
                    animal.CustomerTrackerId = ct.Id;
                    animal.CustomerTracker = ct;
                    animal.UpdatedAt = DateTime.UtcNow;

                    ct.UpdatedAt = DateTime.UtcNow;
                    ct.AssignedAnimal = animal;

                    result.Assignments.Add(new BulkAssignItemDto
                    {
                        AnimalId = animal.Id,
                        AnimalName = animal.Name ?? "Sin nombre",
                        AnimalTag = animal.Tag,
                        CustomerTrackerId = ct.Id,
                        DeviceId = ct.Tracker?.DeviceId ?? "Unknown"
                    });
                }

                if (pairsToAssign > 0)
                {
                    await _context.SaveChangesAsync();
                }

                result.Success = true;
                result.AssignedCount = pairsToAssign;
                result.Message = pairsToAssign > 0
                    ? $"Se asignaron {pairsToAssign} trackers exitosamente."
                    : "No hay pares disponibles para asignar.";

                _logger.LogInformation("Bulk assign: {Count} trackers assigned in farm {FarmId}", pairsToAssign, farmId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BulkAssignTrackersAsync for farm {FarmId}", farmId);
                result.Message = $"Error: {ex.Message}";
                return result;
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
                var trackerInfo = await _context.Animals
                    .AsNoTracking()
                    .Where(a => a.Id == animalId && a.Farm.UserId == userId)
                    .Select(a => a.CustomerTracker != null && a.CustomerTracker.Tracker != null
                        ? new AnimalTrackerInfoDto
                        {
                            AnimalId = a.Id,
                            CustomerTrackerId = a.CustomerTrackerId!.Value,
                            TrackerId = a.CustomerTracker.Tracker.Id,
                            DeviceId = a.CustomerTracker.Tracker.DeviceId,
                            TrackerName = a.CustomerTracker.Tracker.Name ?? a.CustomerTracker.Tracker.DeviceId,
                            Model = a.CustomerTracker.Tracker.Model,
                            BatteryLevel = a.CustomerTracker.Tracker.BatteryLevel,
                            LastSeen = a.CustomerTracker.Tracker.LastSeen,
                            IsOnline = a.CustomerTracker.Tracker.LastSeen > DateTime.UtcNow.AddMinutes(-5),
                            AssignedAt = a.CustomerTracker.AssignedAt
                        }
                        : null)
                    .FirstOrDefaultAsync();

                return trackerInfo;
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
        public async Task<List<AnimalWithTrackerDto>> GetFarmAnimalsWithTrackersAsync(int farmId, int userId)
        {
            try
            {
                // Verificar que la granja pertenece al usuario
                var farm = await _context.Farms
                    .FirstOrDefaultAsync(f => f.Id == farmId && f.UserId == userId);

                if (farm == null)
                {
                    _logger.LogWarning("Farm {FarmId} not found or does not belong to user {UserId}", farmId, userId);
                    return new List<AnimalWithTrackerDto>();
                }

                // Una sola query que trae todos los animales con sus trackers
                var animalsWithTrackers = await _context.Animals
                    .AsNoTracking()
                    .Where(a => a.FarmId == farmId)
                    .OrderBy(a => a.Name)
                    .Select(a => new AnimalWithTrackerDto
                    {
                        AnimalId = a.Id,
                        AnimalName = a.Name,
                        AnimalTag = a.Tag,
                        TrackerInfo = a.CustomerTracker != null && a.CustomerTracker.Tracker != null
                            ? new AnimalTrackerInfoDto
                            {
                                AnimalId = a.Id,
                                CustomerTrackerId = a.CustomerTrackerId!.Value,
                                TrackerId = a.CustomerTracker.Tracker.Id,
                                DeviceId = a.CustomerTracker.Tracker.DeviceId,
                                TrackerName = a.CustomerTracker.Tracker.Name ?? a.CustomerTracker.Tracker.DeviceId,
                                Model = a.CustomerTracker.Tracker.Model,
                                BatteryLevel = a.CustomerTracker.Tracker.BatteryLevel,
                                LastSeen = a.CustomerTracker.Tracker.LastSeen,
                                IsOnline = a.CustomerTracker.Tracker.LastSeen > DateTime.UtcNow.AddMinutes(-5),
                                AssignedAt = a.CustomerTracker.AssignedAt
                            }
                            : null
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} animals with tracker info for farm {FarmId} in single query",
                    animalsWithTrackers.Count, farmId);

                return animalsWithTrackers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animals with trackers for farm {FarmId}", farmId);
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

                    // Eliminar LocationHistories asociadas al tracker
                    var deletedLocationHistories = await _context.LocationHistories
                        .Where(lh => lh.TrackerId == trackerId)
                        .ExecuteDeleteAsync();

                    _logger.LogInformation("Removed {Count} LocationHistory records for tracker {TrackerId}",
                        deletedLocationHistories, trackerId);

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

                    // Eliminar todas las LocationHistories
                    await _context.LocationHistories.ExecuteDeleteAsync();

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