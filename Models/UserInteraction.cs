namespace MatchmakingService.Models
{
    public class UserInteraction
    {
        public int Id { get; set; } // Primary Key
        public int UserId { get; set; } // The user performing the interaction
        public int TargetUserId { get; set; } // The user being interacted with
        public string InteractionType { get; set; } = string.Empty; // "LIKE" or "PASS"
        public DateTime CreatedAt { get; set; } // Timestamp of the interaction
    }
}