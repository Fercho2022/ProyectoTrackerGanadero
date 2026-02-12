namespace ApiWebTrackerGanado.Models
{
    public class PastureUsage
    {
        public int Id { get; set; }

        public int PastureId { get; set; }
        public Pasture Pasture { get; set; } = null!;

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? DateTime.UtcNow.Subtract(StartTime);
    }
}

