using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace ApiWebTrackerGanado.Repositories
{
    public class LocationHistoryRepository: ILocationHistoryRepository
    {
        private readonly CattleTrackingContext _context;

        public LocationHistoryRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<LocationHistory?> GetByIdAsync(int id)
        {
            return await _context.LocationHistories.FindAsync(id);
        }

        public async Task<IEnumerable<LocationHistory>> GetAllAsync()
        {
            return await _context.LocationHistories.ToListAsync();
        }

        public async Task<LocationHistory> AddAsync(LocationHistory locationHistory)
        {
            _context.LocationHistories.Add(locationHistory);
            await _context.SaveChangesAsync();
            return locationHistory;
        }

        public async Task UpdateAsync(LocationHistory locationHistory)
        {
            _context.LocationHistories.Update(locationHistory);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(LocationHistory locationHistory)
        {
            _context.LocationHistories.Remove(locationHistory);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<LocationHistory>> GetAnimalLocationHistoryAsync(int animalId, DateTime from, DateTime to)
        {
            return await _context.LocationHistories
                .Where(lh => lh.AnimalId == animalId &&
                            lh.Timestamp >= from &&
                            lh.Timestamp <= to)
                .OrderBy(lh => lh.Timestamp)
                .ToListAsync();
        }

        public async Task<LocationHistory?> GetAnimalLastLocationAsync(int animalId)
        {
            return await _context.LocationHistories
                .Where(lh => lh.AnimalId == animalId)
                .OrderByDescending(lh => lh.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<LocationHistory>> GetRecentLocationsAsync(int animalId, int hours = 2)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-hours);
            return await _context.LocationHistories
                .Where(lh => lh.AnimalId == animalId && lh.Timestamp >= cutoffTime)
                .OrderByDescending(lh => lh.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<LocationHistory>> GetLocationsInAreaAsync(Polygon area, DateTime from, DateTime to)
        {
            return await _context.LocationHistories
                .Where(lh => lh.Timestamp >= from &&
                            lh.Timestamp <= to &&
                            area.Contains(lh.Location))
                .Include(lh => lh.Animal)
                .ToListAsync();
        }

        public async Task<double> GetAverageActivityLevelAsync(int animalId, int hours = 24)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-hours);
            var activities = await _context.LocationHistories
                .Where(lh => lh.AnimalId == animalId && lh.Timestamp >= cutoffTime)
                .Select(lh => lh.ActivityLevel)
                .ToListAsync();

            return activities.Any() ? activities.Average() : 0;
        }

        public async Task<IEnumerable<LocationHistory>> GetLocationsByTrackerAsync(int trackerId, DateTime from, DateTime to)
        {
            return await _context.LocationHistories
                .Where(lh => lh.TrackerId == trackerId &&
                            lh.Timestamp >= from &&
                            lh.Timestamp <= to)
                .OrderBy(lh => lh.Timestamp)
                .ToListAsync();
        }

        public async Task<LocationHistory?> GetLatestLocationAsync(int animalId, TimeSpan withinTimeSpan)
        {
            var cutoffTime = DateTime.UtcNow - withinTimeSpan;
            return await _context.LocationHistories
                .Where(lh => lh.AnimalId == animalId && lh.Timestamp >= cutoffTime)
                .OrderByDescending(lh => lh.Timestamp)
                .FirstOrDefaultAsync();
        }
    }
}
