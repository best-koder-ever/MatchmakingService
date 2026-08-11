using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingService.Models;

/// <summary>
/// T620 — Post-date reflection feedback submitted by a user after a match conversation.
/// Feeds into radar recalibration (T622).
/// </summary>
public class PostDateFeedback
{
    public int Id { get; set; }

    [Required]
    public string KeycloakId { get; set; } = string.Empty;

    [Required]
    public string MatchId { get; set; } = string.Empty;

    /// <summary>Overall date experience 1-5</summary>
    [Range(1, 5)]
    public int OverallRating { get; set; }

    /// <summary>Chemistry felt 1-5</summary>
    [Range(1, 5)]
    public int ChemistryRating { get; set; }

    /// <summary>Conversation quality 1-5</summary>
    [Range(1, 5)]
    public int ConversationRating { get; set; }

    /// <summary>Would want to meet again</summary>
    public bool WouldMeetAgain { get; set; }

    /// <summary>Optional free-form reflection (max 500 chars)</summary>
    [MaxLength(500)]
    public string? FreeformReflection { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
