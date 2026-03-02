using TrackerGanadero.Shared.Services;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    public class MauiGeolocationService : IGeolocationService
    {
        public async Task<GeoLocationResult?> GetCurrentLocationAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                    {
                        return null;
                    }
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);

                if (location == null)
                    return null;

                return new GeoLocationResult
                {
                    Latitude = location.Latitude,
                    Longitude = location.Longitude
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting MAUI location: {ex.Message}");
                return null;
            }
        }
    }
}
