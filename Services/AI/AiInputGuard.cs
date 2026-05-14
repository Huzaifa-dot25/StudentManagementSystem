using System.Text.RegularExpressions;

namespace StudentManagementSystem.Services.AI;

public interface IAiInputGuard
{
    string SanitizeUserMessage(string message);
}

public sealed class AiInputGuard : IAiInputGuard
{
    private static readonly Regex BlockedPatterns = new(
        @"(\bDROP\b|\bDELETE\b|\bTRUNCATE\b|\bALTER\b|\bEXEC\b|\bEXECUTE\b|\bUNION\b\s+SELECT|--|;|\bINSERT\b|\bUPDATE\b|\bGRANT\b|\bSHUTDOWN\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string SanitizeUserMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var trimmed = message.Trim();
        if (BlockedPatterns.IsMatch(trimmed))
            throw new InvalidOperationException("Message contains disallowed patterns. Rephrase your question without SQL or system commands.");

        return trimmed;
    }
}
