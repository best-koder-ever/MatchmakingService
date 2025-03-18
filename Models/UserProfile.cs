namespace MatchmakingService.Models
{
    public class UserProfile
    {
        public int UserId { get; set; }
        public string Gender { get; set; }
        public int Age { get; set; }
        public string Location { get; set; }
        public string Preferences { get; set; }
    }
}