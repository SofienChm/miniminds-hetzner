namespace DaycareAPI.Services
{
    public interface IGeofenceService
    {
        bool IsWithinRadius(double lat1, double lon1, double lat2, double lon2, int radiusMeters);
        double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
    }

    public class GeofenceService : IGeofenceService
    {
        private const double EarthRadiusKm = 6371.0;

        /// <summary>
        /// Checks if two coordinates are within a specified radius using the Haversine formula
        /// </summary>
        /// <param name="lat1">Latitude of first point (user location)</param>
        /// <param name="lon1">Longitude of first point (user location)</param>
        /// <param name="lat2">Latitude of second point (school location)</param>
        /// <param name="lon2">Longitude of second point (school location)</param>
        /// <param name="radiusMeters">Maximum allowed distance in meters</param>
        /// <returns>True if within radius, false otherwise</returns>
        public bool IsWithinRadius(double lat1, double lon1, double lat2, double lon2, int radiusMeters)
        {
            var distanceMeters = CalculateDistance(lat1, lon1, lat2, lon2);
            return distanceMeters <= radiusMeters;
        }

        /// <summary>
        /// Calculates the distance between two coordinates using the Haversine formula
        /// </summary>
        /// <returns>Distance in meters</returns>
        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            var distanceKm = EarthRadiusKm * c;
            return distanceKm * 1000; // Convert to meters
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
