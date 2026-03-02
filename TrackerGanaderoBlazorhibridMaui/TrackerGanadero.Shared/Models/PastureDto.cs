namespace TrackerGanadero.Shared.Models
{
    public class PastureDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Area { get; set; }
        public int FarmId { get; set; }
        public List<LatLngDto> Boundaries { get; set; } = new();
        public string GrassType { get; set; } = string.Empty;
        public int MaxCapacity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PastureUsageDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public int PastureId { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public TimeSpan? Duration => ExitTime?.Subtract(EntryTime);
        public string? AnimalName { get; set; }
        public string? PastureName { get; set; }
    }
}