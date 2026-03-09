using Microsoft.Extensions.Logging;
using TrackerGanadero.Shared.Models;

namespace TrackerGanadero.Shared.Services
{
    public class NotificationSettingsService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<NotificationSettingsService> _logger;

        public NotificationSettingsService(HttpService httpService, ILogger<NotificationSettingsService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<NotificationSettingsModel?> GetSettingsAsync()
        {
            try
            {
                return await _httpService.GetAsync<NotificationSettingsModel>("api/notificationsettings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notification settings");
                return null;
            }
        }

        public async Task<bool> UpdateSettingsAsync(NotificationSettingsModel settings)
        {
            try
            {
                await _httpService.PutAsync<object>("api/notificationsettings", settings);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification settings");
                return false;
            }
        }
    }
}
