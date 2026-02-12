using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class WeightRecordRepository : IWeightRecordRepository
    {
        private readonly CattleTrackingContext _context;

        public WeightRecordRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<WeightRecord?> GetByIdAsync(int id)
        {
            return await _context.WeightRecords.FindAsync(id);
        }

        public async Task<IEnumerable<WeightRecord>> GetAllAsync()
        {
            return await _context.WeightRecords.ToListAsync();
        }

        public async Task<WeightRecord> AddAsync(WeightRecord weightRecord)
        {
            _context.WeightRecords.Add(weightRecord);
            await _context.SaveChangesAsync();
            return weightRecord;
        }

        public async Task UpdateAsync(WeightRecord weightRecord)
        {
            _context.WeightRecords.Update(weightRecord);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(WeightRecord weightRecord)
        {
            _context.WeightRecords.Remove(weightRecord);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<WeightRecord>> GetAnimalWeightHistoryAsync(int animalId)
        {
            return await _context.WeightRecords
                .Where(wr => wr.AnimalId == animalId)
                .OrderByDescending(wr => wr.WeightDate)
                .ToListAsync();
        }

        public async Task<WeightRecord?> GetLatestWeightRecordAsync(int animalId)
        {
            return await _context.WeightRecords
                .Where(wr => wr.AnimalId == animalId)
                .OrderByDescending(wr => wr.WeightDate)
                .FirstOrDefaultAsync();
        }

        public async Task<decimal> GetAverageWeightGainAsync(int animalId, int months = 6)
        {
            var cutoffDate = DateTime.Today.AddMonths(-months);
            var weights = await _context.WeightRecords
                .Where(wr => wr.AnimalId == animalId && wr.WeightDate >= cutoffDate)
                .OrderBy(wr => wr.WeightDate)
                .Select(wr => new { wr.Weight, wr.WeightDate })
                .ToListAsync();

            if (weights.Count < 2) return 0;

            var firstWeight = weights.First();
            var lastWeight = weights.Last();
            var daysDiff = (lastWeight.WeightDate - firstWeight.WeightDate).Days;

            return daysDiff > 0 ? (lastWeight.Weight - firstWeight.Weight) / daysDiff * 30 : 0;
        }
    }
}
