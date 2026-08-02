using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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

        public ItineraryController(GroqService groqService)
        {
            _groqService = groqService;
        }

        [HttpPost]
        public async Task<ActionResult<ItineraryResponse>> GenerateItinerary(
            [FromBody] ItineraryRequest request)
        {
            var rawText = await _groqService.GenerateItineraryAsync(
                request.City, request.Vibe, request.Duration);

            var response = ParseItinerary(rawText, request.City);

            return Ok(response);
        }

        private ItineraryResponse ParseItinerary(string rawText, string city)
        {
            var result = new ItineraryResponse();
            var lines = rawText.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

            ItineraryStop? currentStop = null;
            int stopIndex = 1;

            foreach (var line in lines)
            {
                // Extract overview
                if (line.StartsWith("Overview:", StringComparison.OrdinalIgnoreCase))
                {
                    result.Overview = line.Replace("Overview:", "").Trim();
                    continue;
                }

                // Match numbered stop with coordinates
                // Format: 1. **Place Name** (Time) [LAT:12.9716,LNG:77.5946]
                var stopMatch = Regex.Match(line,
                    @"^\d+\.\s+\*{0,2}([^*\(\[]+)\*{0,2}\s*(?:\(([^)]+)\))?\s*(?:\[LAT:([-\d.]+),LNG:([-\d.]+)\])?");

                if (stopMatch.Success)
                {
                    if (currentStop != null)
                        result.Stops.Add(currentStop);

                    var placeName = stopMatch.Groups[1].Value.Trim();
                    var time = stopMatch.Groups[2].Value.Trim();

                    double lat = 0, lng = 0;
                    if (stopMatch.Groups[3].Success && stopMatch.Groups[4].Success)
                    {
                        double.TryParse(stopMatch.Groups[3].Value,
                            NumberStyles.Float, CultureInfo.InvariantCulture, out lat);
                        double.TryParse(stopMatch.Groups[4].Value,
                            NumberStyles.Float, CultureInfo.InvariantCulture, out lng);
                    }

                    currentStop = new ItineraryStop
                    {
                        Index = stopIndex++,
                        Name = placeName,
                        Time = time,
                        Lat = lat,
                        Lng = lng
                    };
                    continue;
                }

                if (currentStop != null)
                {
                    // Handle tip variations
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
                result.Stops.Add(currentStop);

            return result;
        }
    }
}