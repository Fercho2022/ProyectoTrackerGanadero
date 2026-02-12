namespace ApiWebTrackerGanado.Dtos
{
    public class AlertDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? AnimalId { get; set; }
        public int? FarmId { get; set; }
        public string AnimalName { get; set; } = string.Empty;
        public string? FarmName { get; set; }
        public bool IsRead { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
