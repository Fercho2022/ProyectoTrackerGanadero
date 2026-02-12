using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class PastureRepository : IPastureRepository
    {
        private readonly CattleTrackingContext _context;

        public PastureRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<Pasture?> GetByIdAsync(int id)
        {
            return await _context.Pastures.FindAsync(id);
        }

        public async Task<IEnumerable<Pasture>> GetAllAsync()
        {
            return await _context.Pastures.ToListAsync();
        }

        public async Task<Pasture> AddAsync(Pasture pasture)
        {
            _context.Pastures.Add(pasture);
            await _context.SaveChangesAsync();
            return pasture;
        }

        public async Task UpdateAsync(Pasture pasture)
        {
            _context.Pastures.Update(pasture);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Pasture pasture)
        {
            _context.Pastures.Remove(pasture);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Pasture>> GetPasturesByFarmAsync(int farmId)
        {
            return await _context.Pastures
                .Where(p => p.FarmId == farmId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Pasture>> GetActivePasturesAsync(int farmId)
        {
            return await _context.Pastures
                .Where(p => p.FarmId == farmId && p.IsActive)
                .ToListAsync();
        }

        public async Task<Pasture?> GetPastureWithUsageAsync(int id)
        {
            return await _context.Pastures
                .Where(p => p.Id == id)
                .Include(p => p.PastureUsages)
                .ThenInclude(pu => pu.Animal)
                .FirstOrDefaultAsync();
        }
    }
}
