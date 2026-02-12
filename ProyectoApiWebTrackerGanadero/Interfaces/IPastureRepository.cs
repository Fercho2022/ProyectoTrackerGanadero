using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IPastureRepository
    {
        Task<Pasture?> GetByIdAsync(int id);
        Task<IEnumerable<Pasture>> GetAllAsync();
        Task<Pasture> AddAsync(Pasture pasture);
        Task UpdateAsync(Pasture pasture);
        Task DeleteAsync(Pasture pasture);
        Task<IEnumerable<Pasture>> GetPasturesByFarmAsync(int farmId);
        Task<IEnumerable<Pasture>> GetActivePasturesAsync(int farmId);
        Task<Pasture?> GetPastureWithUsageAsync(int id);
    }
}
