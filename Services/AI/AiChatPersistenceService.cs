using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models.AI.Dtos;

namespace StudentManagementSystem.Services.AI;

public interface IAiChatPersistenceService
{
    Task<AiChatResponseDto> ProcessChatAsync(AiChatRequestDto request, AiSecurityContext security, CancellationToken cancellationToken);
}

public sealed class AiChatPersistenceService : IAiChatPersistenceService
{
    private readonly ApplicationDbContext _db;
    private readonly IAiOrchestrator _orchestrator;

    public AiChatPersistenceService(ApplicationDbContext db, IAiOrchestrator orchestrator)
    {
        _db = db;
        _orchestrator = orchestrator;
    }

    public async Task<AiChatResponseDto> ProcessChatAsync(AiChatRequestDto request, AiSecurityContext security, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(security.UserId))
            throw new InvalidOperationException("User is not authenticated.");

        var convId = request.ConversationId ?? Guid.NewGuid();
        var conv = await _db.AiConversations.FirstOrDefaultAsync(c => c.Id == convId, cancellationToken);

        if (conv == null)
        {
            conv = new Models.AI.AiConversation
            {
                Id = convId,
                UserId = security.UserId,
                Title = request.Message.Length > 80 ? request.Message[..80] + "…" : request.Message,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.AiConversations.Add(conv);
        }
        else if (!string.Equals(conv.UserId, security.UserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Conversation does not belong to the current user.");
        }

        conv.UpdatedAtUtc = DateTime.UtcNow;

        _db.AiChatMessages.Add(new Models.AI.AiChatMessageEntity
        {
            ConversationId = convId,
            Role = "user",
            Content = request.Message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        var (reply, intent, usedDb) = await _orchestrator.ProcessUserMessageAsync(request.Message, security, cancellationToken);

        _db.AiChatMessages.Add(new Models.AI.AiChatMessageEntity
        {
            ConversationId = convId,
            Role = "assistant",
            Content = reply,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new AiChatResponseDto
        {
            ConversationId = convId,
            Reply = reply,
            IntentUsed = intent,
            UsedDatabase = usedDb
        };
    }
}
