using StudentManagementSystem.Models.AI;

namespace StudentManagementSystem.Services.AI;

public interface IAiOrchestrator
{
    Task<(string reply, string intent, bool usedDatabase)> ProcessUserMessageAsync(string userMessage, AiSecurityContext security, CancellationToken cancellationToken);
}

public sealed class AiOrchestrator : IAiOrchestrator
{
    private readonly IAiInputGuard _guard;
    private readonly IAiIntentInterpreter _interpreter;
    private readonly IAiSecureDataExecutor _executor;
    private readonly IAiResponseFormatter _formatter;
    private readonly Microsoft.Extensions.Options.IOptions<Configuration.AiOptions> _aiOpt;

    public AiOrchestrator(
        IAiInputGuard guard,
        IAiIntentInterpreter interpreter,
        IAiSecureDataExecutor executor,
        IAiResponseFormatter formatter,
        Microsoft.Extensions.Options.IOptions<Configuration.AiOptions> aiOpt)
    {
        _guard = guard;
        _interpreter = interpreter;
        _executor = executor;
        _formatter = formatter;
        _aiOpt = aiOpt;
    }

    public async Task<(string reply, string intent, bool usedDatabase)> ProcessUserMessageAsync(string userMessage, AiSecurityContext security, CancellationToken cancellationToken)
    {
        var clean = _guard.SanitizeUserMessage(userMessage);
        if (clean.Length > _aiOpt.Value.MaxUserMessageLength)
            throw new InvalidOperationException($"Message exceeds maximum length ({_aiOpt.Value.MaxUserMessageLength}).");

        var intent = await _interpreter.InterpretAsync(clean, security, cancellationToken);
        object? data = null;
        var usedDb = false;

        if (!string.Equals(intent.Intent, AiIntents.GeneralAnswer, StringComparison.OrdinalIgnoreCase))
        {
            data = await _executor.ExecuteAsync(intent, security, cancellationToken);
            usedDb = data != null;
        }

        var reply = await _formatter.FormatAnswerAsync(clean, intent, data, security, cancellationToken);
        return (reply, intent.Intent, usedDb);
    }
}
