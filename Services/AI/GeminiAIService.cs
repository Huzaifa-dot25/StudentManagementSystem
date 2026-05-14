using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StudentManagementSystem.Configuration;

namespace StudentManagementSystem.Services.AI;

public interface IGeminiClient
{
    Task<string> GenerateContentAsync(IReadOnlyList<(string role, string content)> messages, bool requireJsonObject, CancellationToken cancellationToken);
}

public sealed class GeminiAIService : IGeminiClient
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _opt;
    private readonly ILogger<GeminiAIService> _logger;

    public GeminiAIService(HttpClient http, IOptions<GeminiOptions> opt, ILogger<GeminiAIService> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<string> GenerateContentAsync(IReadOnlyList<(string role, string content)> messages, bool requireJsonObject, CancellationToken cancellationToken)
    {
        if (!_opt.Enabled || string.IsNullOrWhiteSpace(_opt.ApiKey))
            throw new InvalidOperationException("Gemini is disabled or API key is not configured. Set Gemini:ApiKey in appsettings.json.");

        var url = $"{_opt.BaseUrl.TrimEnd('/')}/models/{_opt.Model}:generateContent?key={_opt.ApiKey.Trim()}";

        var geminiMessages = new List<GeminiContent>();
        foreach (var (role, content) in messages)
        {
            var geminiRole = role.ToLower() switch
            {
                "system" => "user", // Gemini beta doesn't have a distinct system role in the same way, often prepended to user
                "assistant" => "model",
                _ => "user"
            };

            // If system message, we'll handle it specially if needed, but for now just map roles
            geminiMessages.Add(new GeminiContent
            {
                Role = geminiRole,
                Parts = new List<GeminiPart> { new GeminiPart { Text = content } }
            });
        }

        var payload = new GeminiRequest
        {
            Contents = geminiMessages,
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = _opt.Temperature,
                MaxOutputTokens = _opt.MaxOutputTokens,
                ResponseMimeType = requireJsonObject ? "application/json" : "text/plain"
            }
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync(url, httpContent, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini HTTP {Status}: {Body}", (int)resp.StatusCode, body);
            
            try 
            {
                using var errDoc = JsonDocument.Parse(body);
                var errRoot = errDoc.RootElement;
                if (errRoot.TryGetProperty("error", out var errorEl))
                {
                    var code = errorEl.GetProperty("code").GetInt32();
                    var msg = errorEl.GetProperty("message").GetString();
                    
                    if (code == 429)
                        throw new InvalidOperationException("Gemini AI is currently at capacity or quota exceeded. Please try again in a few minutes.");
                    if (code == 404)
                        throw new InvalidOperationException($"The AI model '{_opt.Model}' was not found. Please check your configuration.");
                        
                    throw new InvalidOperationException($"Gemini AI Error: {msg}");
                }
            }
            catch (JsonException) { /* Fallback to generic error */ }

            throw new InvalidOperationException($"Gemini request failed ({(int)resp.StatusCode}).");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var candidates = root.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0)
                return "I'm sorry, I couldn't generate a response.";

            var firstCandidate = candidates[0];
            var parts = firstCandidate.GetProperty("content").GetProperty("parts");
            if (parts.GetArrayLength() == 0)
                return "I'm sorry, the response was empty.";

            return parts[0].GetProperty("text").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini response: {Body}", body);
            throw new InvalidOperationException("Failed to parse AI response.");
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class GeminiRequest
    {
        public List<GeminiContent> Contents { get; set; } = new();
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        public string Role { get; set; } = "";
        public List<GeminiPart> Parts { get; set; } = new();
    }

    private sealed class GeminiPart
    {
        public string Text { get; set; } = "";
    }

    private sealed class GeminiGenerationConfig
    {
        public double? Temperature { get; set; }
        public int? MaxOutputTokens { get; set; }
        public string? ResponseMimeType { get; set; }
    }
}
