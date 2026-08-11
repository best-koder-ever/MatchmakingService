using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingService.Models
{
    /// <summary>
    /// Stores computed psychometric dimension scores for a user.
    /// Populated when the user completes the onboarding micro-quiz.
    /// Scores are deterministic IPIP-based values, not AI-generated.
    /// Dimensions use product-safe labels, not clinical terminology.
    /// </summary>
    [Table("PsychometricProfiles")]
    public class PsychometricProfile
    {
        public int Id { get; set; }

        /// <summary>FK to <see cref="UserProfile.UserId"/> (the internal user id).</summary>
        public int UserId { get; set; }

        /// <summary>Keycloak sub for lookups.</summary>
        [Required, StringLength(50)]
        public string KeycloakId { get; set; } = string.Empty;

        // ─── Product-safe psychometric dimensions (0.0–100.0) ───

        /// <summary>Social energy — outgoing vs reserved.</summary>
        public double SocialEnergy { get; set; }

        /// <summary>Warmth — empathy, kindness, interpersonal orientation.</summary>
        public double Warmth { get; set; }

        /// <summary>Planning rhythm — structured vs spontaneous.</summary>
        public double PlanningRhythm { get; set; }

        /// <summary>Emotional steadiness — calm vs reactive.</summary>
        public double Steadiness { get; set; }

        /// <summary>Curiosity — openness to new experiences, ideas.</summary>
        public double Curiosity { get; set; }

        /// <summary>Depth/version for future schema migrations.</summary>
        public int SchemaVersion { get; set; } = 1;

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // ─── Navigation ───
        [ForeignKey(nameof(UserId))]
        public UserProfile? User { get; set; }
    }
}
