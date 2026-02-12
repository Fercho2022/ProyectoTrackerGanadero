using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IPastureUsageRepository
    {
        Task<PastureUsage?> GetByIdAsync(int id);
        Task<IEnumerable<PastureUsage>> GetAllAsync();
        Task<PastureUsage> AddAsync(PastureUsage pastureUsage);
        Task UpdateAsync(PastureUsage pastureUsage);
        Task DeleteAsync(PastureUsage pastureUsage);
        Task<IEnumerable<PastureUsage>> GetUsageByFarmAsync(int farmId, DateTime from, DateTime to);
        Task<IEnumerable<PastureUsage>> GetActivePastureUsageAsync(int farmId);
        Task<IEnumerable<PastureUsage>> GetUsageByPastureAsync(int pastureId, DateTime from, DateTime to);
        Task<PastureUsage?> GetActiveUsageAsync(int animalId, int pastureId);
        Task<IEnumerable<PastureUsage>> FindAsync(System.Linq.Expressions.Expression<Func<PastureUsage, bool>> predicate);
    }
}
