using TrackerGanadero.Shared.Models;
using Microsoft.Extensions.Logging;

namespace TrackerGanadero.Shared.Services
{
    public class AlertService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<AlertService> _logger;

        public AlertService(HttpService httpService, ILogger<AlertService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<List<AlertDto>> GetAlertsAsync(int? farmId = null, bool? isResolved = null)
        {
            try
            {
                var query = new List<string>();
                if (farmId.HasValue) query.Add($"farmId={farmId}");
                if (isResolved.HasValue) query.Add($"isResolved={isResolved}");

                var endpoint = "api/alerts";
                if (query.Any())
                    endpoint += "?" + string.Join("&", query);

                var alerts = await _httpService.GetAsync<List<AlertDto>>(endpoint);
                return alerts ?? new List<AlertDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alerts");
                throw;
            }
        }

        public async Task<AlertDto?> GetAlertAsync(int alertId)
        {
            try
            {
                return await _httpService.GetAsync<AlertDto>($"api/alerts/{alertId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alert {AlertId}", alertId);
                throw;
            }
        }

        public async Task<bool> ResolveAlertAsync(int alertId)
        {
            try
            {
                await _httpService.PutAsync<object>($"api/alerts/{alertId}/resolve", new { });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
                throw;
            }
        }

        public async Task<List<AlertDto>> GetActiveAlertsAsync()
        {
            return await GetAlertsAsync(isResolved: false);
        }

        public async Task<int> GetActiveAlertCountAsync()
        {
            try
            {
                var alerts = await GetActiveAlertsAsync();
                return alerts.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active alert count");
                return 0;
            }
        }
    }
}