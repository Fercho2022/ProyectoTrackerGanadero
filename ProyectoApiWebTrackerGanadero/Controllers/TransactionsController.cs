using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IMapper _mapper;

        public TransactionsController(ITransactionRepository transactionRepository, IMapper mapper)
        {
            _transactionRepository = transactionRepository;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions(
            [FromQuery] int farmId,
            [FromQuery] string? type = null)
        {
            var transactions = await _transactionRepository.GetTransactionsByFarmAsync(farmId, type);
            return Ok(transactions);
        }

        [HttpPost]
        public async Task<ActionResult<Transaction>> CreateTransaction([FromBody] Transaction transaction)
        {
            transaction.CreatedAt = DateTime.UtcNow;
            await _transactionRepository.AddAsync(transaction);

            return CreatedAtAction(nameof(GetTransactions), new { farmId = 0 }, transaction);
        }

        [HttpGet("summary")]
        public async Task<ActionResult> GetFinancialSummary(
            [FromQuery] int farmId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            var sales = await _transactionRepository.GetTotalSalesAsync(farmId, from, to);
            var purchases = await _transactionRepository.GetTotalPurchasesAsync(farmId, from, to);
            var profit = await _transactionRepository.GetProfitAsync(farmId, from, to);

            var transactions = await _transactionRepository.GetTransactionsByPeriodAsync(farmId, from, to);

            var summary = new
            {
                TotalSales = sales,
                TotalPurchases = purchases,
                NetProfit = profit,
                TransactionCount = transactions.Count(),
                Period = new { From = from, To = to }
            };

            return Ok(summary);
        }
    }
}
