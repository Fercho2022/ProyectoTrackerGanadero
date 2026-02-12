using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ApiWebTrackerGanado.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SetupController : ControllerBase
    {
        private readonly CattleTrackingContext _context;

        public SetupController(CattleTrackingContext context)
        {
            _context = context;
        }

        [HttpPost("create-tables")]
        public async Task<IActionResult> CreateTables()
        {
            try
            {
                // Execute raw SQL to create tables
                await _context.Database.ExecuteSqlRawAsync(@"
                    -- Create Customers table
                    CREATE TABLE IF NOT EXISTS ""Customers"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""UserId"" INTEGER NOT NULL,
                        ""CompanyName"" VARCHAR(200) NOT NULL,
                        ""TaxId"" VARCHAR(50),
                        ""ContactPerson"" VARCHAR(100),
                        ""Phone"" VARCHAR(20),
                        ""Address"" VARCHAR(500),
                        ""City"" VARCHAR(100),
                        ""Country"" VARCHAR(100),
                        ""Plan"" VARCHAR(50) NOT NULL DEFAULT 'Basic',
                        ""TrackerLimit"" INTEGER NOT NULL DEFAULT 10,
                        ""FarmLimit"" INTEGER NOT NULL DEFAULT 1,
                        ""Status"" VARCHAR(20) NOT NULL DEFAULT 'Active',
                        ""SubscriptionStart"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        ""SubscriptionEnd"" TIMESTAMP,
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                    );");

                await _context.Database.ExecuteSqlRawAsync(@"
                    -- Create Licenses table
                    CREATE TABLE IF NOT EXISTS ""Licenses"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""CustomerId"" INTEGER NOT NULL,
                        ""LicenseKey"" VARCHAR(50) NOT NULL UNIQUE,
                        ""LicenseType"" VARCHAR(50) NOT NULL DEFAULT 'Basic',
                        ""MaxTrackers"" INTEGER NOT NULL DEFAULT 10,
                        ""MaxFarms"" INTEGER NOT NULL DEFAULT 1,
                        ""MaxUsers"" INTEGER NOT NULL DEFAULT 1,
                        ""Features"" VARCHAR(1000),
                        ""Status"" VARCHAR(20) NOT NULL DEFAULT 'Active',
                        ""IssuedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        ""ActivatedAt"" TIMESTAMP,
                        ""ExpiresAt"" TIMESTAMP NOT NULL DEFAULT NOW() + INTERVAL '1 year',
                        ""ActivationIp"" VARCHAR(50),
                        ""HardwareId"" VARCHAR(100),
                        ""Notes"" VARCHAR(500),
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                    );");

                await _context.Database.ExecuteSqlRawAsync(@"
                    -- Create CustomerTrackers table
                    CREATE TABLE IF NOT EXISTS ""CustomerTrackers"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""CustomerId"" INTEGER NOT NULL,
                        ""TrackerId"" INTEGER NOT NULL,
                        ""FarmId"" INTEGER,
                        ""AnimalName"" VARCHAR(100),
                        ""Status"" VARCHAR(20) NOT NULL DEFAULT 'Active',
                        ""AssignedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        ""UnassignedAt"" TIMESTAMP,
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                    );");

                // Insert test customer
                await _context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO ""Customers"" (""UserId"", ""CompanyName"", ""ContactPerson"", ""Phone"", ""Address"", ""Status"", ""Plan"", ""TrackerLimit"")
                    SELECT 1, 'Test Company', 'Test Contact Person', '123-456-7890', 'Test Address 123', 'Active', 'Premium', 50
                    WHERE NOT EXISTS (SELECT 1 FROM ""Customers"" WHERE ""CompanyName"" = 'Test Company');");

                // Insert test license
                await _context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO ""Licenses"" (""CustomerId"", ""LicenseKey"", ""LicenseType"", ""MaxTrackers"", ""MaxFarms"", ""MaxUsers"", ""Status"", ""IssuedAt"", ""ExpiresAt"")
                    SELECT c.""Id"", 'TG-2024-1234-5678-9ABC', 'Premium', 50, 5, 10, 'Active', NOW(), NOW() + INTERVAL '1 year'
                    FROM ""Customers"" c
                    WHERE c.""CompanyName"" = 'Test Company'
                      AND NOT EXISTS (SELECT 1 FROM ""Licenses"" WHERE ""LicenseKey"" = 'TG-2024-1234-5678-9ABC');");

                return Ok(new {
                    message = "Tables and test data created successfully",
                    licenseKey = "TG-2024-1234-5678-9ABC",
                    instructions = "You can now activate the license using the license key above"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating tables", error = ex.Message });
            }
        }

        /// <summary>
        /// Crea CustomerTrackers faltantes automáticamente
        /// </summary>
        [HttpPost("create-customer-trackers")]
        public async Task<IActionResult> CreateCustomerTrackers()
        {
            try
            {
                // Buscar customer activo para usuario 1
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == 1 && c.Status == "Active");

                if (customer == null)
                {
                    return BadRequest(new { error = "No active customer found for user 1" });
                }

                // Encontrar trackers que no tienen CustomerTracker
                var trackersWithoutCustomerTracker = await _context.Trackers
                    .Where(t => !_context.CustomerTrackers.Any(ct => ct.TrackerId == t.Id))
                    .ToListAsync();

                var createdCount = 0;
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
                    createdCount++;
                }

                await _context.SaveChangesAsync();

                return Ok(new {
                    success = true,
                    message = $"Created {createdCount} CustomerTrackers",
                    createdCount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Endpoint temporal para crear usuario administrador inicial
        /// IMPORTANTE: Eliminar este endpoint en producción
        /// </summary>
        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdminUser()
        {
            try
            {
                var adminEmail = "admin@trackerganadero.com";

                // Verificar si ya existe
                var existingAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

                if (existingAdmin != null)
                {
                    // Actualizar el existente a Admin
                    existingAdmin.Role = "Admin";
                    existingAdmin.IsActive = true;
                    existingAdmin.PasswordHash = HashPassword("admin123");

                    _context.Users.Update(existingAdmin);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "Usuario administrador actualizado exitosamente",
                        credentials = new
                        {
                            email = adminEmail,
                            password = "admin123"
                        },
                        warning = "⚠️ CAMBIA LA CONTRASEÑA INMEDIATAMENTE DESPUÉS DEL PRIMER LOGIN",
                        userId = existingAdmin.Id
                    });
                }

                // Crear nuevo usuario administrador
                var adminUser = new User
                {
                    Name = "Administrador",
                    Email = adminEmail,
                    PasswordHash = HashPassword("admin123"),
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "✓ Usuario administrador creado exitosamente",
                    credentials = new
                    {
                        email = adminEmail,
                        password = "admin123"
                    },
                    warning = "⚠️ CAMBIA LA CONTRASEÑA INMEDIATAMENTE DESPUÉS DEL PRIMER LOGIN",
                    userId = adminUser.Id,
                    note = "Elimina este endpoint (/api/setup/create-admin) en producción"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al crear usuario administrador",
                    error = ex.Message
                });
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "your-salt-here"));
            return Convert.ToBase64String(hashedBytes);
        }

        /// <summary>
        /// Endpoint temporal para arreglar el estado de los trackers asignados
        /// Marca como "Active" los trackers que están asignados a animales
        /// </summary>
        [HttpPost("fix-tracker-status")]
        public async Task<IActionResult> FixTrackerStatus()
        {
            try
            {
                // Buscar trackers que están asignados a animales pero no están en estado "Active"
                var trackersToFix = await _context.Trackers
                    .Where(t => t.Status != "Active" &&
                                _context.Animals.Any(a => a.TrackerId == t.Id))
                    .ToListAsync();

                var fixedCount = 0;
                foreach (var tracker in trackersToFix)
                {
                    tracker.Status = "Active";
                    fixedCount++;
                }

                await _context.SaveChangesAsync();

                // Obtener información de los trackers arreglados
                var fixedTrackers = trackersToFix.Select(t => new
                {
                    t.DeviceId,
                    t.Status,
                    t.IsOnline,
                    t.LastSeen
                }).ToList();

                return Ok(new
                {
                    success = true,
                    message = $"✅ {fixedCount} tracker(s) actualizados a estado 'Active'",
                    fixedCount,
                    trackers = fixedTrackers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error arreglando estado de trackers",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Diagnóstico completo de un tracker específico
        /// </summary>
        [HttpGet("tracker-diagnosis/{deviceId}")]
        public async Task<IActionResult> TrackerDiagnosis(string deviceId)
        {
            try
            {
                var tracker = await _context.Trackers
                    .FirstOrDefaultAsync(t => t.DeviceId == deviceId);

                if (tracker == null)
                {
                    return NotFound(new { success = false, message = $"Tracker {deviceId} no encontrado" });
                }

                var animal = await _context.Animals
                    .FirstOrDefaultAsync(a => a.TrackerId == tracker.Id);

                var customerTracker = await _context.CustomerTrackers
                    .FirstOrDefaultAsync(ct => ct.TrackerId == tracker.Id);

                var locationCount = await _context.LocationHistories
                    .CountAsync(lh => lh.DeviceId == deviceId);

                var lastLocation = await _context.LocationHistories
                    .Where(lh => lh.DeviceId == deviceId)
                    .OrderByDescending(lh => lh.Timestamp)
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    tracker = new
                    {
                        tracker.Id,
                        tracker.DeviceId,
                        tracker.Status,
                        tracker.IsOnline,
                        tracker.LastSeen,
                        tracker.BatteryLevel
                    },
                    animal = animal != null ? new
                    {
                        animal.Id,
                        animal.Name,
                        animal.TrackerId,
                        animal.FarmId
                    } : null,
                    customerTracker = customerTracker != null ? new
                    {
                        customerTracker.Id,
                        customerTracker.CustomerId,
                        customerTracker.Status
                    } : null,
                    locationHistory = new
                    {
                        totalRecords = locationCount,
                        lastLocation = lastLocation != null ? new
                        {
                            lastLocation.Latitude,
                            lastLocation.Longitude,
                            lastLocation.Timestamp
                        } : null
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error en diagnóstico",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Endpoint temporal para obtener ubicaciones sin autenticación (SOLO PARA DEBUG)
        /// </summary>
        [HttpGet("debug-locations/{customerId}")]
        public async Task<IActionResult> DebugLocations(int customerId)
        {
            try
            {
                // Obtener el servicio de tracking
                var trackingService = HttpContext.RequestServices.GetRequiredService<ITrackingService>();

                var allAnimals = await trackingService.GetAllAnimalsLocationsAsync(customerId);

                return Ok(new
                {
                    success = true,
                    customerId = customerId,
                    animalCount = allAnimals.Count(),
                    animals = allAnimals
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error obteniendo ubicaciones",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}