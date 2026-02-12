using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class BreedingRecordRepository : IBreedingRecordRepository
    {
        private readonly CattleTrackingContext _context;

        public BreedingRecordRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<BreedingRecord?> GetByIdAsync(int id)
        {
            return await _context.BreedingRecords.FindAsync(id);
        }

        public async Task<IEnumerable<BreedingRecord>> GetAllAsync()
        {
            return await _context.BreedingRecords.ToListAsync();
        }

        public async Task<BreedingRecord> AddAsync(BreedingRecord breedingRecord)
        {
            _context.BreedingRecords.Add(breedingRecord);
            await _context.SaveChangesAsync();
            return breedingRecord;
        }

        public async Task UpdateAsync(BreedingRecord breedingRecord)
        {
            _context.BreedingRecords.Update(breedingRecord);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(BreedingRecord breedingRecord)
        {
            _context.BreedingRecords.Remove(breedingRecord);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<BreedingRecord>> GetAnimalBreedingHistoryAsync(int animalId)
        {
            return await _context.BreedingRecords
                .Where(br => br.AnimalId == animalId)
                .OrderByDescending(br => br.EventDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<BreedingRecord>> GetExpectedBirthsAsync(int farmId, DateTime from, DateTime to)
        {
            return await _context.BreedingRecords
                .Include(br => br.Animal)
                .Where(br => br.Animal.FarmId == farmId &&
                            br.ExpectedBirthDate.HasValue &&
                            br.ExpectedBirthDate.Value >= from &&
                            br.ExpectedBirthDate.Value <= to)
                .OrderBy(br => br.ExpectedBirthDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<BreedingRecord>> GetRecentHeatEventsAsync(int farmId, int daysBack = 7)
        {
            var cutoffDate = DateTime.Today.AddDays(-daysBack);
            return await _context.BreedingRecords
                .Include(br => br.Animal)
                .Where(br => br.Animal.FarmId == farmId &&
                            br.EventType == "Heat" &&
                            br.EventDate >= cutoffDate)
                .OrderByDescending(br => br.EventDate)
                .ToListAsync();
        }
    }
}
