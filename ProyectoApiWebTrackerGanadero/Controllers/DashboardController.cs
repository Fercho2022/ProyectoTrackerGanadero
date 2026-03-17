using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly CattleTrackingContext _context;

        public DashboardController(CattleTrackingContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns all dashboard data in a single optimized query.
        /// Much faster than making 3 separate API calls.
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
        {
            var currentUserId = GetCurrentUserId();

            // Run all count queries in parallel - these are simple COUNT(*) queries, very fast
            var totalAnimalsTask = _context.Animals
                .AsNoTracking()
                .Where(a => a.Farm.UserId == currentUserId)
                .CountAsync();

            var animalsWithTrackersTask = _context.Animals
                .AsNoTracking()
                .Where(a => a.Farm.UserId == currentUserId && a.TrackerId != null)
                .CountAsync();

            var totalFarmsTask = _context.Farms
                .AsNoTracking()
                .Where(f => f.UserId == currentUserId)
                .CountAsync();

            var activeAlertsCountTask = _context.Alerts
                .AsNoTracking()
                .Where(a => a.Animal.Farm.UserId == currentUserId && !a.IsResolved)
                .CountAsync();

            // Recent alerts - only top 5 with projection (no entity loading)
            var recentAlertsTask = _context.Alerts
                .AsNoTracking()
                .Where(a => a.Animal.Farm.UserId == currentUserId && !a.IsResolved)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new AlertDto
                {
                    Id = a.Id,
                    Type = a.Type,
                    Title = a.Type,
                    Severity = a.Severity,
                    Message = a.Message,
                    AnimalId = a.AnimalId,
                    FarmId = a.Animal.FarmId,
                    AnimalName = a.Animal.Name ?? "N/A",
                    FarmName = a.Animal.Farm.Name,
                    IsRead = a.IsRead,
                    IsResolved = a.IsResolved,
                    CreatedAt = a.CreatedAt,
                    ResolvedAt = a.ResolvedAt
                })
                .ToListAsync();

            // Recent animals - only top 10 with projection
            var recentAnimalsTask = _context.Animals
                .AsNoTracking()
                .Where(a => a.Farm.UserId == currentUserId)
                .OrderByDescending(a => a.Id)
                .Take(10)
                .Select(a => new AnimalSummaryDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Tag = a.Tag,
                    Breed = a.Breed,
                    Status = a.Status,
                    TrackerId = a.TrackerId,
                    TrackerIsOnline = a.Tracker != null && a.Tracker.IsOnline,
                    LastLocationTimestamp = _context.LocationHistories
                        .Where(lh => lh.AnimalId == a.Id)
                        .OrderByDescending(lh => lh.Timestamp)
                        .Select(lh => (DateTime?)lh.Timestamp)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // Wait for all queries to complete in parallel
            await Task.WhenAll(
                totalAnimalsTask,
                animalsWithTrackersTask,
                totalFarmsTask,
                activeAlertsCountTask,
                recentAlertsTask,
                recentAnimalsTask
            );

            var result = new DashboardStatsDto
            {
                TotalAnimals = totalAnimalsTask.Result,
                AnimalsWithTrackers = animalsWithTrackersTask.Result,
                TotalFarms = totalFarmsTask.Result,
                ActiveAlerts = activeAlertsCountTask.Result,
                RecentAlerts = recentAlertsTask.Result,
                RecentAnimals = recentAnimalsTask.Result
            };

            return Ok(result);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                userIdClaim = User.FindFirst("sub")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return 1;
        }
    }

    // DTO for dashboard stats - lightweight
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
