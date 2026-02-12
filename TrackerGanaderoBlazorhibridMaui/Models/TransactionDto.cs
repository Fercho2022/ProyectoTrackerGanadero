namespace TrackerGanaderoBlazorHibridMaui.Models
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // "purchase", "sale"
        public int? AnimalId { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? BuyerSeller { get; set; }
        public string? Description { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal? Weight { get; set; }
        public decimal? PricePerKg { get; set; }
        public int FarmId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AnimalName { get; set; }
        public string? FarmName { get; set; }
    }
}