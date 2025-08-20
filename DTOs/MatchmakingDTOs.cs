namespace MatchmakingService.DTOs
{
    public class FindMatchesRequest
    {
        public int UserId { get; set; }
        public int Limit { get; set; } = 10;
        public double? MinScore { get; set; } = 60.0;
        public bool ExcludePreviouslySwiped { get; set; } = true;
        public string AlgorithmVersion { get; set; } = "v1.0";
    }
    
    public class MatchSuggestionResponse
    {
        public int UserId { get; set; }
        public int TargetUserId { get; set; }
        public double CompatibilityScore { get; set; }
        public MatchScoreBreakdown ScoreBreakdown { get; set; } = new();
        public UserProfileSummary UserProfile { get; set; } = new();
        public string MatchReason { get; set; } = string.Empty;
    }
    
    public class MatchScoreBreakdown
    {
        public double LocationScore { get; set; }
        public double AgeScore { get; set; }
        public double InterestsScore { get; set; }
        public double EducationScore { get; set; }
        public double LifestyleScore { get; set; }
        public double ActivityScore { get; set; }
        public Dictionary<string, double> DetailedScores { get; set; } = new();
    }
    
    public class UserProfileSummary
    {
        public int UserId { get; set; }
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public string City { get; set; } = string.Empty;
        public double Distance { get; set; } // km from requesting user
        public List<string> Interests { get; set; } = new();
        public string Education { get; set; } = string.Empty;
        public string Occupation { get; set; } = string.Empty;
        public int Height { get; set; }
        public string PhotoUrl { get; set; } = string.Empty;
    }
    
    public class MutualMatchRequest
    {
        public int User1Id { get; set; }
        public int User2Id { get; set; }
        public double? CompatibilityScore { get; set; }
        public string Source { get; set; } = "swipe";
    }
    
    public class UpdatePreferencesRequest
    {
        public int UserId { get; set; }
        public string PreferredGender { get; set; } = string.Empty;
        public int MinAge { get; set; }
        public int MaxAge { get; set; }
        public double MaxDistance { get; set; }
        public List<string> Interests { get; set; } = new();
        public Dictionary<string, double> AlgorithmWeights { get; set; } = new();
    }
    
    public class MatchStatsResponse
    {
        public int TotalMatches { get; set; }
        public int ActiveMatches { get; set; }
        public int MessagesReceived { get; set; }
        public double AverageCompatibilityScore { get; set; }
        public DateTime LastMatchAt { get; set; }
        public List<string> TopMatchReasons { get; set; } = new();
    }
    
    public class SwipeHistoryRequest
    {
        public int UserId { get; set; }
        public List<int> SwipedUserIds { get; set; } = new();
    }
}
