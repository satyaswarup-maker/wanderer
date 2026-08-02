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
            var randomSeed = new Random().Next(1, 99999);

            var prompt = $@"You are a local travel expert for Indian cities.
Create a detailed travel itinerary for someone visiting {city}.

Vibe: {vibe}
Duration: {duration}
Session seed (for variety): {randomSeed}

Format your response EXACTLY like this:
Overview: [2-3 sentence city intro]

Then list stops numbered like:
1. **Place Name** (Time e.g. 9:00 AM - 10:30 AM)
Description of the place, 2-3 sentences.
Tip: One practical local tip.

2. **Next Place** (Time)
...

Include {(duration.Contains("Half") ? "4-5" : duration.Contains("2 Days") ? "8-10" : "6-8")} stops.
Use real place names specific to {city}.
Every time you generate, keep the most iconic 2-3 places but swap remaining stops with different hidden gems, alternate restaurants, or offbeat spots.
Never give the exact same itinerary twice — vary the mix each time.";

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 1500,
                temperature = 1.2,
                seed = randomSeed
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