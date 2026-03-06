using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using TrackerGanadero.Shared.Models;
using System.Globalization;

namespace TrackerGanadero.Shared.Services
{
    public class NavigationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IGeolocationService _geolocationService;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public const bool USE_MOCK_LOCATION = true;

        public static bool SIMULATE_USER_MOVEMENT = true;

        // Mock location dentro del área de geofencing de Gualeguaychú (emulador_gps_stress_test.py)
        private static double _mockLatitude = -33.030;
        private static double _mockLongitude = -60.470;

        public static void UpdateMockLocation(double latitude, double longitude)
        {
            _mockLatitude = latitude;
            _mockLongitude = longitude;
            System.Diagnostics.Debug.WriteLine($"Mock location updated: ({latitude}, {longitude})");
        }

        public NavigationService(HttpClient httpClient, IConfiguration configuration, IGeolocationService geolocationService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _geolocationService = geolocationService;
            _apiKey = _configuration["ApiSettings:GraphHopperApiKey"] ?? "";
            _baseUrl = _configuration["ApiSettings:GraphHopperBaseUrl"] ?? "https://graphhopper.com/api/1";
        }

        public async Task<GeoLocationResult?> GetUserLocationAsync()
        {
            try
            {
                if (USE_MOCK_LOCATION)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEV MODE] Using mock GPS location: ({_mockLatitude}, {_mockLongitude})");
                    return new GeoLocationResult
                    {
                        Latitude = _mockLatitude,
                        Longitude = _mockLongitude
                    };
                }

                return await _geolocationService.GetCurrentLocationAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting location: {ex.Message}");
                return null;
            }
        }

        public async Task<RouteDto?> CalculateRouteAsync(CoordinateDto start, CoordinateDto end)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CalculateRouteAsync called - Start: ({start.Latitude}, {start.Longitude}), End: ({end.Latitude}, {end.Longitude})");

                if (string.IsNullOrEmpty(_apiKey))
                {
                    System.Diagnostics.Debug.WriteLine("GraphHopper API key not configured");
                    return null;
                }

                var url = $"{_baseUrl}/route?" +
                    $"point={start.Latitude.ToString(CultureInfo.InvariantCulture)},{start.Longitude.ToString(CultureInfo.InvariantCulture)}&" +
                    $"point={end.Latitude.ToString(CultureInfo.InvariantCulture)},{end.Longitude.ToString(CultureInfo.InvariantCulture)}&" +
                    $"vehicle=foot&" +
                    $"locale=es&" +
                    $"points_encoded=false&" +
                    $"key={_apiKey}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"GraphHopper API error: {response.StatusCode} - {error}");
                    return CalculateStraightLineRoute(start, end);
                }

                var result = await response.Content.ReadFromJsonAsync<GraphHopperRouteResponse>();
                if (result?.paths == null || result.paths.Length == 0)
                {
                    return CalculateStraightLineRoute(start, end);
                }

                var path = result.paths[0];

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
                    DurationSeconds = path.time / 1000.0,
                    StartPoint = start,
                    EndPoint = end
                };

                System.Diagnostics.Debug.WriteLine($"Route calculated: {routeDto.DistanceMeters}m, {routeDto.DurationSeconds}s");
                return routeDto;
            }
            catch (TaskCanceledException)
            {
                return CalculateStraightLineRoute(start, end);
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network error calculating route: {ex.Message}");
                return CalculateStraightLineRoute(start, end);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating route: {ex.Message}");
                return CalculateStraightLineRoute(start, end);
            }
        }

        private RouteDto CalculateStraightLineRoute(CoordinateDto start, CoordinateDto end)
        {
            var distance = CalculateDistance(start.Latitude, start.Longitude, end.Latitude, end.Longitude);
            var duration = (distance / 1000.0) / 3.0 * 3600.0;

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

            return new RouteDto
            {
                Coordinates = coordinates,
                DistanceMeters = distance,
                DurationSeconds = duration,
                StartPoint = start,
                EndPoint = end
            };
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        private class GraphHopperRouteResponse
        {
            public GraphHopperPath[]? paths { get; set; }
        }

        private class GraphHopperPath
        {
            public double distance { get; set; }
            public long time { get; set; }
            public GraphHopperPoints points { get; set; } = new();
        }

        private class GraphHopperPoints
        {
            public double[][] coordinates { get; set; } = Array.Empty<double[]>();
        }
    }
}
