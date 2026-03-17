using Microsoft.Extensions.Logging;
using TrackerGanadero.Shared.Models;

namespace TrackerGanadero.Shared.Services
{
    public class DashboardService
    {
        private readonly HttpService _httpService;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(HttpService httpService, ILogger<DashboardService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<DashboardStatsDto?> GetStatsAsync()
        {
            try
            {
                return await _httpService.GetAsync<DashboardStatsDto>("api/dashboard/stats");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return null;
            }
        }
    }

    // Mirror DTOs from API
    public class DashboardStatsDto
    {
        public int TotalAnimals { get; set; }
        public int AnimalsWithTrackers { get; set; }
        public int TotalFarms { get; set; }
        public int ActiveAlerts { get; set; }
        public List<AlertDto> RecentAlerts { get; set; } = new();
        public List<AnimalSummaryDto> RecentAnimals { get; set; } = new();
    }

    public class AnimalSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Breed { get; set; } = "";
        public string Status { get; set; } = "";
        public int? TrackerId { get; set; }
        public bool TrackerIsOnline { get; set; }
        public DateTime? LastLocationTimestamp { get; set; }
    }
}
