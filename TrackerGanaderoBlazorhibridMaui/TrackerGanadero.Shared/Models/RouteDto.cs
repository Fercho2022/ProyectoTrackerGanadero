namespace TrackerGanadero.Shared.Models
{
    /// <summary>
    /// DTO representing a calculated route between two points
    /// </summary>
    public class RouteDto
    {
        /// <summary>
        /// List of coordinates forming the route path
        /// </summary>
        public List<CoordinateDto> Coordinates { get; set; } = new();

        /// <summary>
        /// Total distance of the route in meters
        /// </summary>
        public double DistanceMeters { get; set; }

        /// <summary>
        /// Estimated duration in seconds
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Starting point of the route
        /// </summary>
        public CoordinateDto StartPoint { get; set; } = new();

        /// <summary>
        /// Ending point of the route (destination)
        /// </summary>
        public CoordinateDto EndPoint { get; set; } = new();

        /// <summary>
        /// Name of the destination animal (optional)
        /// </summary>
        public string? AnimalName { get; set; }
        public double? DistanceToNextTurn { get; set; }
    }

    /// <summary>
    /// Simple coordinate with latitude and longitude
    /// </summary>
    public class CoordinateDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
