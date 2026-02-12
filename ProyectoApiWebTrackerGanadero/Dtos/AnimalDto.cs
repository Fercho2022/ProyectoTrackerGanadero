namespace ApiWebTrackerGanado.Dtos
{
    public class AnimalDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public DateTime BirthDate { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string Status { get; set; } = string.Empty;
        public int FarmId { get; set; }
        public int? TrackerId { get; set; }
        public LocationDto? CurrentLocation { get; set; }
    }
}
