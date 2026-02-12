using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class PastureUsageRepository : IPastureUsageRepository
    {
        private readonly CattleTrackingContext _context;

        public PastureUsageRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<PastureUsage?> GetByIdAsync(int id)
        {
            return await _context.PastureUsages.FindAsync(id);
        }

        public async Task<IEnumerable<PastureUsage>> GetAllAsync()
        {
            return await _context.PastureUsages.ToListAsync();
        }

        public async Task<PastureUsage> AddAsync(PastureUsage pastureUsage)
        {
            _context.PastureUsages.Add(pastureUsage);
            await _context.SaveChangesAsync();
            return pastureUsage;
        }

        public async Task UpdateAsync(PastureUsage pastureUsage)
        {
            _context.PastureUsages.Update(pastureUsage);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(PastureUsage pastureUsage)
        {
            _context.PastureUsages.Remove(pastureUsage);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<PastureUsage>> GetUsageByFarmAsync(int farmId, DateTime from, DateTime to)
        {
            return await _context.PastureUsages
                .Include(pu => pu.Pasture)
                .Include(pu => pu.Animal)
                .Where(pu => pu.Pasture.FarmId == farmId &&
                            pu.StartTime >= from &&
                            pu.StartTime <= to)
                .ToListAsync();
        }

        public async Task<IEnumerable<PastureUsage>> GetActivePastureUsageAsync(int farmId)
        {
            return await _context.PastureUsages
                .Include(pu => pu.Pasture)
                .Include(pu => pu.Animal)
                .Where(pu => pu.Pasture.FarmId == farmId && pu.EndTime == null)
                .ToListAsync();
        }

        public async Task<IEnumerable<PastureUsage>> GetUsageByPastureAsync(int pastureId, DateTime from, DateTime to)
        {
            return await _context.PastureUsages
                .Include(pu => pu.Animal)
                .Where(pu => pu.PastureId == pastureId &&
                            pu.StartTime >= from &&
                            pu.StartTime <= to)
                .ToListAsync();
        }

        public async Task<PastureUsage?> GetActiveUsageAsync(int animalId, int pastureId)
        {
            return await _context.PastureUsages
                .FirstOrDefaultAsync(pu => pu.AnimalId == animalId &&
                                          pu.PastureId == pastureId &&
                                          pu.EndTime == null);
        }

        public async Task<IEnumerable<PastureUsage>> FindAsync(System.Linq.Expressions.Expression<Func<PastureUsage, bool>> predicate)
        {
            return await _context.PastureUsages.Where(predicate).ToListAsync();
        }
    }
}
