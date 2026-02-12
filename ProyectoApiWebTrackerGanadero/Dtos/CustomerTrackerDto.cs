namespace ApiWebTrackerGanado.Dtos
{
    public class CustomerTrackerDto
    {
        public int Id { get; set; }
        public int TrackerId { get; set; }
        public string? TrackerName { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public string? AnimalName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
    }
}
