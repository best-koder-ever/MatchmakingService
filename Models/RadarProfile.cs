namespace MatchmakingService.Models;

/// <summary>
/// 7-axis compatibility radar profile per user.
/// Axes are scored 0.0-1.0. Computed from question answers (60%) + psykolog themes (40%).
/// </summary>
public class RadarProfile
{
    public int Id { get; set; }
    public string KeycloakId { get; set; } = string.Empty;

    // 7 axes
    public double EmotionalStability { get; set; }
    public double SocialEnergy { get; set; }
    public double Openness { get; set; }
    public double Warmth { get; set; }
    public double LifeStructure { get; set; }
    public double IntimacyComfort { get; set; }
    public double ConflictStyle { get; set; }

    /// <summary>0-1 confidence based on data sources available.</summary>
    public double Confidence { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
