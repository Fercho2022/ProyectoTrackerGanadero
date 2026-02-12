using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class HealthRecordRepository : IHealthRecordRepository
    {
        private readonly CattleTrackingContext _context;

        public HealthRecordRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<HealthRecord?> GetByIdAsync(int id)
        {
            return await _context.HealthRecords
                .Include(hr => hr.Animal)
                    .ThenInclude(a => a.Farm)
                .FirstOrDefaultAsync(hr => hr.Id == id);
        }

        public async Task<IEnumerable<HealthRecord>> GetAllAsync()
        {
            return await _context.HealthRecords
                .Include(hr => hr.Animal)
                    .ThenInclude(a => a.Farm)
                .OrderByDescending(hr => hr.RecordDate)
                .ToListAsync();
        }

        public async Task<HealthRecord> AddAsync(HealthRecord healthRecord)
        {
            _context.HealthRecords.Add(healthRecord);
            await _context.SaveChangesAsync();
            return healthRecord;
        }

        public async Task UpdateAsync(HealthRecord healthRecord)
        {
            _context.HealthRecords.Update(healthRecord);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(HealthRecord healthRecord)
        {
            _context.HealthRecords.Remove(healthRecord);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<HealthRecord>> GetAnimalHealthRecordsAsync(int animalId)
        {
            return await _context.HealthRecords
                .Include(hr => hr.Animal)
                    .ThenInclude(a => a.Farm)
                .Where(hr => hr.AnimalId == animalId)
                .OrderByDescending(hr => hr.RecordDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<HealthRecord>> GetUpcomingCheckupsAsync(int farmId, int daysAhead = 30)
        {
            var endDate = DateTime.Today.AddDays(daysAhead);
            return await _context.HealthRecords
                .Include(hr => hr.Animal)
                .Where(hr => hr.Animal.FarmId == farmId &&
                            hr.NextCheckup.HasValue &&
                            hr.NextCheckup.Value >= DateTime.Today &&
                            hr.NextCheckup.Value <= endDate)
                .OrderBy(hr => hr.NextCheckup)
                .ToListAsync();
        }

        public async Task<IEnumerable<HealthRecord>> GetTreatmentsByTypeAsync(int farmId, string treatment)
        {
            return await _context.HealthRecords
                .Include(hr => hr.Animal)
                .Where(hr => hr.Animal.FarmId == farmId && hr.Treatment == treatment)
                .OrderByDescending(hr => hr.RecordDate)
                .ToListAsync();
        }

        public async Task<decimal> GetHealthCostsByFarmAsync(int farmId, DateTime from, DateTime to)
        {
            return await _context.HealthRecords
                .Include(hr => hr.Animal)
                .Where(hr => hr.Animal.FarmId == farmId &&
                            hr.RecordDate >= from &&
                            hr.RecordDate <= to &&
                            hr.Cost.HasValue)
                .SumAsync(hr => hr.Cost!.Value);
        }
    }
}
