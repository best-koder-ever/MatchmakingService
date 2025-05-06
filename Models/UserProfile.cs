namespace MatchmakingService.Models
{
    public class UserProfile
    {
        public int UserId { get; set; }
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Preferences { get; set; } = string.Empty;
    }
}