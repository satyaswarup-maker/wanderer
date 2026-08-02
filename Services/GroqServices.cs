using System.Text;
using System.Text.Json;

namespace wanderer_api.Services
{
    public class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GroqService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["ApiKeys:Groq"] ?? string.Empty;
        }

        public async Task<string> GenerateItineraryAsync(string city, string vibe, string duration)
        {
            var prompt = $@"You are a local travel expert for Indian cities with deep knowledge of specific venues.
Create a detailed travel itinerary for someone visiting {city}.

Vibe: {vibe}
Duration: {duration}

STRICT RULES — follow these exactly:
- For Foodie/Cafe vibe: name SPECIFIC restaurants, cafes, dhabas, street food stalls (e.g. 'Vidyarthi Bhavan', 'MTR 1924', 'Koshy's Restaurant') NOT generic areas like 'MG Road' or 'Church Street'
- For Explorer/Culture vibe: name SPECIFIC monuments, museums, temples, palaces (e.g. 'Vidhana Soudha', 'NGMA Museum') NOT generic areas
- For Nightlife vibe: name SPECIFIC bars, clubs, lounges (e.g. 'Toit Brewpub', 'The Black Rabbit')
- For Chill vibe: name SPECIFIC cafes, parks, bookstores (e.g. 'Dialogues Cafe', 'Koshy's')
- Every stop must be a SPECIFIC named venue or landmark, never a street or neighborhood
- Include {(duration.Contains("Half") ? "4-5" : duration.Contains("2 Days") ? "8-10" : "6-8")} stops
- Vary the selection slightly each time — mix iconic with hidden gems

Format EXACTLY like this:
Overview: [2-3 sentence city intro]

1. **Specific Venue Name** (9:00 AM - 10:30 AM) [LAT:12.9716,LNG:77.5946]
Description of this specific venue, what makes it special, what to order or do there.
Tip: One specific practical tip for this exact venue.

2. **Another Specific Venue** (11:00 AM - 12:30 PM) [LAT:12.9611,LNG:77.6387]
Description.
Tip: tip here.";

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 1500,
                temperature = 1.0,
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions", content);

            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var errorMessage = error.GetProperty("message").GetString();
                throw new Exception($"Groq API error: {errorMessage}");
            }

            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return text ?? string.Empty;
        }
    }
}