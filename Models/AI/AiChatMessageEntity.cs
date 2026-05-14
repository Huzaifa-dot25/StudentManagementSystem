using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.Models.AI;

public class AiChatMessageEntity
{
    public long Id { get; set; }

    public Guid ConversationId { get; set; }
    [ForeignKey(nameof(ConversationId))]
    public AiConversation Conversation { get; set; } = null!;

    [Required]
    [MaxLength(32)]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
