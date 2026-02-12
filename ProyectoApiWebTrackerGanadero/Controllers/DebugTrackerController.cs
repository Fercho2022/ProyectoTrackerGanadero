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
    public class DebugTrackerController : ControllerBase
    {
        private readonly TrackerDiscoveryService _trackerDiscoveryService;
        private readonly CattleTrackingContext _context;
        private readonly LicenseService _licenseService;
        private readonly ILogger<DebugTrackerController> _logger;

        public DebugTrackerController(
            TrackerDiscoveryService trackerDiscoveryService,
            CattleTrackingContext context,
            LicenseService licenseService,
            ILogger<DebugTrackerController> logger)
        {
            _trackerDiscoveryService = trackerDiscoveryService;
            _context = context;
            _licenseService = licenseService;
            _logger = logger;
        }

        [HttpGet("location-data")]
        public async Task<IActionResult> GetLocationData()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-60); // Look at last hour

                var recentData = await _context.LocationHistories
                    .Where(lh => lh.Timestamp > cutoffTime)
                    .Select(lh => new {
                        lh.DeviceId,
                        lh.Timestamp,
                        lh.Latitude,
                        lh.Longitude
                    })
                    .OrderByDescending(lh => lh.Timestamp)
                    .Take(20)
                    .ToListAsync();

                var deviceIds = await _context.LocationHistories
                    .Where(lh => lh.Timestamp > cutoffTime)
                    .Where(lh => !string.IsNullOrEmpty(lh.DeviceId))
                    .Select(lh => lh.DeviceId!)
                    .Distinct()
                    .ToListAsync();

                var existingTrackers = await _context.Trackers
                    .Select(t => new { t.DeviceId, t.Name, t.Status })
                    .ToListAsync();

                return Ok(new
                {
                    cutoffTime,
                    recentDataCount = recentData.Count,
                    recentData,
                    uniqueDeviceIds = deviceIds,
                    uniqueDeviceIdCount = deviceIds.Count,
                    existingTrackers,
                    existingTrackerCount = existingTrackers.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("test-discovery-detect")]
        public async Task<IActionResult> TestDiscovery()
        {
            try
            {
                var result = await _trackerDiscoveryService.DetectNewTrackersAsync();
                return Ok(new
                {
                    detectedCount = result.Count,
                    detectedTrackers = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("test-discovery-active")]
        public async Task<IActionResult> TestTrackingDiscovery()
        {
            try
            {
                var activeTrackers = await _trackerDiscoveryService.GetActiveTransmittingTrackersAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Se encontraron {activeTrackers.Count} trackers transmitiendo",
                    trackers = activeTrackers.Select(t => new {
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
                    count = activeTrackers.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("simulate-gps-data")]
        public async Task<IActionResult> SimulateGpsData()
        {
            try
            {
                var testDeviceId = "TEST_GPS_001";
                var locationHistory = new ApiWebTrackerGanado.Models.LocationHistory
                {
                    DeviceId = testDeviceId,
                    Latitude = -32.5217,
                    Longitude = -58.2344,
                    Timestamp = DateTime.UtcNow,
                    Speed = 0,
                    Altitude = 100,
                    ActivityLevel = 5,
                    Temperature = 37.5,
                    SignalStrength = 85
                };

                _context.LocationHistories.Add(locationHistory);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Test GPS data created",
                    deviceId = testDeviceId,
                    timestamp = locationHistory.Timestamp
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("fix-database")]
        public async Task<IActionResult> FixDatabase()
        {
            try
            {
                var results = new List<string>();

                // 1. Verificar si la columna CustomerTrackerId existe
                var checkColumnSql = @"
                    SELECT column_name
                    FROM information_schema.columns
                    WHERE table_name = 'Animals' AND column_name = 'CustomerTrackerId'";

                var columnExists = false;
                try
                {
                    var existingColumns = await _context.Database.SqlQueryRaw<string>(checkColumnSql).ToListAsync();
                    columnExists = existingColumns.Any();
                    results.Add($"✓ Column check completed. Exists: {columnExists}");
                }
                catch (Exception ex)
                {
                    results.Add($"⚠️ Column check failed: {ex.Message}");
                }

                // 2. Agregar la columna si no existe
                if (!columnExists)
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Animals"" ADD COLUMN ""CustomerTrackerId"" integer");
                        results.Add("✅ CustomerTrackerId column added successfully");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"❌ Failed to add CustomerTrackerId column: {ex.Message}");
                        return BadRequest(new { success = false, results });
                    }

                    // 3. Agregar foreign key constraint
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(@"
                            ALTER TABLE ""Animals""
                            ADD CONSTRAINT ""FK_Animals_CustomerTrackers_CustomerTrackerId""
                            FOREIGN KEY (""CustomerTrackerId"")
                            REFERENCES ""CustomerTrackers"" (""Id"")
                            ON DELETE SET NULL");
                        results.Add("✅ Foreign key constraint added successfully");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"⚠️ Foreign key constraint failed (may already exist): {ex.Message}");
                    }

                    // 4. Crear índice
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(@"CREATE INDEX ""IX_Animals_CustomerTrackerId"" ON ""Animals"" (""CustomerTrackerId"")");
                        results.Add("✅ Index created successfully");
                    }
                    catch (Exception ex)
                    {
                        results.Add($"⚠️ Index creation failed (may already exist): {ex.Message}");
                    }
                }
                else
                {
                    results.Add("ℹ️ CustomerTrackerId column already exists");
                }

                // 5. Verificación final
                var finalCheck = await _context.Database.SqlQueryRaw<string>(checkColumnSql).ToListAsync();
                var success = finalCheck.Any();
                results.Add($"🎯 Final verification: CustomerTrackerId exists = {success}");

                return Ok(new
                {
                    success = success,
                    message = success ? "Database migration completed successfully!" : "Database migration failed!",
                    results = results,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("check-database")]
        public async Task<IActionResult> CheckDatabase()
        {
            try
            {
                // Verificar si la tabla Animals existe
                var tableExistsResult = await _context.Database.SqlQueryRaw<string>(@"
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_name = 'Animals'").ToListAsync();

                // Verificar columnas de la tabla Animals
                var columnsResult = await _context.Database.SqlQueryRaw<string>(@"
                    SELECT column_name
                    FROM information_schema.columns
                    WHERE table_name = 'Animals'
                    ORDER BY ordinal_position").ToListAsync();

                // Verificar si CustomerTrackerId existe específicamente
                var customerTrackerColumnResult = await _context.Database.SqlQueryRaw<string>(@"
                    SELECT column_name
                    FROM information_schema.columns
                    WHERE table_name = 'Animals' AND column_name = 'CustomerTrackerId'").ToListAsync();

                return Ok(new
                {
                    success = true,
                    tableExists = tableExistsResult.Any(),
                    allColumns = columnsResult,
                    hasCustomerTrackerColumn = customerTrackerColumnResult.Any(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("quick-fix")]
        public async Task<IActionResult> QuickFix()
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Animals"" ADD COLUMN ""CustomerTrackerId"" integer");
                return Ok(new { success = true, message = "Column added successfully" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("check-customer")]
        public async Task<IActionResult> CheckCustomer()
        {
            try
            {
                var users = await _context.Users.CountAsync();
                var customers = await _context.Customers.CountAsync();
                var customer1 = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == 1);

                return Ok(new
                {
                    usersCount = users,
                    customersCount = customers,
                    hasCustomerForUser1 = customer1 != null,
                    customer1 = customer1?.CompanyName ?? "No existe"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("create-customer")]
        public async Task<IActionResult> CreateCustomer()
        {
            try
            {
                // Crear usuario de prueba
                var user = new ApiWebTrackerGanado.Models.User
                {
                    Name = "Usuario Prueba",
                    Email = "test@test.com",
                    PasswordHash = "hash123",
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Crear customer de prueba
                var customer = new ApiWebTrackerGanado.Models.Customer
                {
                    UserId = user.Id,
                    CompanyName = "Granja Test",
                    Plan = "Premium",
                    TrackerLimit = 50,
                    FarmLimit = 10,
                    Status = "Active",
                    SubscriptionStart = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Customer creado",
                    userId = user.Id,
                    customerId = customer.Id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("simple-register")]
        public async Task<IActionResult> SimpleRegister([FromBody] SimpleRegisterRequest request)
        {
            try
            {
                // Registrar tracker sin customer assignment
                var trackerId = await _trackerDiscoveryService.RegisterDetectedTrackerAsync(
                    request.DeviceId, "GPS Tracker v1.0");

                if (!trackerId.HasValue)
                {
                    return BadRequest(new {
                        success = false,
                        message = "No se pudo registrar el tracker"
                    });
                }

                return Ok(new {
                    success = true,
                    message = "Tracker registrado exitosamente",
                    trackerId = trackerId.Value,
                    deviceId = request.DeviceId,
                    note = "Registrado en modo simplificado"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new {
                    success = false,
                    message = "Error registrando tracker",
                    error = ex.Message
                });
            }
        }

        [HttpPost("assign-to-customer")]
        public async Task<IActionResult> AssignTrackerToCustomer([FromBody] AssignToCustomerRequest request)
        {
            try
            {
                _logger.LogInformation($"[AssignTrackerToCustomer] Starting assignment for tracker ID: {request.TrackerId}");

                // SECURITY FIX: Obtener el usuario actual del JWT token (NO usar UserId=1)
                var userId = GetCurrentUserId();
                _logger.LogInformation($"[AssignTrackerToCustomer] Current user ID from JWT: {userId}");

                // Buscar el tracker
                var tracker = await _context.Trackers
                    .FirstOrDefaultAsync(t => t.Id == request.TrackerId);

                if (tracker == null)
                {
                    _logger.LogWarning($"[AssignTrackerToCustomer] Tracker ID {request.TrackerId} not found");
                    return NotFound(new {
                        success = false,
                        message = "Tracker no encontrado"
                    });
                }

                _logger.LogInformation($"[AssignTrackerToCustomer] Found tracker: {tracker.DeviceId}");

                // SECURITY FIX: Obtener el customer del usuario actual (del JWT)
                var customer = await _licenseService.GetCurrentCustomerAsync(userId);

                if (customer == null)
                {
                    _logger.LogWarning($"[AssignTrackerToCustomer] No customer found for user ID {userId}");
                    return BadRequest(new {
                        success = false,
                        message = "No hay licencia activa. Active una licencia primero para asignar trackers."
                    });
                }

                _logger.LogInformation($"[AssignTrackerToCustomer] Found customer: {customer.CompanyName} (ID: {customer.Id})");

                // SECURITY FIX: Validar límite de trackers del customer
                var currentTrackerCount = await _context.CustomerTrackers
                    .Where(ct => ct.CustomerId == customer.Id && ct.Status == "Active")
                    .CountAsync();

                _logger.LogInformation($"[AssignTrackerToCustomer] Customer has {currentTrackerCount}/{customer.TrackerLimit} trackers");

                if (currentTrackerCount >= customer.TrackerLimit)
                {
                    _logger.LogWarning($"[AssignTrackerToCustomer] Customer reached tracker limit: {currentTrackerCount}/{customer.TrackerLimit}");
                    return BadRequest(new {
                        success = false,
                        message = $"Ha alcanzado el límite de {customer.TrackerLimit} trackers para su plan {customer.Plan}."
                    });
                }

                // Verificar si ya existe la asignación activa para este tracker
                var existingAssignment = await _context.CustomerTrackers
                    .FirstOrDefaultAsync(ct => ct.TrackerId == tracker.Id && ct.Status == "Active");

                if (existingAssignment != null)
                {
                    _logger.LogWarning($"[AssignTrackerToCustomer] Tracker {tracker.DeviceId} already assigned to customer ID {existingAssignment.CustomerId}");

                    // SECURITY: Verificar si está asignado al mismo customer o a otro
                    if (existingAssignment.CustomerId == customer.Id)
                    {
                        return Ok(new {
                            success = true,
                            message = "Tracker ya estaba asignado a tu cuenta",
                            customerTrackerId = existingAssignment.Id,
                            trackerId = tracker.Id,
                            deviceId = tracker.DeviceId
                        });
                    }
                    else
                    {
                        return BadRequest(new {
                            success = false,
                            message = "Este tracker ya está asignado a otro cliente"
                        });
                    }
                }

                // Crear la asignación Customer-Tracker para el usuario actual
                var customerTracker = new CustomerTracker
                {
                    CustomerId = customer.Id,
                    TrackerId = tracker.Id,
                    AssignedAt = DateTime.UtcNow,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.CustomerTrackers.Add(customerTracker);

                // Marcar tracker como no disponible
                tracker.IsAvailableForAssignment = false;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"[AssignTrackerToCustomer] Successfully assigned tracker {tracker.DeviceId} to customer {customer.CompanyName}");

                return Ok(new {
                    success = true,
                    message = "Tracker asignado exitosamente a tu cuenta",
                    customerTrackerId = customerTracker.Id,
                    trackerId = tracker.Id,
                    deviceId = tracker.DeviceId,
                    customerId = customer.Id,
                    customerName = customer.CompanyName
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "[AssignTrackerToCustomer] Authentication error");
                return Unauthorized(new {
                    success = false,
                    message = "No autenticado. Inicie sesión primero."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AssignTrackerToCustomer] Error assigning tracker");
                return BadRequest(new {
                    success = false,
                    message = "Error asignando tracker al customer",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// DEPRECATED: Este endpoint está deshabilitado por seguridad.
        /// Los trackers NO deben asignarse automáticamente.
        /// Use simple-register para registrar y asigne manualmente desde Panel Admin.
        /// </summary>
        [HttpPost("register-and-assign")]
        public IActionResult RegisterAndAssign([FromBody] SimpleRegisterRequest request)
        {
            return BadRequest(new {
                success = false,
                message = "SECURITY ERROR: Auto-assignment is disabled. Trackers must be assigned manually from Admin Panel.",
                code = "AUTO_ASSIGNMENT_DISABLED"
            });
        }
            
                    [HttpPost("reset-all-trackers")]
                    public async Task<IActionResult> ResetAllTrackers()
                    {
                        try
                        {
                            // 1. Remove all CustomerTracker assignments
                            var allAssignments = await _context.CustomerTrackers.ToListAsync();
                            _context.CustomerTrackers.RemoveRange(allAssignments);
                
                            await _context.SaveChangesAsync();
                
                            return Ok(new
                            {
                                success = true,
                                message = $"Reset complete. Removed {allAssignments.Count} assignments."
                            });
                        }
                        catch (Exception ex)
                        {
                            return StatusCode(500, new { success = false, message = "An error occurred during reset.", error = ex.Message });
                        }
                    }

        /// <summary>
        /// Obtiene el ID del usuario actual desde el JWT token
        /// </summary>
        private int GetCurrentUserId()
        {
            _logger.LogInformation($"[DebugTracker.GetCurrentUserId] Checking user authentication...");
            _logger.LogInformation($"[DebugTracker.GetCurrentUserId] User.Identity.IsAuthenticated: {HttpContext.User.Identity?.IsAuthenticated}");

            var userIdClaim = HttpContext.User.FindFirst("sub")?.Value ??
                             HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation($"[DebugTracker.GetCurrentUserId] Found userIdClaim: {userIdClaim}");

            if (int.TryParse(userIdClaim, out var userId))
            {
                _logger.LogInformation($"[DebugTracker.GetCurrentUserId] Parsed userId: {userId}");
                return userId;
            }

            _logger.LogWarning("[DebugTracker.GetCurrentUserId] Could not determine user ID from token claims.");
            throw new UnauthorizedAccessException("Could not determine user ID from token claims.");
        }
    }

            public class SimpleRegisterRequest
            {
                public string DeviceId { get; set; } = string.Empty;
                public string? Name { get; set; }
                public string? SerialNumber { get; set; }
            }
    public class AssignToCustomerRequest
    {
        public int TrackerId { get; set; }
    }
}