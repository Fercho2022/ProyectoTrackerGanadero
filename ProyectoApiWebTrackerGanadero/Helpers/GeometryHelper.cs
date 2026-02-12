using ApiWebTrackerGanado.Dtos;
using NetTopologySuite.Geometries;
using NetTopologySuite;

namespace ApiWebTrackerGanado.Helpers
{
    public static class GeometryHelper
    {
        private static readonly GeometryFactory _geometryFactory =
            NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326); // WGS84

        public static Point CreatePoint(double longitude, double latitude)
        {
            return _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
        }

        public static Polygon CreatePolygonFromCoordinates(double[][] coordinates)
        {
            var coords = coordinates.Select(coord => new Coordinate(coord[0], coord[1])).ToArray();

            if (!coords.First().Equals(coords.Last()))
            {
                var closedCoords = new Coordinate[coords.Length + 1];
                Array.Copy(coords, closedCoords, coords.Length);
                closedCoords[coords.Length] = coords[0];
                coords = closedCoords;
            }

            return _geometryFactory.CreatePolygon(coords);
        }

        public static Polygon CreateRectangle(double minLng, double minLat, double maxLng, double maxLat)
        {
            var coordinates = new[]
            {
                new Coordinate(minLng, minLat),
                new Coordinate(maxLng, minLat),
                new Coordinate(maxLng, maxLat),
                new Coordinate(minLng, maxLat),
                new Coordinate(minLng, minLat)
            };

            return _geometryFactory.CreatePolygon(coordinates);
        }

        public static bool IsPointInPolygon(Polygon polygon, double longitude, double latitude)
        {
            var point = CreatePoint(longitude, latitude);
            return polygon.Contains(point);
        }

        public static double CalculatePolygonArea(Polygon polygon)
        {
            return polygon.Area;
        }

        public static Polygon CreatePolygonFromGoogleMapsCoordinates(List<LatLngDto> coordinates)
        {
            var coords = coordinates.Select(coord => new Coordinate(coord.Lng, coord.Lat)).ToList();

            if (!coords.First().Equals(coords.Last()))
            {
                coords.Add(coords.First());
            }

            return _geometryFactory.CreatePolygon(coords.ToArray());
        }

        public static List<LatLngDto> ConvertPolygonToLatLng(Polygon polygon)
        {
            return polygon.Coordinates
                .Select(coord => new LatLngDto { Lat = coord.Y, Lng = coord.X })
                .ToList();
        }
    }
}
