using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models.AI.Dtos;
using StudentManagementSystem.Services.AI;

namespace StudentManagementSystem.Controllers.Api;

[ApiController]
[Route("api/ai/[controller]")]
[Authorize(Roles = "Admin")]
[IgnoreAntiforgeryToken]
[EnableRateLimiting("ai")]
public class ChatController : ControllerBase
{
    private readonly IAiChatPersistenceService _chat;
    private readonly IAiSecurityContextFactory _securityFactory;
    private readonly ApplicationDbContext _db;

    public ChatController(IAiChatPersistenceService chat, IAiSecurityContextFactory securityFactory, ApplicationDbContext db)
    {
        _chat = chat;
        _securityFactory = securityFactory;
        _db = db;
    }

    [HttpPost("message")]
    [ProducesResponseType(typeof(AiChatResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiChatResponseDto>> PostMessage([FromBody] AiChatRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message is required.");

        var security = await _securityFactory.CreateAsync(User, cancellationToken);
        try
        {
            var result = await _chat.ProcessChatAsync(request, security, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("conversations")]
    [ProducesResponseType(typeof(IReadOnlyList<AiConversationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AiConversationSummaryDto>>> ListConversations(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var list = await _db.AiConversations.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Take(30)
            .Select(c => new AiConversationSummaryDto { Id = c.Id, Title = c.Title, UpdatedAtUtc = c.UpdatedAtUtc })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }
}
