namespace TrackerGanadero.Shared.Services
{
    public class GeoLocationResult
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public interface IGeolocationService
    {
        Task<GeoLocationResult?> GetCurrentLocationAsync();
    }
}
