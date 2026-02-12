using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IHealthRecordRepository
    {
        Task<HealthRecord?> GetByIdAsync(int id);
        Task<IEnumerable<HealthRecord>> GetAllAsync();
        Task<HealthRecord> AddAsync(HealthRecord healthRecord);
        Task UpdateAsync(HealthRecord healthRecord);
        Task DeleteAsync(HealthRecord healthRecord);
        Task<IEnumerable<HealthRecord>> GetAnimalHealthRecordsAsync(int animalId);
        Task<IEnumerable<HealthRecord>> GetUpcomingCheckupsAsync(int farmId, int daysAhead = 30);
        Task<IEnumerable<HealthRecord>> GetTreatmentsByTypeAsync(int farmId, string treatment);
        Task<decimal> GetHealthCostsByFarmAsync(int farmId, DateTime from, DateTime to);
    }
}
