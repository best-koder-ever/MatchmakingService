using Microsoft.EntityFrameworkCore;
using MatchmakingService.Models;

namespace MatchmakingService.Data
{
    /// <summary>
    /// Additional EF Core optimizations and indexes for better query performance
    /// Implements T062: Optimize EF Core queries
    /// </summary>
    public static class DbContextOptimizations
    {
        public static void ApplyOptimizations(ModelBuilder modelBuilder)
        {
            // Add composite indexes for common query patterns
            
            // UserProfile: Filter by IsActive + Gender + Age range (for candidate filtering)
            modelBuilder.Entity<UserProfile>()
                .HasIndex(up => new { up.IsActive, up.Gender, up.Age })
                .HasDatabaseName("IX_UserProfile_ActiveSearch");
            
            // UserProfiles: PreferredGender + IsActive (for matchmaking)
            modelBuilder.Entity<UserProfile>()
                .HasIndex(up => new { up.PreferredGender, up.IsActive })
                .HasDatabaseName("IX_UserProfile_PreferredGenderActive");
            
            // Matches: For checking active matches per user
            modelBuilder.Entity<Match>()
                .HasIndex(m => new { m.User1Id, m.IsActive })
                .HasDatabaseName("IX_Match_User1Id_IsActive");
            
            modelBuilder.Entity<Match>()
                .HasIndex(m => new { m.User2Id, m.IsActive })
                .HasDatabaseName("IX_Match_User2Id_IsActive");
            
            // MatchScore: For filtering valid scores by user
            modelBuilder.Entity<MatchScore>()
                .HasIndex(ms => new { ms.UserId, ms.IsValid, ms.OverallScore })
                .HasDatabaseName("IX_MatchScore_UserIdValid_Score");
            
            // MatchScore: For filtering valid scores with timestamp
            modelBuilder.Entity<MatchScore>()
                .HasIndex(ms => new { ms.UserId, ms.TargetUserId, ms.IsValid, ms.CalculatedAt })
                .HasDatabaseName("IX_MatchScore_Lookup_Valid");
            
            // UserInteractions: For date-based analytics
            modelBuilder.Entity<UserInteraction>()
                .HasIndex(ui => ui.CreatedAt)
                .HasDatabaseName("IX_UserInteraction_CreatedAt");
            
            // MatchingAlgorithmMetric: For user + date lookups
            modelBuilder.Entity<MatchingAlgorithmMetric>()
                .HasIndex(m => new { m.UserId, m.CalculatedAt })
                .HasDatabaseName("IX_MatchingAlgorithmMetric_User_Date");
        }
    }
}
