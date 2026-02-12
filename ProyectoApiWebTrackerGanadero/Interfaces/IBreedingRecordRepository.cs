using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IBreedingRecordRepository
    {
        Task<BreedingRecord?> GetByIdAsync(int id);
        Task<IEnumerable<BreedingRecord>> GetAllAsync();
        Task<BreedingRecord> AddAsync(BreedingRecord breedingRecord);
        Task UpdateAsync(BreedingRecord breedingRecord);
        Task DeleteAsync(BreedingRecord breedingRecord);
        Task<IEnumerable<BreedingRecord>> GetAnimalBreedingHistoryAsync(int animalId);
        Task<IEnumerable<BreedingRecord>> GetExpectedBirthsAsync(int farmId, DateTime from, DateTime to);
        Task<IEnumerable<BreedingRecord>> GetRecentHeatEventsAsync(int farmId, int daysBack = 7);
    }
}
