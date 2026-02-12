using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Helpers
{
    public static class GeofencingHelper
    {
        /// <summary>
        /// Determines if a point is inside a polygon using the ray casting algorithm
        /// </summary>
        /// <param name="point">The point to test (latitude, longitude)</param>
        /// <param name="polygon">List of boundary coordinates in order</param>
        /// <returns>True if the point is inside the polygon</returns>
        public static bool IsPointInPolygon(double latitude, double longitude, IEnumerable<FarmBoundary> polygon)
        {
            if (polygon == null || !polygon.Any())
                return false;

            var boundaryList = polygon.OrderBy(b => b.SequenceOrder).ToList();

            if (boundaryList.Count < 3)
                return false;

            bool inside = false;
            int j = boundaryList.Count - 1;

            for (int i = 0; i < boundaryList.Count; i++)
            {
                double xi = boundaryList[i].Latitude;
                double yi = boundaryList[i].Longitude;
                double xj = boundaryList[j].Latitude;
                double yj = boundaryList[j].Longitude;

                if (((yi > longitude) != (yj > longitude)) &&
                    (latitude < (xj - xi) * (longitude - yi) / (yj - yi) + xi))
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
        }

        /// <summary>
        /// Calculates the distance between two geographic points using the Haversine formula
        /// </summary>
        /// <param name="lat1">Latitude of first point</param>
        /// <param name="lon1">Longitude of first point</param>
        /// <param name="lat2">Latitude of second point</param>
        /// <param name="lon2">Longitude of second point</param>
        /// <returns>Distance in meters</returns>
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;
            const double toRadians = Math.PI / 180;

            double dLat = (lat2 - lat1) * toRadians;
            double dLon = (lon2 - lon1) * toRadians;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                      Math.Cos(lat1 * toRadians) * Math.Cos(lat2 * toRadians) *
                      Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c * 1000; // Convert to meters
        }

        /// <summary>
        /// Calculates the minimum distance from a point to the polygon boundary
        /// </summary>
        /// <param name="latitude">Point latitude</param>
        /// <param name="longitude">Point longitude</param>
        /// <param name="polygon">Polygon boundary coordinates</param>
        /// <returns>Minimum distance to boundary in meters</returns>
        public static double DistanceToPolygonBoundary(double latitude, double longitude, IEnumerable<FarmBoundary> polygon)
        {
            if (polygon == null || !polygon.Any())
                return double.MaxValue;

            var boundaryList = polygon.OrderBy(b => b.SequenceOrder).ToList();
            double minDistance = double.MaxValue;

            for (int i = 0; i < boundaryList.Count; i++)
            {
                int j = (i + 1) % boundaryList.Count;

                double distance = DistanceToLineSegment(
                    latitude, longitude,
                    boundaryList[i].Latitude, boundaryList[i].Longitude,
                    boundaryList[j].Latitude, boundaryList[j].Longitude);

                if (distance < minDistance)
                    minDistance = distance;
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the distance from a point to a line segment
        /// </summary>
        private static double DistanceToLineSegment(double px, double py, double ax, double ay, double bx, double by)
        {
            double A = px - ax;
            double B = py - ay;
            double C = bx - ax;
            double D = by - ay;

            double dot = A * C + B * D;
            double lenSq = C * C + D * D;
            double param = -1;

            if (lenSq != 0)
                param = dot / lenSq;

            double xx, yy;

            if (param < 0)
            {
                xx = ax;
                yy = ay;
            }
            else if (param > 1)
            {
                xx = bx;
                yy = by;
            }
            else
            {
                xx = ax + param * C;
                yy = ay + param * D;
            }

            return CalculateDistance(px, py, xx, yy);
        }
    }
}