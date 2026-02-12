using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface ITransactionRepository
    {
        Task<Transaction?> GetByIdAsync(int id);
        Task<IEnumerable<Transaction>> GetAllAsync();
        Task<Transaction> AddAsync(Transaction transaction);
        Task UpdateAsync(Transaction transaction);
        Task DeleteAsync(Transaction transaction);
        Task<IEnumerable<Transaction>> GetTransactionsByFarmAsync(int farmId, string? type = null);
        Task<IEnumerable<Transaction>> GetTransactionsByPeriodAsync(int farmId, DateTime from, DateTime to);
        Task<decimal> GetTotalSalesAsync(int farmId, DateTime from, DateTime to);
        Task<decimal> GetTotalPurchasesAsync(int farmId, DateTime from, DateTime to);
        Task<decimal> GetProfitAsync(int farmId, DateTime from, DateTime to);
    }
}
