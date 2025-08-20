using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingService.Models
{
    [Table("Matches")]
    public class Match
    {
        public int Id { get; set; } // Primary Key
        public int User1Id { get; set; } // First user in the match
        public int User2Id { get; set; } // Second user in the match
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Timestamp of the match
        public double CompatibilityScore { get; set; } // Score from 0-100
        public bool IsActive { get; set; } = true; // Whether match is still active
        public DateTime? UnmatchedAt { get; set; } // When one user unmatched
        public int? UnmatchedByUserId { get; set; } // Which user initiated unmatch
        public int? LastMessageByUserId { get; set; } // Track last message sender
        public DateTime? LastMessageAt { get; set; } // Track last activity
        public string? MatchSource { get; set; } // How they were matched (algorithm, manual, etc.)
    }
}