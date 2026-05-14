using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.Models.AI;

public class AiConversation
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Title { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<AiChatMessageEntity> Messages { get; set; } = new List<AiChatMessageEntity>();
}
