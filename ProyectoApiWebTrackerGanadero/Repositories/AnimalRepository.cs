using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace ApiWebTrackerGanado.Repositories
{
    public class AnimalRepository : IAnimalRepository
    {
        private readonly CattleTrackingContext _context;

        public AnimalRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<Animal?> GetByIdAsync(int id)
        {
            return await _context.Animals.FindAsync(id);
        }

        public async Task<IEnumerable<Animal>> GetAllAsync()
        {
            return await _context.Animals.AsNoTracking().ToListAsync();
        }

        public async Task<Animal> AddAsync(Animal animal)
        {
            _context.Animals.Add(animal);
            await _context.SaveChangesAsync();
            return animal;
        }

        public async Task UpdateAsync(Animal animal)
        {
            _context.Animals.Update(animal);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Animal animal)
        {
            _context.Animals.Remove(animal);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Animal>> GetAnimalsByFarmAsync(int farmId)
        {
            return await _context.Animals
                .AsNoTracking()
                .Where(a => a.FarmId == farmId)
                .Include(a => a.Tracker)
                .ToListAsync();
        }

        public async Task<IEnumerable<Animal>> GetAnimalsWithTrackersAsync(int farmId)
        {
            return await _context.Animals
                .AsNoTracking()
                .Where(a => a.FarmId == farmId && a.TrackerId != null)
                .Include(a => a.Tracker)
                .ToListAsync();
        }

        public async Task<Animal?> GetAnimalWithDetailsAsync(int id)
        {
            return await _context.Animals
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Include(a => a.Farm)
                .Include(a => a.Tracker)
                .Include(a => a.HealthRecords.OrderByDescending(hr => hr.RecordDate).Take(5))
                .Include(a => a.WeightRecords.OrderByDescending(wr => wr.WeightDate).Take(5))
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Animal>> GetAnimalsInAreaAsync(Polygon area)
        {
            return await _context.Animals
                .AsNoTracking()
                .Where(a => a.TrackerId != null)
                .Where(a => _context.LocationHistories
                    .Where(lh => lh.AnimalId == a.Id)
                    .OrderByDescending(lh => lh.Timestamp)
                    .Take(1)
                    .Any(lh => area.Contains(lh.Location)))
                .Include(a => a.Tracker)
                .ToListAsync();
        }

        public async Task<IEnumerable<Animal>> GetAnimalsOutsideBoundariesAsync(int farmId)
        {
            // TODO: Re-implement with new boundary system using point-in-polygon calculations
            // Temporarily disabled during boundary system migration
            return new List<Animal>();

            /*
            var farm = await _context.Farms
                .Include(f => f.BoundaryCoordinates)
                .FirstOrDefaultAsync(f => f.Id == farmId);

            if (farm?.BoundaryCoordinates == null || !farm.BoundaryCoordinates.Any())
                return new List<Animal>();

            // Need to implement point-in-polygon algorithm here
            // This would check if each animal's latest location is inside the boundary polygon
            return await _context.Animals
                .Where(a => a.FarmId == farmId && a.TrackerId != null)
                .Include(a => a.Tracker)
                .ToListAsync();
            */
        }

        public async Task<IEnumerable<Animal>> GetBreedingFemalesAsync(int farmId)
        {
            var minAge = DateTime.UtcNow.AddMonths(-15);
            return await _context.Animals
                .AsNoTracking()
                .Where(a => a.FarmId == farmId &&
                           a.Gender.ToLower() == "female" &&
                           a.BirthDate <= minAge &&
                           a.Status == "Active")
                .ToListAsync();
        }

        public async Task<IEnumerable<Animal>> GetAnimalsByStatusAsync(int farmId, string status)
        {
            return await _context.Animals
                .AsNoTracking()
                .Where(a => a.FarmId == farmId && a.Status == status)
                .Include(a => a.Tracker)
                .ToListAsync();
        }

        public async Task<decimal> GetAverageWeightByBreedAsync(int farmId, string breed)
        {
            var weights = await _context.Animals
                .AsNoTracking()
                .Where(a => a.FarmId == farmId && a.Breed == breed && a.Weight > 0)
                .Select(a => a.Weight)
                .ToListAsync();

            return weights.Any() ? weights.Average() : 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Animals.AnyAsync(a => a.Id == id);
        }

        public async Task<int> CountAsync()
        {
            return await _context.Animals.CountAsync();
        }

        public async Task<Animal?> GetByIdWithLocationAsync(int id)
        {
            return await _context.Animals
                .AsNoTracking()
                .Include(a => a.Farm)
                    .ThenInclude(f => f.BoundaryCoordinates)
                .Include(a => a.LocationHistories.OrderByDescending(lh => lh.Timestamp).Take(1))
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Animal?> GetAnimalWithTrackerAsync(int animalId)
        {
            return await _context.Animals
                .AsNoTracking() // Force fresh data from database, not cached
                .Include(a => a.Tracker) // Include the Tracker navigation property
                .FirstOrDefaultAsync(a => a.Id == animalId);
        }
    }
}
