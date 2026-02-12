using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IWeightRecordRepository
    {
        Task<WeightRecord?> GetByIdAsync(int id);
        Task<IEnumerable<WeightRecord>> GetAllAsync();
        Task<WeightRecord> AddAsync(WeightRecord weightRecord);
        Task UpdateAsync(WeightRecord weightRecord);
        Task DeleteAsync(WeightRecord weightRecord);
        Task<IEnumerable<WeightRecord>> GetAnimalWeightHistoryAsync(int animalId);
        Task<WeightRecord?> GetLatestWeightRecordAsync(int animalId);
        Task<decimal> GetAverageWeightGainAsync(int animalId, int months = 6);
    }
}
