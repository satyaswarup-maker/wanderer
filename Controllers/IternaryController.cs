using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using wanderer_api.Models;
using wanderer_api.Services;

namespace wanderer_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ItineraryController : ControllerBase
    {
        private readonly GroqService _groqService;
        private readonly GeocodingService _geocodingService;

        public ItineraryController(GroqService groqService, GeocodingService geocodingService)
        {
            _groqService = groqService;
            _geocodingService = geocodingService;
        }

        [HttpPost]
        public async Task<ActionResult<ItineraryResponse>> GenerateItinerary(
            [FromBody] ItineraryRequest request)
        {
            var rawText = await _groqService.GenerateItineraryAsync(
                request.City, request.Vibe, request.Duration);

            var response = await ParseAndGeocodeAsync(rawText, request.City);

            return Ok(response);
        }

        private async Task<ItineraryResponse> ParseAndGeocodeAsync(string rawText, string city)
        {
            var result = new ItineraryResponse();
            var lines = rawText.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

            var stops = new List<ItineraryStop>();
            ItineraryStop? currentStop = null;
            int stopIndex = 1;

            foreach (var line in lines)
            {
                if (line.StartsWith("Overview:", StringComparison.OrdinalIgnoreCase))
                {
                    result.Overview = line.Replace("Overview:", "").Trim();
                    continue;
                }

                var stopMatch = Regex.Match(line, @"^\d+\.\s+\*{0,2}([^*\(]+)\*{0,2}\s*[\(\-]?\s*([\d:apmAPM\s\-–]+)?");
                if (stopMatch.Success)
                {
                    if (currentStop != null)
                        stops.Add(currentStop);

                    currentStop = new ItineraryStop
                    {
                        Index = stopIndex++,
                        Name = stopMatch.Groups[1].Value.Trim(),
                        Time = stopMatch.Groups[2].Value.Trim(),
                    };
                    continue;
                }

                if (currentStop != null)
                {
                    if (line.StartsWith("Tip:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("Local Tip:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("Insider Tip:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("Pro Tip:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentStop.Tip = Regex.Replace(line,
                            @"^(insider tip:|pro tip:|local tip:|tip:)",
                            "", RegexOptions.IgnoreCase).Trim();
                    }
                    else if (line.Contains("Tip:", StringComparison.OrdinalIgnoreCase))
                    {
                        var tipIndex = line.IndexOf("Tip:", StringComparison.OrdinalIgnoreCase);
                        currentStop.Tip = line.Substring(tipIndex + 4).Trim();
                        var descPart = line.Substring(0, tipIndex).Trim();
                        if (!string.IsNullOrEmpty(descPart))
                            currentStop.Desc = string.IsNullOrEmpty(currentStop.Desc)
                                ? descPart.Replace("**", "")
                                : currentStop.Desc + " " + descPart.Replace("**", "");
                    }
                    else if (string.IsNullOrEmpty(currentStop.Desc))
                        currentStop.Desc = line.Replace("**", "");
                    else
                        currentStop.Desc += " " + line.Replace("**", "");
                }
            }

            if (currentStop != null)
                stops.Add(currentStop);

            // Geocode ALL stops in parallel — much faster!
            await Task.WhenAll(stops.Select(async stop =>
            {
                var (lat, lng) = await _geocodingService.GeocodeAsync(stop.Name, city);
                stop.Lat = lat;
                stop.Lng = lng;
            }));

            result.Stops = stops;
            return result;
        }
    }
}