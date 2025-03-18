namespace MatchmakingService.Models
{
    public class Match
    {
        public int Id { get; set; } // Primary Key
        public int User1Id { get; set; } // First user in the match
        public int User2Id { get; set; } // Second user in the match
        public DateTime CreatedAt { get; set; } // Timestamp of the match
    }
}