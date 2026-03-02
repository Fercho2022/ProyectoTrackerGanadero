using TrackerGanadero.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace TrackerGanadero.Shared.Services
{
    public class AuthService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<AuthService> _logger;
        private readonly SettingsStateService _stateService;
        private readonly ITokenStorageService _tokenStorage;
        private const string TokenKey = "auth_token";
        private const string UserKey = "current_user";

        public AuthService(HttpService httpService, ILogger<AuthService> logger, SettingsStateService stateService, ITokenStorageService tokenStorage)
        {
            _httpService = httpService;
            _logger = logger;
            _stateService = stateService;
            _tokenStorage = tokenStorage;
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

                await _httpService.PostAsync<object>("api/users/register", registerDto);

                _logger.LogInformation("Registration API call completed successfully");

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
                _tokenStorage.Remove(TokenKey);
                _tokenStorage.Remove(UserKey);

                _stateService.ClearState();

                _logger.LogInformation("User logged out successfully and state cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var token = await _tokenStorage.GetAsync(TokenKey);
                if (string.IsNullOrEmpty(token))
                    return false;

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
                var userJson = await _tokenStorage.GetAsync(UserKey);
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
                await _tokenStorage.SetAsync(TokenKey, authResponse.Token);
                var userJson = JsonSerializer.Serialize(authResponse.User);
                await _tokenStorage.SetAsync(UserKey, userJson);

                _logger.LogInformation("Authentication data saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving auth data");
                throw;
            }
        }
    }
}
