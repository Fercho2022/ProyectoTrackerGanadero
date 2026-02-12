namespace ApiWebTrackerGanado.Dtos
{
    public class PastureUsageDto
    {
        public int PastureId { get; set; }
        public string PastureName { get; set; } = string.Empty;
        public int AnimalCount { get; set; }
        public TimeSpan AverageUsageTime { get; set; }
        public DateTime LastUsed { get; set; }
    }
}
