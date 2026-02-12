using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class FarmRepository : IFarmRepository
    {
        private readonly CattleTrackingContext _context;

        public FarmRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<Farm?> GetByIdAsync(int id)
        {
            return await _context.Farms.FindAsync(id);
        }

        public async Task<IEnumerable<Farm>> GetAllAsync()
        {
            return await _context.Farms.ToListAsync();
        }

        public async Task<Farm> AddAsync(Farm farm)
        {
            _context.Farms.Add(farm);
            await _context.SaveChangesAsync();
            return farm;
        }

        public async Task UpdateAsync(Farm farm)
        {
            _context.Farms.Update(farm);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Farm farm)
        {
            _context.Farms.Remove(farm);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Farm>> GetFarmsByUserAsync(int userId)
        {
            return await _context.Farms
                .Where(f => f.UserId == userId)
                .ToListAsync();
        }

        public async Task<Farm?> GetFarmWithAnimalsAsync(int id)
        {
            return await _context.Farms
                .Where(f => f.Id == id)
                .Include(f => f.Animals)
                .ThenInclude(a => a.Tracker)
                .FirstOrDefaultAsync();
        }

        public async Task<Farm?> GetFarmWithPasturesAsync(int id)
        {
            return await _context.Farms
                .Where(f => f.Id == id)
                .Include(f => f.Pastures)
                .FirstOrDefaultAsync();
        }

        public async Task<Farm?> GetFarmWithBoundariesAsync(int id)
        {
            return await _context.Farms
                .Where(f => f.Id == id)
                .Include(f => f.BoundaryCoordinates.OrderBy(b => b.SequenceOrder))
                .FirstOrDefaultAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Farms.AnyAsync(f => f.Id == id);
        }

        // New methods for boundary coordinates
        public async Task<Farm?> GetByIdWithBoundariesAsync(int id)
        {
            return await _context.Farms
                .Where(f => f.Id == id)
                .Include(f => f.BoundaryCoordinates)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Farm>> GetAllWithBoundariesAsync()
        {
            return await _context.Farms
                .Include(f => f.BoundaryCoordinates)
                .ToListAsync();
        }

        public async Task<IEnumerable<Farm>> GetFarmsByUserWithBoundariesAsync(int userId)
        {
            return await _context.Farms
                .Where(f => f.UserId == userId)
                .Include(f => f.BoundaryCoordinates)
                .ToListAsync();
        }

        public async Task SetFarmBoundariesAsync(int farmId, List<LatLngDto> boundaries)
        {
            var farmBoundaries = new List<FarmBoundary>();

            for (int i = 0; i < boundaries.Count; i++)
            {
                farmBoundaries.Add(new FarmBoundary
                {
                    FarmId = farmId,
                    Latitude = boundaries[i].Lat,
                    Longitude = boundaries[i].Lng,
                    SequenceOrder = i + 1,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.FarmBoundaries.AddRange(farmBoundaries);
            await _context.SaveChangesAsync();
        }

        public async Task ClearFarmBoundariesAsync(int farmId)
        {
            var existingBoundaries = await _context.FarmBoundaries
                .Where(fb => fb.FarmId == farmId)
                .ToListAsync();

            if (existingBoundaries.Any())
            {
                _context.FarmBoundaries.RemoveRange(existingBoundaries);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<LatLngDto>> GetFarmBoundariesAsync(int farmId)
        {
            var boundaries = await _context.FarmBoundaries
                .Where(fb => fb.FarmId == farmId)
                .OrderBy(fb => fb.SequenceOrder)
                .Select(fb => new LatLngDto
                {
                    Lat = fb.Latitude,
                    Lng = fb.Longitude
                })
                .ToListAsync();

            return boundaries;
        }
    }
}
