using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Services;

public interface IRadarProfileCalculator
{
    Task<RadarProfile> CalculateAsync(string keycloakId, CancellationToken ct = default);
    Task<RadarProfile> ComputeAndSaveAsync(string keycloakId, CancellationToken ct = default);
}

/// <summary>
/// Computes the 7-axis RadarProfile from:
///   - QuestionAnswers (60%): TIPI-10 → BigFive axes, ECR-S → Intimacy/Conflict, Values → Warmth/LifeStructure
///   - Confidence: low when few answers, grows with answer count
/// Future: blend in psykolog themes (40%) when vector data is available.
/// </summary>
public class RadarProfileCalculator : IRadarProfileCalculator
{
    private readonly MatchmakingDbContext _db;

    public RadarProfileCalculator(MatchmakingDbContext db)
    {
        _db = db;
    }

    public async Task<RadarProfile> CalculateAsync(string keycloakId, CancellationToken ct = default)
    {
        var answers = await _db.UserQuestionAnswers
            .Include(a => a.Question)
            .Where(a => a.KeycloakId == keycloakId)
            .ToListAsync(ct);

        if (answers.Count == 0)
            return Neutral(keycloakId);

        // Bucket answers by category
        var byCategory = answers
            .Where(a => a.Question != null)
            .GroupBy(a => a.Question!.Category)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Map category averages (normalised to 0-1 from 1-7 scale) → axes
        static double Norm(double avg) => (avg - 1.0) / 6.0;

        double Avg(QuestionCategory cat)
        {
            if (!byCategory.TryGetValue(cat, out var list) || list.Count == 0) return 0.5;
            return Norm(list.Average(a => (double)a.Value));
        }

        var personality = Avg(QuestionCategory.Personality);
        var values      = Avg(QuestionCategory.Values);
        var attachment  = Avg(QuestionCategory.Attachment);
        var lifestyle   = Avg(QuestionCategory.Lifestyle);

        // Axis calculation — approximate mapping:
        //   EmotionalStability   ← mostly Personality (inverse of neuroticism)
        //   SocialEnergy         ← Personality (extraversion facet)
        //   Openness             ← 50/50 Personality + Values
        //   Warmth               ← Values
        //   LifeStructure        ← Lifestyle (conscientiousness-like)
        //   IntimacyComfort      ← Attachment
        //   ConflictStyle        ← Attachment (inverse: high attachment security = low conflict avoidance)
        var emotionalStability = Clamp(0.7 * personality + 0.3 * values);
        var socialEnergy       = Clamp(0.8 * personality + 0.2 * lifestyle);
        var openness           = Clamp(0.5 * personality + 0.5 * values);
        var warmth             = Clamp(0.9 * values);
        var lifeStructure      = Clamp(0.8 * lifestyle + 0.2 * values);
        var intimacyComfort    = Clamp(attachment);
        var conflictStyle      = Clamp(0.6 * attachment + 0.4 * (1 - values)); // flip: high values → lower conflict

        // Confidence: scales with total answer count
        var confidence = ConfidenceFromAnswerCount(answers.Count);

        return new RadarProfile
        {
            KeycloakId        = keycloakId,
            EmotionalStability = emotionalStability,
            SocialEnergy      = socialEnergy,
            Openness          = openness,
            Warmth            = warmth,
            LifeStructure     = lifeStructure,
            IntimacyComfort   = intimacyComfort,
            ConflictStyle     = conflictStyle,
            Confidence        = confidence,
            UpdatedAt         = DateTime.UtcNow
        };
    }

    /// <summary>Upserts a RadarProfile row in the database.</summary>
    public async Task<RadarProfile> ComputeAndSaveAsync(string keycloakId, CancellationToken ct = default)
    {
        var profile = await CalculateAsync(keycloakId, ct);
        var existing = await _db.RadarProfiles.FirstOrDefaultAsync(r => r.KeycloakId == keycloakId, ct);
        if (existing != null)
        {
            existing.EmotionalStability = profile.EmotionalStability;
            existing.SocialEnergy       = profile.SocialEnergy;
            existing.Openness           = profile.Openness;
            existing.Warmth             = profile.Warmth;
            existing.LifeStructure      = profile.LifeStructure;
            existing.IntimacyComfort    = profile.IntimacyComfort;
            existing.ConflictStyle      = profile.ConflictStyle;
            existing.Confidence         = profile.Confidence;
            existing.UpdatedAt          = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return existing;
        }
        _db.RadarProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public static double ConfidenceFromAnswerCount(int count) => count switch
    {
        0       => 0.0,
        < 5     => 0.3,
        < 10    => 0.5,
        < 20    => 0.7,
        < 32    => 0.85,
        _       => 0.95
    };

    private static double Clamp(double v) => Math.Max(0.0, Math.Min(1.0, v));

    private static RadarProfile Neutral(string keycloakId) => new()
    {
        KeycloakId = keycloakId,
        EmotionalStability = 0.5, SocialEnergy = 0.5, Openness = 0.5,
        Warmth = 0.5, LifeStructure = 0.5, IntimacyComfort = 0.5, ConflictStyle = 0.5,
        Confidence = 0.0, UpdatedAt = DateTime.UtcNow
    };
}
