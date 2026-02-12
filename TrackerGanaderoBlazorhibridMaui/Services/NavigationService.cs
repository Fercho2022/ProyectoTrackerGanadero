using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using TrackerGanaderoBlazorHibridMaui.Models;
using System.Globalization;

namespace TrackerGanaderoBlazorHibridMaui.Services
{
    /// <summary>
    /// Service for GPS navigation and route calculation
    /// </summary>
    public class NavigationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        // ⚙️ DESARROLLO: Activar para usar ubicación de prueba (desactivar en producción)
        public const bool USE_MOCK_LOCATION = true;

        // 🚶 SIMULACIÓN: Activar para simular movimiento del usuario siguiendo la ruta
        public static bool SIMULATE_USER_MOVEMENT = true;

        // 📍 Ubicación de prueba: Gualeguaychú, Entre Ríos, Argentina (cerca de tus animales)
        // Cambiadas a static para permitir actualización durante simulación
        private static double _mockLatitude = -33.0095;  // Gualeguaychú, Entre Ríos
        private static double _mockLongitude = -58.5173;

        /// <summary>
        /// Updates the mock GPS location (used for movement simulation)
        /// </summary>
        public static void UpdateMockLocation(double latitude, double longitude)
        {
            _mockLatitude = latitude;
            _mockLongitude = longitude;
            System.Diagnostics.Debug.WriteLine($"🚶 Mock location updated: ({latitude}, {longitude})");
        }

