namespace ApiWebTrackerGanado.Dtos
{
    public class LocationDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public int ActivityLevel { get; set; }
        public double Temperature { get; set; }
        public DateTime Timestamp { get; set; }
        public bool HasSignal { get; set; } = true; // New property
    }
}
