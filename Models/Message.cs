namespace MatchmakingService.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        // public virtual Match Match { get; set; } // Navigation property
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; }
        public System.DateTime SentAt { get; set; }
    }
}
