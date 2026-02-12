using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ApiWebTrackerGanado.Services.LicenseService _licenseService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserRepository userRepository,
            IConfiguration configuration,
            ApiWebTrackerGanado.Services.LicenseService licenseService,
            ILogger<UsersController> logger)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _licenseService = licenseService;
            _logger = logger;
        }

        [HttpGet]
        
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            var users = await _userRepository.GetAllAsync();
            var userDtos = users.Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt
            });

            return Ok(userDtos);
        }

        [HttpGet("{id}")]
        
        public async Task<ActionResult<object>> GetUser(int id)
        {
            var user = await _userRepository.GetUserWithFarmsAsync(id);
            if (user == null) return NotFound();

            var userDto = new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt,
                FarmsCount = user.Farms.Count,
                Farms = user.Farms.Select(f => new { f.Id, f.Name })
            };

            return Ok(userDto);
        }

        [HttpGet("test-connection")]
        
        public async Task<ActionResult<object>> TestConnection()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                return Ok(new { Status = "Connection successful", UserCount = users.Count() });
            }
            catch (Exception ex)
            {
                return Ok(new { Status = "Connection failed", Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }

        [HttpPost("register")]

        public async Task<ActionResult<object>> Register([FromBody] RegisterUserDto registerDto)
        {
            try
            {
                // Validate model state (includes password confirmation)
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if email already exists
                if (await _userRepository.EmailExistsAsync(registerDto.Email))
                {
                    return BadRequest("Email already exists");
                }

                // Hash password
                var passwordHash = HashPassword(registerDto.Password);

                var user = new User
                {
                    Name = registerDto.Name,
                    Email = registerDto.Email,
                    PasswordHash = passwordHash,
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _userRepository.AddAsync(user);

                // DESHABILITADO: AUTO-GENERACIÓN DE LICENCIA TRIAL
                // El administrador debe asignar licencias manualmente desde el Panel Admin
                // Los nuevos usuarios aparecerán como "Sin licencia" hasta que se les asigne una

                /* CÓDIGO ORIGINAL DE AUTO-GENERACIÓN TRIAL (COMENTADO)
                _logger.LogInformation("Creating trial license for new user {UserId}", user.Id);

                // 1. Crear Customer
                var customer = new Customer
                {
                    UserId = user.Id,
                    CompanyName = registerDto.Name,
                    Plan = "Trial",
                    Status = "Active",
                    SubscriptionStart = DateTime.UtcNow,
                    SubscriptionEnd = DateTime.UtcNow.AddDays(30),
                    TrackerLimit = 5,
                    FarmLimit = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _licenseService.CreateCustomerAsync(customer);

                // 2. Generar licencia Trial
                var trialLicense = await _licenseService.GenerateLicenseAsync(
                    customerId: customer.Id,
                    licenseType: "Trial",
                    maxTrackers: 5,
                    maxFarms: 1,
                    maxUsers: 1,
                    expiresAt: DateTime.UtcNow.AddDays(30),
                    notes: "Licencia Trial auto-generada al registrarse"
                );

                // 3. Auto-activar la licencia Trial (no requiere clave)
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var activationResult = await _licenseService.ValidateAndActivateLicenseAsync(
                    trialLicense.LicenseKey,
                    user.Id,
                    ipAddress,
                    "auto-trial"
                );

                _logger.LogInformation("Trial license created and activated for user {UserId}. License: {LicenseKey}",
                    user.Id, trialLicense.LicenseKey);

                var userDto = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    user.CreatedAt,
                    trial = new
                    {
                        licenseType = "Trial",
                        expiresAt = trialLicense.ExpiresAt,
                        daysRemaining = 30,
                        maxTrackers = 5,
                        maxFarms = 1,
                        message = "Cuenta creada con 30 días de prueba gratuita"
                    }
                };
                */

                _logger.LogInformation("User {UserId} registered successfully without auto-trial license", user.Id);

                var userDto = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    user.CreatedAt,
                    message = "Usuario registrado exitosamente. Un administrador debe asignar una licencia."
                };

                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(500, new { error = "Error creating account", details = ex.Message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            // Try to find user by email (loginDto.Username is actually the email)
            var user = await _userRepository.GetByEmailAsync(loginDto.Username);
            if (user == null || !VerifyPassword(loginDto.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid credentials");
            }

            if (!user.IsActive)
            {
                return Unauthorized("Account is inactive");
            }

            // Generate real JWT token
            var jwtToken = GenerateJwtToken(user);

            // Create the response in the format expected by Blazor MAUI
            var authResponse = new AuthResponseDto
            {
                Token = jwtToken,
                Expiration = DateTime.UtcNow.AddHours(24),
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Name,
                    Email = user.Email,
                    FirstName = user.Name, // Using Name as FirstName for now
                    LastName = "",
                    PhoneNumber = null,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.CreatedAt
                }
            };

            return Ok(authResponse);
        }

        [HttpPut("{id}")]
        
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto updateDto)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

            user.Name = updateDto.Name;
            user.Email = updateDto.Email;
            user.IsActive = updateDto.IsActive;

            if (!string.IsNullOrEmpty(updateDto.Password))
            {
                user.PasswordHash = HashPassword(updateDto.Password);
            }

            await _userRepository.UpdateAsync(user);
            return NoContent();
        }

        [HttpDelete("{id}")]
        
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

            await _userRepository.DeleteAsync(user);
            return NoContent();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "your-salt-here"));
            return Convert.ToBase64String(hashedBytes);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSecret = _configuration["JWT:Secret"] ??
                throw new InvalidOperationException("JWT Secret is not configured");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name),
                new Claim("role", user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
