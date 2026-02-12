using TrackerGanaderoBlazorHibridMaui.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    public class AuthService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<AuthService> _logger;
        private readonly SettingsStateService _stateService;
        private const string TokenKey = "auth_token";
        private const string UserKey = "current_user";

        public AuthService(HttpService httpService, ILogger<AuthService> logger, SettingsStateService stateService)
        {
            _httpService = httpService;
            _logger = logger;
            _stateService = stateService;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
        {
            try
            {
                _logger.LogInformation("Attempting login for user: {Username}", loginDto.Username);

                var response = await _httpService.PostAsync<AuthResponseDto>("api/users/login", loginDto);

                _logger.LogInformation("Login response received: {Response}", response != null ? "Success" : "Null");

                if (response != null)
                {
                    _logger.LogInformation("Saving auth data and setting authorization header");
                    await SaveAuthDataAsync(response);
                    _logger.LogInformation("Login completed successfully");
                }
                else
                {
                    _logger.LogWarning("Login response was null");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", loginDto.Username);
                throw;
            }
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterUserDto registerDto)
        {
            try
            {
                _logger.LogInformation("Sending registration request with Name: {Name}, Email: {Email}", registerDto.Name, registerDto.Email);

                // Backend returns a simple user object, not AuthResponseDto
                // We don't need the response, just need to know if it succeeded
                await _httpService.PostAsync<object>("api/users/register", registerDto);

                _logger.LogInformation("Registration API call completed successfully");

                // Return null since backend doesn't provide auth token on registration
                // Caller should do a separate login after registration
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                throw;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                SecureStorage.Remove(TokenKey);
                SecureStorage.Remove(UserKey);

                // IMPORTANTE: Limpiar el estado de la sesión (CustomerInfo, trackers, etc.)
                _stateService.ClearState();

                // Authorization header clearing is now handled automatically by AuthHeaderHandler
                // since it reads from SecureStorage and won't find a token after removal
                _logger.LogInformation("User logged out successfully and state cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
            }
        }

        // NOTE: Token initialization is now handled automatically by AuthHeaderHandler
        // No manual initialization is needed since AuthHeaderHandler reads from SecureStorage

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync(TokenKey);
                if (string.IsNullOrEmpty(token))
                    return false;

                // TODO: Check if token is expired
                // Note: AuthHeaderHandler will automatically use this token for requests
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authentication status");
                return false;
            }
        }

        public async Task<UserDto?> GetCurrentUserAsync()
        {
            try
            {
                var userJson = await SecureStorage.GetAsync(UserKey);
                if (string.IsNullOrEmpty(userJson))
                    return null;

                return JsonSerializer.Deserialize<UserDto>(userJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return null;
            }
        }

        private async Task SaveAuthDataAsync(AuthResponseDto authResponse)
        {
            try
            {
                await SecureStorage.SetAsync(TokenKey, authResponse.Token);
                var userJson = JsonSerializer.Serialize(authResponse.User);
                await SecureStorage.SetAsync(UserKey, userJson);

                _logger.LogInformation("Authentication data saved successfully");
                System.Diagnostics.Debug.WriteLine($"[AuthService] Token saved: {!string.IsNullOrEmpty(authResponse.Token)}");
                System.Diagnostics.Debug.WriteLine($"[AuthService] Token key used: {TokenKey}");

                // Verificar que se guardó correctamente
                var savedToken = await SecureStorage.GetAsync(TokenKey);
                System.Diagnostics.Debug.WriteLine($"[AuthService] Token verification - saved correctly: {!string.IsNullOrEmpty(savedToken)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving auth data");
                throw;
            }
        }
    }
}