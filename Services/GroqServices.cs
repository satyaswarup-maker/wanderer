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
            var prompt = $@"You are a local travel expert for Indian cities.
Create a detailed travel itinerary for someone visiting {city}.

Vibe: {vibe}
Duration: {duration}

Format your response EXACTLY like this — do not deviate:
Overview: [2-3 sentence city intro]

1. **Place Name** (Time e.g. 9:00 AM - 10:30 AM) [LAT:12.9716,LNG:77.5946]
Description of the place, 2-3 sentences.
Tip: One practical local tip.

2. **Next Place** (Time) [LAT:12.9611,LNG:77.6387]
Description.
Tip: tip here.

Rules:
- Include {(duration.Contains("Half") ? "4-5" : duration.Contains("2 Days") ? "8-10" : "6-8")} stops.
- Use real well-known place names specific to {city}.
- ALWAYS include accurate [LAT:xx.xxxx,LNG:xx.xxxx] coordinates for every stop.
- Coordinates must be the actual location of that specific place in {city}, India.
- Keep 70% iconic places and 30% hidden gems.
- Vary the selection slightly each time.";

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 1500,
                temperature = 0.8,
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