using System.Globalization;
using System.Text.Json;

namespace wanderer_api.Services
{
    public class GeocodingService
    {
        private readonly HttpClient _httpClient;

        private static readonly Dictionary<string, (double Lat, double Lng)> CityCenters = new()
        {
            { "bangalore", (12.9716, 77.5946) },
            { "mumbai", (19.0760, 72.8777) },
            { "delhi", (28.6139, 77.2090) },
            { "goa", (15.2993, 74.1240) },
            { "jaipur", (26.9124, 75.7873) },
            { "kolkata", (22.5726, 88.3639) },
            { "chennai", (13.0827, 80.2707) },
            { "hyderabad", (17.3850, 78.4867) },
            { "udaipur", (24.5854, 73.7125) },
            { "manali", (32.2432, 77.1892) },
        };

        private static readonly Dictionary<string, (double MinLat, double MaxLat, double MinLng, double MaxLng)> CityBounds = new()
        {
            { "bangalore", (12.7, 13.2, 77.3, 77.9) },
            { "mumbai",    (18.8, 19.3, 72.7, 73.1) },
            { "delhi",     (28.4, 28.9, 76.8, 77.5) },
            { "goa",       (14.8, 15.8, 73.6, 74.4) },
            { "jaipur",    (26.6, 27.2, 75.5, 76.1) },
            { "kolkata",   (22.3, 22.8, 88.1, 88.6) },
            { "chennai",   (12.7, 13.3, 80.0, 80.5) },
            { "hyderabad", (17.1, 17.7, 78.2, 78.8) },
            { "udaipur",   (24.4, 24.8, 73.5, 73.9) },
            { "manali",    (32.1, 32.4, 77.0, 77.4) },
        };

        public GeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Wanderer/1.0");
        }

        private bool IsWithinCityBounds(double lat, double lng, string city)
        {
            var cityKey = city.ToLower().Trim();
            if (!CityBounds.TryGetValue(cityKey, out var bounds))
                return true;

            return lat >= bounds.MinLat && lat <= bounds.MaxLat &&
                   lng >= bounds.MinLng && lng <= bounds.MaxLng;
        }

        public async Task<(double Lat, double Lng)> GeocodeAsync(string placeName, string city)
        {
            try
            {
                var cleanName = placeName
                    .Replace("'", "")
                    .Replace("\u2019", "")
                    .Replace("-", " ")
                    .Trim();

                // Attempt 1 — full name + city
                var result = await TryGeocode($"{cleanName}, {city}, India", city);
                if (result.Lat != 0) return result;

                // Attempt 2 — name + city
                result = await TryGeocode($"{cleanName} {city}", city);
                if (result.Lat != 0) return result;

                // Attempt 3 — just place name
                result = await TryGeocode(cleanName, city);
                if (result.Lat != 0) return result;

                // Attempt 4 — first two words + city
                var firstWords = string.Join(" ", cleanName.Split(' ').Take(2));
                result = await TryGeocode($"{firstWords} {city} India", city);
                if (result.Lat != 0) return result;

                // Fallback — city center
                var cityKey = city.ToLower().Trim();
                if (CityCenters.TryGetValue(cityKey, out var center))
                    return center;

                return (0, 0);
            }
            catch
            {
                var cityKey = city.ToLower().Trim();
                if (CityCenters.TryGetValue(cityKey, out var center))
                    return center;
                return (0, 0);
            }
        }

        private async Task<(double Lat, double Lng)> TryGeocode(string query, string city = "")
        {
            try
            {
                var encoded = Uri.EscapeDataString(query);

                // countrycodes=in restricts results to India only
                var url = $"https://nominatim.openstreetmap.org/search?q={encoded}&format=jsonv2&limit=1&countrycodes=in";

                // Nominatim rate limit — 1 request per second
                await Task.Delay(1000);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return (0, 0);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.GetArrayLength() == 0) return (0, 0);

                var result = doc.RootElement[0];
                var lat = double.Parse(
                    result.GetProperty("lat").GetString()!,
                    CultureInfo.InvariantCulture);
                var lng = double.Parse(
                    result.GetProperty("lon").GetString()!,
                    CultureInfo.InvariantCulture);

                // Reject if outside city bounds
                if (!string.IsNullOrEmpty(city) && !IsWithinCityBounds(lat, lng, city))
                    return (0, 0);

                return (lat, lng);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}