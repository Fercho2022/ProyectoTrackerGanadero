using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        [StringLength(20)]
        public string Type { get; set; } = string.Empty;

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        public decimal Amount { get; set; }

        [StringLength(200)]
        public string? CustomerSupplier { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime TransactionDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
