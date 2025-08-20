using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingService.Models
{
    [Table("UserProfiles")]
    public class UserProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; } // Foreign key to User service
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        
        // Preferences
        public string PreferredGender { get; set; } = string.Empty;
        public int MinAge { get; set; }
        public int MaxAge { get; set; }
        public double MaxDistance { get; set; } = 50; // km
        
        // Additional profile data
        public string Interests { get; set; } = string.Empty; // JSON array of interests
        public string Education { get; set; } = string.Empty;
        public string Occupation { get; set; } = string.Empty;
        public int Height { get; set; } // in cm
        public string Religion { get; set; } = string.Empty;
        public string Ethnicity { get; set; } = string.Empty;
        public bool WantsChildren { get; set; }
        public bool HasChildren { get; set; }
        public string SmokingStatus { get; set; } = string.Empty; // Never, Sometimes, Often
        public string DrinkingStatus { get; set; } = string.Empty; // Never, Sometimes, Often
        
        // Algorithm weights
        public double LocationWeight { get; set; } = 1.0;
        public double AgeWeight { get; set; } = 1.0;
        public double InterestsWeight { get; set; } = 1.0;
        public double EducationWeight { get; set; } = 0.5;
        public double LifestyleWeight { get; set; } = 0.7;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}