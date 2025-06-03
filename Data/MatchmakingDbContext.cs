using Microsoft.EntityFrameworkCore;
using MatchmakingService.Models; // Add this namespace

namespace MatchmakingService.Data
{
    public class MatchmakingDbContext : DbContext
    {
        public MatchmakingDbContext(DbContextOptions<MatchmakingDbContext> options) : base(options) { }

        public DbSet<UserInteraction> UserInteractions { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<Message> Messages { get; set; } // Add this line
    }
}