using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingService.Models
{
    [Table("MatchScores")]
    public class MatchScore
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TargetUserId { get; set; }
        public double OverallScore { get; set; } // 0-100
        public double LocationScore { get; set; }
        public double AgeScore { get; set; }
        public double InterestsScore { get; set; }
        public double EducationScore { get; set; }
        public double LifestyleScore { get; set; }
        public double ActivityScore { get; set; } // Based on app usage patterns
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
        public bool IsValid { get; set; } = true; // Cache invalidation flag
    }
    
    [Table("MatchPreferences")]
    public class MatchPreference
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string PreferenceType { get; set; } = string.Empty; // age, distance, education, etc.
        public string PreferenceValue { get; set; } = string.Empty; // JSON value
        public double Weight { get; set; } = 1.0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    
    [Table("MatchingAlgorithmMetrics")]
    public class MatchingAlgorithmMetric
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string AlgorithmVersion { get; set; } = string.Empty;
        public int SuggestionsGenerated { get; set; }
        public int SwipesReceived { get; set; }
        public int LikesReceived { get; set; }
        public int MatchesCreated { get; set; }
        public double SuccessRate { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }
}
