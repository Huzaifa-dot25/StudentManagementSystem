using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StudentManagementSystem.Configuration;

namespace StudentManagementSystem.Services.AI;

public interface IOpenAiClient
{
    Task<string> ChatCompletionAsync(IReadOnlyList<(string role, string content)> messages, bool requireJsonObject, CancellationToken cancellationToken);
}

public sealed class OpenAiClient : IOpenAiClient
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _opt;
    private readonly ILogger<OpenAiClient> _logger;

    public OpenAiClient(HttpClient http, IOptions<OpenAiOptions> opt, ILogger<OpenAiClient> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<string> ChatCompletionAsync(IReadOnlyList<(string role, string content)> messages, bool requireJsonObject, CancellationToken cancellationToken)
    {
        if (!_opt.Enabled || string.IsNullOrWhiteSpace(_opt.ApiKey))
            throw new InvalidOperationException("OpenAI is disabled or API key is not configured. Set OpenAI:ApiKey or environment OPENAI_API_KEY.");

        var url = CombineUri(_opt.BaseUrl, "chat/completions");
        var payload = new ChatCompletionRequest
        {
            Model = _opt.Model,
            Temperature = _opt.Temperature,
            MaxTokens = _opt.MaxOutputTokens,
            Messages = messages.Select(m => new ChatMessage { Role = m.role, Content = m.content }).ToList(),
            ResponseFormat = requireJsonObject ? new ResponseFormat { Type = "json_object" } : null
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey.Trim());
        req.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI HTTP {Status}: {Body}", (int)resp.StatusCode, body);
            throw new InvalidOperationException($"OpenAI request failed ({(int)resp.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0];
        var content = choice.GetProperty("message").GetProperty("content").GetString();
        return content ?? string.Empty;
    }

    private static string CombineUri(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return path;
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = "";
        public double Temperature { get; set; }
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
        public List<ChatMessage> Messages { get; set; } = new();
        [JsonPropertyName("response_format")]
        public ResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private sealed class ResponseFormat
    {
        public string Type { get; set; } = "json_object";
    }
}
