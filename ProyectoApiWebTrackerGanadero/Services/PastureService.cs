using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Services
{
    public class PastureService : IPastureService 
    {
        private readonly CattleTrackingContext _context;

        public PastureService(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PastureUsageDto>> GetPastureUsageReportAsync(int farmId, DateTime from, DateTime to)
        {
            var pastureUsage = await _context.PastureUsages
                .Include(pu => pu.Pasture)
                .Include(pu => pu.Animal)
                .Where(pu => pu.Pasture.FarmId == farmId &&
                            pu.StartTime >= from &&
                            pu.StartTime <= to)
                .GroupBy(pu => pu.PastureId)
                .Select(g => new PastureUsageDto
                {
                    PastureId = g.Key,
                    PastureName = g.First().Pasture.Name,
                    AnimalCount = g.Select(pu => pu.AnimalId).Distinct().Count(),
                    AverageUsageTime = TimeSpan.FromTicks((long)g.Average(pu => pu.Duration.Ticks)),
                    LastUsed = g.Max(pu => pu.StartTime)
                })
                .ToListAsync();

            return pastureUsage;
        }

        public async Task UpdatePastureUsageAsync()
        {
            // Get all active pastures
            var pastures = await _context.Pastures
                .Where(p => p.IsActive)
                .ToListAsync();

            foreach (var pasture in pastures)
            {
                // Get animals currently in this pasture
                var animalsInPasture = await _context.LocationHistories
                    .Where(lh => lh.Timestamp >= DateTime.UtcNow.AddMinutes(-10) && // Last 10 minutes
                                lh.Location.Within(pasture.Area) &&
                                lh.AnimalId.HasValue) // Only include records with valid AnimalId
                    .Select(lh => lh.AnimalId.Value)
                    .Distinct()
                    .ToListAsync();

                foreach (var animalId in animalsInPasture)
                {
                    // Check if there's an active usage record
                    var activeUsage = await _context.PastureUsages
                        .FirstOrDefaultAsync(pu => pu.AnimalId == animalId &&
                                                  pu.PastureId == pasture.Id &&
                                                  pu.EndTime == null);

                    if (activeUsage == null)
                    {
                        // Create new usage record
                        var newUsage = new PastureUsage
                        {
                            PastureId = pasture.Id,
                            AnimalId = animalId,
                            StartTime = DateTime.UtcNow
                        };
                        _context.PastureUsages.Add(newUsage);
                    }
                }

                // End usage for animals no longer in pasture
                var activeUsages = await _context.PastureUsages
                    .Where(pu => pu.PastureId == pasture.Id && pu.EndTime == null)
                    .ToListAsync();

                foreach (var usage in activeUsages)
                {
                    if (!animalsInPasture.Contains(usage.AnimalId))
                    {
                        usage.EndTime = DateTime.UtcNow;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<PastureUsageDto>> GetCurrentPastureOccupancyAsync(int farmId)
        {
            var currentOccupancy = await _context.PastureUsages
                .Include(pu => pu.Pasture)
                .Where(pu => pu.Pasture.FarmId == farmId && pu.EndTime == null)
                .GroupBy(pu => pu.PastureId)
                .Select(g => new PastureUsageDto
                {
                    PastureId = g.Key,
                    PastureName = g.First().Pasture.Name,
                    AnimalCount = g.Count(),
                    AverageUsageTime = TimeSpan.FromTicks((long)g.Average(pu => pu.Duration.Ticks)),
                    LastUsed = g.Max(pu => pu.StartTime)
                })
                .ToListAsync();

            return currentOccupancy;
        }
    }
}