        public NavigationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _apiKey = _configuration["ApiSettings:GraphHopperApiKey"] ?? "";
            _baseUrl = _configuration["ApiSettings:GraphHopperBaseUrl"] ?? "https://graphhopper.com/api/1";
        }

        /// <summary>
        /// Gets the user's current GPS location
        /// </summary>
        /// <returns>Location object with coordinates, or null if unavailable</returns>
        public async Task<Location?> GetUserLocationAsync()
        {
            try
            {
                // 🔧 MODO DESARROLLO: Usar ubicación de prueba
                if (USE_MOCK_LOCATION)
                {
                    System.Diagnostics.Debug.WriteLine($"🔧 [DEV MODE] Using mock GPS location: ({_mockLatitude}, {_mockLongitude})");
                    return new Location
                    {
                        Latitude = _mockLatitude,
                        Longitude = _mockLongitude,
                        Accuracy = 10.0,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }

                // Check and request permissions
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                    {
                        throw new PermissionException("Location permission denied");
                    }
                }

                // Get location with medium accuracy (battery-friendly)
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);

                return location;
            }
            catch (FeatureNotSupportedException)
            {
                // Device doesn't support GPS
                System.Diagnostics.Debug.WriteLine("GPS not supported on this device");
                return null;
            }
            catch (PermissionException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Permission denied: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting location: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Calculates route between two points using GraphHopper API
        /// </summary>
        /// <param name="start">Starting coordinate (user location)</param>
        /// <param name="end">Destination coordinate (animal location)</param>
        /// <returns>RouteDto with path coordinates and metadata, or null if calculation fails</returns>
        public async Task<RouteDto?> CalculateRouteAsync(CoordinateDto start, CoordinateDto end)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🚀 CalculateRouteAsync called - Start: ({start.Latitude}, {start.Longitude}), End: ({end.Latitude}, {end.Longitude})");

                // Validate API key
                if (string.IsNullOrEmpty(_apiKey))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ GraphHopper API key not configured");
                    return null;
                }

                // Build request URL with query parameters
                // IMPORTANT: Use InvariantCulture to ensure decimal point (not comma) in coordinates
                var url = $"{_baseUrl}/route?" +
                    $"point={start.Latitude.ToString(CultureInfo.InvariantCulture)},{start.Longitude.ToString(CultureInfo.InvariantCulture)}&" +
                    $"point={end.Latitude.ToString(CultureInfo.InvariantCulture)},{end.Longitude.ToString(CultureInfo.InvariantCulture)}&" +
                    $"vehicle=foot&" +  // Cambio a "foot" para zonas rurales
                    $"locale=es&" +
                    $"points_encoded=false&" +
                    $"key={_apiKey}";

                System.Diagnostics.Debug.WriteLine($"📡 Calling GraphHopper API: {url.Replace(_apiKey, "***")}");

                // Send request with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"❌ GraphHopper API error: {response.StatusCode} - {error}");
                    System.Diagnostics.Debug.WriteLine("🔄 Fallback: Calculando ruta en línea recta...");
                    return CalculateStraightLineRoute(start, end);
                }

                // Parse response
                var result = await response.Content.ReadFromJsonAsync<GraphHopperRouteResponse>();
                if (result?.paths == null || result.paths.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No routes found in response");
                    System.Diagnostics.Debug.WriteLine("🔄 Fallback: Calculando ruta en línea recta...");
                    return CalculateStraightLineRoute(start, end);
                }

                var path = result.paths[0];

                // Convert to our DTO format
                // GraphHopper returns [lon, lat] but we need to convert to our format
                var routeDto = new RouteDto
                {
                    Coordinates = path.points.coordinates
                        .Select(c => new CoordinateDto
                        {
                            Longitude = c[0],
                            Latitude = c[1]
                        })
                        .ToList(),
                    DistanceMeters = path.distance,
                    DurationSeconds = path.time / 1000.0, // GraphHopper returns milliseconds
                    StartPoint = start,
                    EndPoint = end
                };

                System.Diagnostics.Debug.WriteLine($"✅ Route calculated: {routeDto.DistanceMeters}m, {routeDto.DurationSeconds}s");
                return routeDto;
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("⏱️ Route calculation timed out (>30 seconds)");
                System.Diagnostics.Debug.WriteLine("🔄 Fallback: Calculando ruta en línea recta...");
                return CalculateStraightLineRoute(start, end);
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"🌐 Network error calculating route: {ex.Message}");
                System.Diagnostics.Debug.WriteLine("🔄 Fallback: Calculando ruta en línea recta...");
                return CalculateStraightLineRoute(start, end);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error calculating route: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("🔄 Fallback: Calculando ruta en línea recta...");
                return CalculateStraightLineRoute(start, end);
            }
        }

        /// <summary>
        /// Calculates a straight-line route between two points (fallback when road routing fails)
        /// </summary>
        private RouteDto CalculateStraightLineRoute(CoordinateDto start, CoordinateDto end)
        {
            System.Diagnostics.Debug.WriteLine($"📏 Calculating straight-line route from ({start.Latitude}, {start.Longitude}) to ({end.Latitude}, {end.Longitude})");

            // Calculate distance using Haversine formula
            var distance = CalculateDistance(start.Latitude, start.Longitude, end.Latitude, end.Longitude);

            // Estimate duration: assuming 3 km/h walking speed in rural areas
            var duration = (distance / 1000.0) / 3.0 * 3600.0; // seconds

            // Create a simple straight line with 10 intermediate points for smooth animation
            var coordinates = new List<CoordinateDto>();
            var steps = 10;
            for (int i = 0; i <= steps; i++)
            {
                var ratio = (double)i / steps;
                coordinates.Add(new CoordinateDto
                {
                    Latitude = start.Latitude + (end.Latitude - start.Latitude) * ratio,
                    Longitude = start.Longitude + (end.Longitude - start.Longitude) * ratio
                });
            }

            var route = new RouteDto
            {
                Coordinates = coordinates,
                DistanceMeters = distance,
                DurationSeconds = duration,
                StartPoint = start,
                EndPoint = end
            };

            System.Diagnostics.Debug.WriteLine($"✅ Straight-line route: {distance:F1}m, {duration:F0}s");
            return route;
        }

        /// <summary>
        /// Calculates distance between two coordinates using Haversine formula
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth radius in meters
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        // GraphHopper API Response Models
        private class GraphHopperRouteResponse
        {
            public GraphHopperPath[]? paths { get; set; }
        }

        private class GraphHopperPath
        {
            public double distance { get; set; } // meters
            public long time { get; set; } // milliseconds
            public GraphHopperPoints points { get; set; } = new();
        }

        private class GraphHopperPoints
        {
            public double[][] coordinates { get; set; } = Array.Empty<double[]>();
        }
    }
}
