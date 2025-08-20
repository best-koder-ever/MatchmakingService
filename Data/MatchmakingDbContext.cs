using Microsoft.EntityFrameworkCore;
using MatchmakingService.Models;

namespace MatchmakingService.Data
{
    public class MatchmakingDbContext : DbContext
    {
        public MatchmakingDbContext(DbContextOptions<MatchmakingDbContext> options) : base(options) { }

        public DbSet<UserInteraction> UserInteractions { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<MatchScore> MatchScores { get; set; }
        public DbSet<MatchPreference> MatchPreferences { get; set; }
        public DbSet<MatchingAlgorithmMetric> MatchingAlgorithmMetrics { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Match entity configuration
            modelBuilder.Entity<Match>()
                .HasIndex(m => m.User1Id)
                .HasDatabaseName("IX_Match_User1Id");
                
            modelBuilder.Entity<Match>()
                .HasIndex(m => m.User2Id)
                .HasDatabaseName("IX_Match_User2Id");
                
            modelBuilder.Entity<Match>()
                .HasIndex(m => new { m.User1Id, m.User2Id })
                .IsUnique()
                .HasDatabaseName("IX_Match_User1Id_User2Id");

            modelBuilder.Entity<Match>()
                .HasIndex(m => m.CreatedAt)
                .HasDatabaseName("IX_Match_CreatedAt");

            // UserProfile entity configuration
            modelBuilder.Entity<UserProfile>()
                .HasIndex(up => up.UserId)
                .IsUnique()
                .HasDatabaseName("IX_UserProfile_UserId");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(up => new { up.Latitude, up.Longitude })
                .HasDatabaseName("IX_UserProfile_Location");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(up => up.Age)
                .HasDatabaseName("IX_UserProfile_Age");

            modelBuilder.Entity<UserProfile>()
                .HasIndex(up => up.Gender)
                .HasDatabaseName("IX_UserProfile_Gender");

            // MatchScore entity configuration
            modelBuilder.Entity<MatchScore>()
                .HasIndex(ms => ms.UserId)
                .HasDatabaseName("IX_MatchScore_UserId");

            modelBuilder.Entity<MatchScore>()
                .HasIndex(ms => new { ms.UserId, ms.TargetUserId })
                .IsUnique()
                .HasDatabaseName("IX_MatchScore_UserId_TargetUserId");

            modelBuilder.Entity<MatchScore>()
                .HasIndex(ms => ms.OverallScore)
                .HasDatabaseName("IX_MatchScore_OverallScore");

            modelBuilder.Entity<MatchScore>()
                .HasIndex(ms => ms.CalculatedAt)
                .HasDatabaseName("IX_MatchScore_CalculatedAt");

            // MatchPreference entity configuration
            modelBuilder.Entity<MatchPreference>()
                .HasIndex(mp => mp.UserId)
                .HasDatabaseName("IX_MatchPreference_UserId");

            modelBuilder.Entity<MatchPreference>()
                .HasIndex(mp => new { mp.UserId, mp.PreferenceType })
                .IsUnique()
                .HasDatabaseName("IX_MatchPreference_UserId_Type");

            // MatchingAlgorithmMetric entity configuration
            modelBuilder.Entity<MatchingAlgorithmMetric>()
                .HasIndex(mam => mam.UserId)
                .HasDatabaseName("IX_MatchingAlgorithmMetric_UserId");

            modelBuilder.Entity<MatchingAlgorithmMetric>()
                .HasIndex(mam => mam.CalculatedAt)
                .HasDatabaseName("IX_MatchingAlgorithmMetric_CalculatedAt");

            // Ensure proper ordering for matches (smaller userId first)
            modelBuilder.Entity<Match>()
                .ToTable(t => t.HasCheckConstraint("CK_Match_UserOrder", "User1Id < User2Id"));
        }
    }
}