using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StudentManagementSystem.Configuration;

namespace StudentManagementSystem.Services.AI;

public interface IGroqClient
{
    Task<string> GenerateContentAsync(IReadOnlyList<(string role, string content)> messages, bool requireJsonObject, CancellationToken cancellationToken);
}

public sealed class GroqAIService : IGroqClient
{
    private readonly HttpClient _http;
    private readonly GroqOptions _opt;
    private readonly ILogger<GroqAIService> _logger;

    public GroqAIService(HttpClient http, IOptions<GroqOptions> opt, ILogger<GroqAIService> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<string> GenerateContentAsync(IReadOnlyList<(string role, string content)> messages, bool requireJsonObject, CancellationToken cancellationToken)
    {
        if (!_opt.Enabled || string.IsNullOrWhiteSpace(_opt.ApiKey))
            throw new InvalidOperationException("Groq is disabled or API key is not configured. Set Groq:ApiKey in appsettings.json.");

        var url = $"{_opt.BaseUrl.TrimEnd('/')}/chat/completions";

        var groqMessages = new List<GroqMessage>();

        foreach (var (role, content) in messages)
        {
            var lowerRole = role.ToLower();
            string groqRole = lowerRole switch
            {
                "system" => "system",
                "assistant" => "assistant",
                "model" => "assistant",
                _ => "user"
            };

            // Handle system instruction prefixes sent as user content
            if (lowerRole == "user" && content.StartsWith("System Instruction:", StringComparison.OrdinalIgnoreCase))
            {
                var text = content["System Instruction:".Length..].TrimStart();
                groqMessages.Add(new GroqMessage { Role = "system", Content = text });
            }
            else
            {
                groqMessages.Add(new GroqMessage { Role = groqRole, Content = content });
            }
        }

        if (groqMessages.Count == 0)
            throw new InvalidOperationException("No messages provided to Groq.");

        var payload = new GroqRequest
        {
            Model = _opt.Model,
            Messages = groqMessages,
            Temperature = _opt.Temperature,
            MaxTokens = _opt.MaxOutputTokens,
            ResponseFormat = requireJsonObject ? new GroqResponseFormat { Type = "json_object" } : null
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        // Set Groq Authorization header
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _opt.ApiKey.Trim());

        using var resp = await _http.PostAsync(url, httpContent, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Groq HTTP {Status}: {Body}", (int)resp.StatusCode, body);

            try
            {
                using var errDoc = JsonDocument.Parse(body);
                var errRoot = errDoc.RootElement;
                if (errRoot.TryGetProperty("error", out var errorEl))
                {
                    var msg = errorEl.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                    var code = errorEl.TryGetProperty("code", out var c) ? c.GetString() : null;

                    if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        throw new InvalidOperationException("Groq AI is currently at capacity or rate-limit exceeded. Please try again in a few moments.");

                    throw new InvalidOperationException($"Groq AI Error: {msg} (Code: {code ?? "N/A"})");
                }
            }
            catch (JsonException) { /* Fallback to generic error */ }

            throw new InvalidOperationException($"Groq request failed ({(int)resp.StatusCode}).");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
                return "I'm sorry, I couldn't generate a response.";

            var firstChoice = choices[0];
            var messageEl = firstChoice.GetProperty("message");
            var content = messageEl.GetProperty("content").GetString();

            return content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Groq response: {Body}", body);
            throw new InvalidOperationException("Failed to parse AI response.");
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class GroqRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<GroqMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("response_format")]
        public GroqResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class GroqMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class GroqResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }
}
