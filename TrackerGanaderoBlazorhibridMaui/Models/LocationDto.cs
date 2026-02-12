namespace TrackerGanaderoBlazorHibridMaui.Models
{
    public class LocationDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
        public double? Accuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public double Altitude { get; set; }
        public int ActivityLevel { get; set; }
        public double Temperature { get; set; }
    }
}