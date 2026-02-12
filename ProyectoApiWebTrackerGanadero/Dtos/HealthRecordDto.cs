namespace ApiWebTrackerGanado.Dtos
{
    public class HealthRecordDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string RecordType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime RecordDate { get; set; }
        public string? VeterinarianName { get; set; }
        public string? Treatment { get; set; }
        public string? Medication { get; set; }
        public decimal? Cost { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AnimalName { get; set; }
        public string? AnimalTag { get; set; }
        public string? FarmName { get; set; }
        public DateTime? NextCheckup { get; set; }
    }
}
