using MatchmakingService.Models;

namespace MatchmakingService.Services;

/// <summary>
/// T622 — Adjusts radar axes in-place based on post-date feedback ratings.
///
/// Logic:
///   - Low chemistry (1-2) + high conversation (4-5) → nudge IntimacyComfort down, Openness up
///   - High chemistry (4-5) → nudge IntimacyComfort up
///   - Low overall (1-2) → reduce Confidence by 10%
///   - High overall (4-5) → nudge Confidence up by 5%
///   - WouldMeetAgain=false + low overall → additional Confidence penalty
///
/// All axis values are clamped [0,1]. Confidence clamped [0.1, 1.0].
/// </summary>
public static class FeedbackRadarRecalibrator
{
    private const double NudgeLarge = 0.05;
    private const double NudgeSmall = 0.03;

    public static void Apply(
        RadarProfile profile,
        int overallRating,
        int chemistryRating,
        int conversationRating)
    {
        // Chemistry signal → IntimacyComfort axis
        if (chemistryRating <= 2)
            profile.IntimacyComfort = Clamp(profile.IntimacyComfort - NudgeLarge);
        else if (chemistryRating >= 4)
            profile.IntimacyComfort = Clamp(profile.IntimacyComfort + NudgeSmall);

        // Low chemistry + high conversation → Openness up (intellectual connection, not physical)
        if (chemistryRating <= 2 && conversationRating >= 4)
            profile.Openness = Clamp(profile.Openness + NudgeSmall);

        // Conflict signal: low conversation = might indicate conflict avoidance mismatch
        if (conversationRating <= 2)
            profile.ConflictStyle = Clamp(profile.ConflictStyle - NudgeSmall);

        // Overall confidence adjustment
        if (overallRating <= 2)
            profile.Confidence = Clamp(profile.Confidence * 0.90, min: 0.1);
        else if (overallRating >= 4)
            profile.Confidence = Clamp(profile.Confidence * 1.05, max: 1.0);

        profile.UpdatedAt = DateTime.UtcNow;
    }

    private static double Clamp(double v, double min = 0.0, double max = 1.0)
        => Math.Max(min, Math.Min(max, v));
}
