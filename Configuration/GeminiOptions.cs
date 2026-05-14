namespace StudentManagementSystem.Configuration;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-pro";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public bool Enabled { get; set; } = true;
    public double Temperature { get; set; } = 0.2;
    public int MaxOutputTokens { get; set; } = 2048;
}
