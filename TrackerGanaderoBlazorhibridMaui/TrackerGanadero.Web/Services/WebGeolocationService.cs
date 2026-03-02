using Microsoft.JSInterop;
using TrackerGanadero.Shared.Services;

namespace TrackerGanadero.Web.Services
{
    public class WebGeolocationService : IGeolocationService
    {
        private readonly IJSRuntime _jsRuntime;

        public WebGeolocationService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<GeoLocationResult?> GetCurrentLocationAsync()
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<GeoLocationResult?>("eval", @"
                    new Promise((resolve, reject) => {
                        if (!navigator.geolocation) {
                            resolve(null);
                            return;
                        }
                        navigator.geolocation.getCurrentPosition(
                            pos => resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }),
                            err => resolve(null),
                            { timeout: 10000, enableHighAccuracy: false }
                        );
                    })
                ");
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
