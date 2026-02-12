using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Roles = "Admin")] // TODO: Descomentar cuando se implemente autenticación de admin
    public class AdminController : ControllerBase
    {
        private readonly CattleTrackingContext _context;
        private readonly TrackerDiscoveryService _trackerDiscoveryService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            CattleTrackingContext context,
            TrackerDiscoveryService trackerDiscoveryService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _trackerDiscoveryService = trackerDiscoveryService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los customers del sistema (ADMIN ONLY)
        /// </summary>
        [HttpGet("customers")]
        public async Task<IActionResult> GetAllCustomers()
        {
            try
            {
                _logger.LogInformation("[Admin.GetAllCustomers] Getting all customers");

                var customers = await _context.Customers
                    .Include(c => c.User)
                    .Include(c => c.CustomerTrackers.Where(ct => ct.Status == "Active"))
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new
                    {
                        id = c.Id,
                        userId = c.UserId,
                        userName = c.User != null ? c.User.Name : "N/A",
                        userEmail = c.User != null ? c.User.Email : "N/A",
                        companyName = c.CompanyName,
                        plan = c.Plan,
                        trackerLimit = c.TrackerLimit,
                        farmLimit = c.FarmLimit,
                        status = c.Status,
                        subscriptionStart = c.SubscriptionStart,
                        subscriptionEnd = c.SubscriptionEnd,
                        // contactEmail = c.ContactEmail, // Comentado porque la propiedad no existe en Customer
                        // contactPhone = c.ContactPhone, // Comentado porque la propiedad no existe en Customer
                        activeTrackerCount = c.CustomerTrackers.Count(ct => ct.Status == "Active"),
                        createdAt = c.CreatedAt
                    })
                    .ToListAsync();

                _logger.LogInformation($"[Admin.GetAllCustomers] Found {customers.Count} customers");

                return Ok(new
                {
                    success = true,
                    customers = customers,
                    count = customers.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.GetAllCustomers] Error getting customers");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo clientes",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene los trackers asignados a un customer específico (ADMIN ONLY)
        /// </summary>
        [HttpGet("customers/{customerId}/trackers")]
        public async Task<IActionResult> GetCustomerTrackers(int customerId)
        {
            try
            {
                _logger.LogInformation($"[Admin.GetCustomerTrackers] Getting trackers for customer {customerId}");

                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Customer {customerId} no encontrado"
                    });
                }

                var trackers = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    .Include(ct => ct.Customer)
                    .Include(ct => ct.AssignedAnimal)
                        .ThenInclude(a => a!.Farm)
                    .Where(ct => ct.CustomerId == customerId && ct.Status == "Active")
                    .OrderByDescending(ct => ct.AssignedAt)
                    .Select(ct => new
                    {
                        id = ct.Id,
                        trackerId = ct.TrackerId,
                        customerId = ct.CustomerId,
                        deviceId = ct.Tracker.DeviceId,
                        trackerName = ct.Tracker.Name,
                        model = ct.Tracker.Model,
                        batteryLevel = ct.Tracker.BatteryLevel,
                        lastSeen = ct.Tracker.LastSeen,
                        isOnline = ct.Tracker.LastSeen > DateTime.UtcNow.AddMinutes(-30),
                        assignedAt = ct.AssignedAt,
                        status = ct.Status,
                        farmName = ct.AssignedAnimal != null && ct.AssignedAnimal.Farm != null
                            ? ct.AssignedAnimal.Farm.Name
                            : null,
                        animalName = ct.AssignedAnimal != null ? ct.AssignedAnimal.Name : null,
                        customerName = ct.Customer.CompanyName
                    })
                    .ToListAsync();

                _logger.LogInformation($"[Admin.GetCustomerTrackers] Found {trackers.Count} trackers for customer {customerId}");

                return Ok(new
                {
                    success = true,
                    trackers = trackers,
                    count = trackers.Count,
                    customerName = customer.CompanyName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Admin.GetCustomerTrackers] Error getting trackers for customer {customerId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo trackers del cliente"
                });
            }
        }

        /// <summary>
        /// Asigna un tracker a un customer (ADMIN ONLY)
        /// </summary>
        [HttpPost("assign-tracker")]
        public async Task<IActionResult> AssignTrackerToCustomer([FromBody] AdminAssignRequest request)
        {
            try
            {
                _logger.LogInformation($"[Admin.AssignTracker] Assigning tracker {request.TrackerId} to customer {request.CustomerId}");

                // Validar que el tracker existe
                var tracker = await _context.Trackers.FindAsync(request.TrackerId);
                if (tracker == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Tracker no encontrado"
                    });
                }

                // Validar que el customer existe
                var customer = await _context.Customers
                    .Include(c => c.CustomerTrackers.Where(ct => ct.Status == "Active"))
                    .FirstOrDefaultAsync(c => c.Id == request.CustomerId);

                if (customer == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Customer no encontrado"
                    });
                }

                // Validar límite de trackers
                var currentTrackerCount = customer.CustomerTrackers.Count(ct => ct.Status == "Active");
                if (currentTrackerCount >= customer.TrackerLimit)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"El cliente ha alcanzado el límite de {customer.TrackerLimit} trackers"
                    });
                }

                // Verificar si el tracker ya está asignado
                var existingAssignment = await _context.CustomerTrackers
                    .FirstOrDefaultAsync(ct => ct.TrackerId == request.TrackerId && ct.Status == "Active");

                if (existingAssignment != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Este tracker ya está asignado a otro cliente"
                    });
                }

                // Crear la asignación
                var customerTracker = new CustomerTracker
                {
                    CustomerId = request.CustomerId,
                    TrackerId = request.TrackerId,
                    Status = "Active",
                    AssignedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.CustomerTrackers.Add(customerTracker);

                // Marcar tracker como no disponible y activarlo
                tracker.IsAvailableForAssignment = false;
                tracker.Status = "Active"; // CRITICAL FIX: Cambiar estado para que ProcessTrackerDataAsync procese LocationHistory

                await _context.SaveChangesAsync();

                _logger.LogInformation($"[Admin.AssignTracker] Successfully assigned tracker {tracker.DeviceId} to customer {customer.CompanyName}");

                return Ok(new
                {
                    success = true,
                    message = $"Tracker {tracker.DeviceId} asignado exitosamente a {customer.CompanyName}",
                    customerTrackerId = customerTracker.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.AssignTracker] Error assigning tracker");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error asignando tracker",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Desasigna un tracker de un customer (ADMIN ONLY)
        /// </summary>
        [HttpPost("unassign-tracker/{customerTrackerId}")]
        public async Task<IActionResult> UnassignTracker(int customerTrackerId)
        {
            try
            {
                _logger.LogInformation($"[Admin.UnassignTracker] Unassigning customerTracker {customerTrackerId}");

                var customerTracker = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    .Include(ct => ct.Customer)
                    .FirstOrDefaultAsync(ct => ct.Id == customerTrackerId);

                if (customerTracker == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Asignación no encontrada"
                    });
                }

                // Desasignar
                customerTracker.Status = "Inactive";
                customerTracker.UnassignedAt = DateTime.UtcNow;
                customerTracker.UpdatedAt = DateTime.UtcNow;

                // Marcar tracker como disponible nuevamente
                if (customerTracker.Tracker != null)
                {
                    customerTracker.Tracker.IsAvailableForAssignment = true;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"[Admin.UnassignTracker] Successfully unassigned tracker {customerTracker.Tracker?.DeviceId}");

                return Ok(new
                {
                    success = true,
                    message = $"Tracker {customerTracker.Tracker?.DeviceId} desasignado exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.UnassignTracker] Error unassigning tracker");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error desasignando tracker"
                });
            }
        }

        /// <summary>
        /// Obtiene todas las asignaciones activas del sistema (ADMIN ONLY)
        /// </summary>
        [HttpGet("all-assignments")]
        public async Task<IActionResult> GetAllAssignments()
        {
            try
            {
                _logger.LogInformation("[Admin.GetAllAssignments] Getting all assignments");

                var assignments = await _context.CustomerTrackers
                    .Include(ct => ct.Tracker)
                    .Include(ct => ct.Customer)
                    .Include(ct => ct.AssignedAnimal)
                        .ThenInclude(a => a!.Farm)
                    .Where(ct => ct.Status == "Active")
                    .OrderByDescending(ct => ct.AssignedAt)
                    .Select(ct => new
                    {
                        id = ct.Id,
                        trackerId = ct.TrackerId,
                        customerId = ct.CustomerId,
                        deviceId = ct.Tracker.DeviceId,
                        trackerName = ct.Tracker.Name,
                        model = ct.Tracker.Model,
                        batteryLevel = ct.Tracker.BatteryLevel,
                        lastSeen = ct.Tracker.LastSeen,
                        isOnline = ct.Tracker.LastSeen > DateTime.UtcNow.AddMinutes(-30),
                        assignedAt = ct.AssignedAt,
                        status = ct.Status,
                        farmName = ct.AssignedAnimal != null && ct.AssignedAnimal.Farm != null
                            ? ct.AssignedAnimal.Farm.Name
                            : null,
                        animalName = ct.AssignedAnimal != null ? ct.AssignedAnimal.Name : null,
                        customerName = ct.Customer.CompanyName,
                        customerPlan = ct.Customer.Plan
                    })
                    .ToListAsync();

                _logger.LogInformation($"[Admin.GetAllAssignments] Found {assignments.Count} active assignments");

                return Ok(new
                {
                    success = true,
                    assignments = assignments,
                    count = assignments.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.GetAllAssignments] Error getting assignments");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo asignaciones"
                });
            }
        }

        /// <summary>
        /// Obtiene estadísticas del sistema (ADMIN ONLY)
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetSystemStats()
        {
            try
            {
                var totalCustomers = await _context.Customers.CountAsync();
                var activeCustomers = await _context.Customers.CountAsync(c => c.Status == "Active");
                var totalTrackers = await _context.Trackers.CountAsync();
                var assignedTrackers = await _context.CustomerTrackers.CountAsync(ct => ct.Status == "Active");
                var availableTrackers = totalTrackers - assignedTrackers;
                var onlineTrackers = await _context.Trackers.CountAsync(t => t.LastSeen > DateTime.UtcNow.AddMinutes(-30));

                return Ok(new
                {
                    success = true,
                    stats = new
                    {
                        customers = new
                        {
                            total = totalCustomers,
                            active = activeCustomers
                        },
                        trackers = new
                        {
                            total = totalTrackers,
                            assigned = assignedTrackers,
                            available = availableTrackers,
                            online = onlineTrackers
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.GetSystemStats] Error getting stats");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo estadísticas"
                });
            }
        }

        /// <summary>
        /// Obtiene todos los usuarios del sistema para dropdown (ADMIN ONLY)
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                _logger.LogInformation("[Admin.GetAllUsers] Getting all users");

                var users = await _context.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Name)
                    .Select(u => new
                    {
                        id = u.Id,
                        name = u.Name,
                        email = u.Email,
                        hasCustomer = _context.Customers.Any(c => c.UserId == u.Id)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    users = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.GetAllUsers] Error getting users");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo usuarios"
                });
            }
        }

        /// <summary>
        /// Crea un nuevo customer (ADMIN ONLY)
        /// </summary>
        [HttpPost("customers")]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
        {
            try
            {
                _logger.LogInformation($"[Admin.CreateCustomer] Creating customer for user {request.UserId}");

                // Validar que el usuario existe
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Usuario no encontrado"
                    });
                }

                // Validar que el usuario no tenga ya un customer
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == request.UserId);

                if (existingCustomer != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"El usuario '{user.Name}' ya tiene un customer asociado"
                    });
                }

                // Crear el customer
                var customer = new Customer
                {
                    UserId = request.UserId,
                    CompanyName = request.CompanyName,
                    Plan = request.Plan,
                    Status = "Active",
                    SubscriptionStart = DateTime.UtcNow,
                    SubscriptionEnd = request.SubscriptionEnd ?? DateTime.UtcNow.AddDays(365),
                    TrackerLimit = request.TrackerLimit,
                    FarmLimit = request.FarmLimit,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"[Admin.CreateCustomer] Customer {customer.Id} created successfully for user {request.UserId}");

                return Ok(new
                {
                    success = true,
                    message = $"Customer '{customer.CompanyName}' creado exitosamente",
                    customer = new
                    {
                        id = customer.Id,
                        userId = customer.UserId,
                        userName = user.Name,
                        userEmail = user.Email,
                        companyName = customer.CompanyName,
                        plan = customer.Plan,
                        trackerLimit = customer.TrackerLimit,
                        farmLimit = customer.FarmLimit,
                        status = customer.Status,
                        subscriptionEnd = customer.SubscriptionEnd
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.CreateCustomer] Error creating customer");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error creando customer",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene todos los trackers disponibles para asignación (ADMIN ONLY - sin autenticación de usuario)
        /// </summary>
        [HttpGet("available-trackers")]
        public async Task<IActionResult> GetAvailableTrackers()
        {
            try
            {
                _logger.LogInformation("[Admin.GetAvailableTrackers] Getting all available trackers");

                var availableTrackers = await _trackerDiscoveryService.GetAvailableTrackersAsync();

                _logger.LogInformation($"[Admin.GetAvailableTrackers] Found {availableTrackers.Count} available trackers");

                return Ok(new
                {
                    success = true,
                    trackers = availableTrackers.Select(t => new
                    {
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
                    count = availableTrackers.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.GetAvailableTrackers] Error getting available trackers");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo trackers disponibles"
                });
            }
        }

        /// <summary>
        /// Obtiene los trackers detectados/transmitiendo disponibles para asignación (ADMIN ONLY - sin autenticación de usuario)
        /// </summary>
        [HttpGet("detected-trackers")]
        public async Task<IActionResult> GetDetectedTrackers()
        {
            try
            {
                _logger.LogInformation("[Admin.GetDetectedTrackers] Getting detected trackers");

                var detectedTrackers = await _trackerDiscoveryService.GetAvailableUnassignedTrackersAsync();

                _logger.LogInformation($"[Admin.GetDetectedTrackers] Found {detectedTrackers.Count} detected trackers");

                return Ok(new
                {
                    success = true,
                    newTrackers = detectedTrackers.Select(t => new
                    {
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
                    count = detectedTrackers.Count,
                    message = detectedTrackers.Count > 0
                        ? $"Se encontraron {detectedTrackers.Count} trackers detectados disponibles"
                        : "No se encontraron trackers detectados actualmente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin.GetDetectedTrackers] Error getting detected trackers");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo trackers detectados"
                });
            }
        }
    }

    public class AdminAssignRequest
    {
        public int TrackerId { get; set; }
        public int CustomerId { get; set; }
    }

    public class CreateCustomerRequest
    {
        public int UserId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Plan { get; set; } = "Trial"; // Trial, Basic, Premium, Enterprise
        public int TrackerLimit { get; set; } = 5;
        public int FarmLimit { get; set; } = 1;
        public DateTime? SubscriptionEnd { get; set; }
    }
}
