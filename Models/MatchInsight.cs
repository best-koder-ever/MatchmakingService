using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingService.Models
{
    /// <summary>
    /// T533: Per-user insight for a match. A single match produces TWO rows — one per participant (ForKeycloakId).
    /// JSON fields are stored as raw strings; value converters are a future task.
    /// </summary>
    [Table("MatchInsights")]
    public class MatchInsight
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>FK to the Matches table.</summary>
        [Required]
        public int MatchId { get; set; }

        /// <summary>The Keycloak subject (sub) of the user this insight belongs to.</summary>
        [Required]
        public string ForKeycloakId { get; set; } = string.Empty;

        /// <summary>JSON array of reasons this match is a good fit (nullable).</summary>
        public string? ReasonsJson { get; set; }

        /// <summary>JSON array of friction points (nullable).</summary>
        public string? FrictionJson { get; set; }

        /// <summary>JSON array of growth opportunities (nullable).</summary>
        public string? GrowthJson { get; set; }

        /// <summary>Aggregate insight score, 0–100.</summary>
        public double OverallScore { get; set; }

        /// <summary>UTC timestamp when this insight was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey(nameof(MatchId))]
        public Match? Match { get; set; }
    }
}
