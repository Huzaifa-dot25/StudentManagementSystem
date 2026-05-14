namespace StudentManagementSystem.Configuration;

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>API key. Prefer environment variable OPENAI_API_KEY in production.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public string Model { get; set; } = "gpt-4o-mini";

    public int MaxOutputTokens { get; set; } = 1200;

    public double Temperature { get; set; } = 0.2;

    public bool Enabled { get; set; } = true;
}
