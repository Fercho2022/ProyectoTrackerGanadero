using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestLicenseController : ControllerBase
    {
        private readonly CattleTrackingContext _context;

        public TestLicenseController(CattleTrackingContext context)
        {
            _context = context;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTestLicense()
        {
            try
            {
                // Check if license already exists
                var existingLicense = await _context.Licenses
                    .FirstOrDefaultAsync(l => l.LicenseKey == "TG-2024-1234-5678-9ABC");

                if (existingLicense != null)
                {
                    return Ok(new { message = "Test license already exists", licenseKey = existingLicense.LicenseKey });
                }

                // First create a customer if not exists
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CompanyName == "Test Company");
                if (customer == null)
                {
                    customer = new Customer
                    {
                        UserId = 1, // Assuming user with ID 1 exists
                        CompanyName = "Test Company",
                        ContactPerson = "Test Contact Person",
                        Phone = "123-456-7890",
                        Address = "Test Address 123",
                        Status = "Active"
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }

                var license = new License
                {
                    CustomerId = customer.Id,
                    LicenseKey = "TG-2024-1234-5678-9ABC",
                    LicenseType = "Premium",
                    MaxTrackers = 50,
                    MaxFarms = 5,
                    MaxUsers = 10,
                    Status = "Active"
                };

                _context.Licenses.Add(license);
                await _context.SaveChangesAsync();

                return Ok(new {
                    message = "Test license created successfully",
                    licenseKey = license.LicenseKey,
                    customer = customer.CompanyName,
                    maxTrackers = license.MaxTrackers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating test license", error = ex.Message });
            }
        }

        [HttpPost("create-new")]
        public async Task<IActionResult> CreateNewTestLicense()
        {
            try
            {
                // Check if the new license already exists
                var existingLicense = await _context.Licenses
                    .FirstOrDefaultAsync(l => l.LicenseKey == "TG-2025-TEST-ABCD-1234");

                if (existingLicense != null)
                {
                    return Ok(new {
                        message = "New test license already exists",
                        licenseKey = existingLicense.LicenseKey
                    });
                }

                // Get existing customer
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CompanyName == "Test Company");
                if (customer == null)
                {
                    return BadRequest(new { message = "Test customer not found. Run /create first." });
                }

                var license = new License
                {
                    CustomerId = customer.Id,
                    LicenseKey = "TG-2025-TEST-ABCD-1234",
                    LicenseType = "Premium",
                    MaxTrackers = 50,
                    MaxFarms = 5,
                    MaxUsers = 10,
                    Status = "Active",
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(1)
                };

                _context.Licenses.Add(license);
                await _context.SaveChangesAsync();

                return Ok(new {
                    message = "New test license created successfully",
                    licenseKey = license.LicenseKey,
                    customer = customer.CompanyName,
                    maxTrackers = license.MaxTrackers,
                    instructions = "You can now activate this license using the key above"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    message = "Error creating new test license",
                    error = ex.Message
                });
            }
        }

        [HttpPost("activate-simple")]
        public async Task<IActionResult> ActivateSimple([FromBody] TestActivateLicenseRequest request)
        {
            try
            {
                // Buscar la licencia
                var license = await _context.Licenses
                    .Include(l => l.Customer)
                    .FirstOrDefaultAsync(l => l.LicenseKey == request.LicenseKey);

                if (license == null)
                {
                    return BadRequest(new { success = false, message = "Clave de licencia no encontrada" });
                }

                // Verificar si ya está activada
                if (license.ActivatedAt.HasValue)
                {
                    return Ok(new {
                        success = true,
                        message = "Licencia ya activada anteriormente",
                        license = new {
                            licenseType = license.LicenseType,
                            maxTrackers = license.MaxTrackers,
                            activatedAt = license.ActivatedAt
                        },
                        customer = new {
                            companyName = license.Customer?.CompanyName,
                            plan = license.LicenseType
                        }
                    });
                }

                // Activar la licencia (solo actualizar el campo ActivatedAt)
                license.ActivatedAt = DateTime.UtcNow;
                license.ActivationIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                license.HardwareId = request.HardwareId;

                await _context.SaveChangesAsync();

                return Ok(new {
                    success = true,
                    message = "Licencia activada exitosamente",
                    license = new {
                        licenseType = license.LicenseType,
                        maxTrackers = license.MaxTrackers,
                        maxFarms = license.MaxFarms,
                        expiresAt = license.ExpiresAt,
                        activatedAt = license.ActivatedAt
                    },
                    customer = new {
                        companyName = license.Customer?.CompanyName,
                        plan = license.LicenseType
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    success = false,
                    message = $"Error interno del servidor: {ex.Message}"
                });
            }
        }

        public class TestActivateLicenseRequest
        {
            public string? LicenseKey { get; set; }
            public string? HardwareId { get; set; }
        }
    }
}