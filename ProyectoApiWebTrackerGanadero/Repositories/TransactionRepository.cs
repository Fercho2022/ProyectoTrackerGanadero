using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly CattleTrackingContext _context;

        public TransactionRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<Transaction?> GetByIdAsync(int id)
        {
            return await _context.Transactions.FindAsync(id);
        }

        public async Task<IEnumerable<Transaction>> GetAllAsync()
        {
            return await _context.Transactions.ToListAsync();
        }

        public async Task<Transaction> AddAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task UpdateAsync(Transaction transaction)
        {
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Transaction transaction)
        {
            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByFarmAsync(int farmId, string? type = null)
        {
            var query = _context.Transactions
                .Include(t => t.Animal)
                .Where(t => t.Animal.FarmId == farmId);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(t => t.Type == type);

            return await query
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByPeriodAsync(int farmId, DateTime from, DateTime to)
        {
            return await _context.Transactions
                .Include(t => t.Animal)
                .Where(t => t.Animal.FarmId == farmId &&
                           t.TransactionDate >= from &&
                           t.TransactionDate <= to)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalSalesAsync(int farmId, DateTime from, DateTime to)
        {
            return await _context.Transactions
                .Include(t => t.Animal)
                .Where(t => t.Animal.FarmId == farmId &&
                           t.Type == "Sale" &&
                           t.TransactionDate >= from &&
                           t.TransactionDate <= to)
                .SumAsync(t => t.Amount);
        }

        public async Task<decimal> GetTotalPurchasesAsync(int farmId, DateTime from, DateTime to)
        {
            return await _context.Transactions
                .Include(t => t.Animal)
                .Where(t => t.Animal.FarmId == farmId &&
                           t.Type == "Purchase" &&
                           t.TransactionDate >= from &&
                           t.TransactionDate <= to)
                .SumAsync(t => t.Amount);
        }

        public async Task<decimal> GetProfitAsync(int farmId, DateTime from, DateTime to)
        {
            var sales = await GetTotalSalesAsync(farmId, from, to);
            var purchases = await GetTotalPurchasesAsync(farmId, from, to);
            return sales - purchases;
        }
    }
}
