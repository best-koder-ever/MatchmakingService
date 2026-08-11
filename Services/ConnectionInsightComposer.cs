using System.Text.Json;
using MatchmakingService.DTOs;
using MatchmakingService.Models;
using MatchmakingService.Data;

namespace MatchmakingService.Services;

/// <summary>
/// Composes layered connection insights from compatibility, psychometrics,
/// and profile data. Uses deterministic template-based copy.
///
/// Signal ranking: ValueAlignment > PersonalityFit > CommunicationStyle >
///   LifestyleRhythm > SharedInterest > GrowthEdge > Caution
///
/// Honesty rules:
///   Score >= 75 → confident, warm language
///   Score 55-74 → exploratory language
///   Score < 55  → caution/contrast hook
///   Insufficient data → low confidence, minimal card
/// </summary>
public class ConnectionInsightComposer : IConnectionInsightComposer
{
    private readonly ILogger<ConnectionInsightComposer> _logger;

    // Product-safe dimension labels for the card
    private static readonly Dictionary<string, string> DimensionLabels = new()
    {
        [PsychometricQuestionBank.Dimensions.SocialEnergy] = "Social energy",
        [PsychometricQuestionBank.Dimensions.Warmth] = "Warmth",
        [PsychometricQuestionBank.Dimensions.PlanningRhythm] = "Planning style",
        [PsychometricQuestionBank.Dimensions.Steadiness] = "Emotional steadiness",
        [PsychometricQuestionBank.Dimensions.Curiosity] = "Curiosity",
    };

    public ConnectionInsightComposer(ILogger<ConnectionInsightComposer> logger)
    {
        _logger = logger;
    }

    public Task<ConnectionInsightResult?> ComposeAsync(
        int matchId,
        CompatibilityResult compat,
        PsychometricProfile? profileA,
        PsychometricProfile? profileB,
        IReadOnlyList<string> interestsA,
        IReadOnlyList<string> interestsB,
        IReadOnlyList<string> languagesA,
        IReadOnlyList<string> languagesB,
        string? cityA,
        string? cityB,
        string? occupationA,
        string? occupationB,
        CancellationToken ct = default)
    {
        try
        {
            var signals = new List<ConnectionSignal>();

            // Layer 1: Values alignment (highest depth)
            if (compat.ValuesScore >= 70)
            {
                signals.Add(new ConnectionSignal(
                    SignalType.ValueAlignment,
                    "Values alignment",
                    $"You both scored high on shared values ({compat.ValuesScore:F0}%).",
                    new[] { compat.TopReasons.FirstOrDefault() ?? "Shared principles" }.ToList(),
                    compat.ValuesScore / 100.0,
                    false));
            }

            // Layer 2: Personality fit from psychometric profiles
            if (profileA != null && profileB != null)
            {
                AddPsychometricSignals(signals, profileA, profileB);
            }

            // Layer 3: Lifestyle rhythm from compatibility categories
            if (compat.LifestyleScore >= 65)
            {
                signals.Add(new ConnectionSignal(
                    SignalType.LifestyleRhythm,
                    "Lifestyle rhythm",
                    $"Your daily styles align well ({compat.LifestyleScore:F0}%).",
                    Array.Empty<string>(),
                    compat.LifestyleScore / 100.0,
                    false));
            }

            // Layer 4: Shared interests (surface)
            var sharedInterests = interestsA.Intersect(interestsB).ToList();
            if (sharedInterests.Count > 0)
            {
                signals.Add(new ConnectionSignal(
                    SignalType.SharedInterest,
                    "Shared interests",
                    $"You both enjoy {sharedInterests.First()}.",
                    sharedInterests,
                    0.5 + (sharedInterests.Count * 0.1),
                    false));
            }

            // Layer 5: Other commonalities
            var commonalities = new List<string>();
            if (!string.IsNullOrEmpty(cityA) && string.Equals(cityA, cityB, StringComparison.OrdinalIgnoreCase))
                commonalities.Add($"Same city: {cityA}");
            if (!string.IsNullOrEmpty(occupationA) && string.Equals(occupationA, occupationB, StringComparison.OrdinalIgnoreCase))
                commonalities.Add($"Both in {occupationA}");

            // Shared languages
            var sharedLangs = languagesA.Intersect(languagesB).ToList();
            if (sharedLangs.Count > 0)
                commonalities.Add($"Share {sharedLangs.Count} language(s)");

            // Layer 6: Friction / caution
            if (compat.FrictionPoints.Count > 0 || compat.OverallScore < 55)
            {
                var frictionText = compat.FrictionPoints.Count > 0
                    ? compat.FrictionPoints[0]
                    : "Different perspectives";
                signals.Add(new ConnectionSignal(
                    SignalType.Caution,
                    "Different rhythms",
                    frictionText,
                    compat.FrictionPoints.ToArray(),
                    0.3,
                    true));
            }

            // Rank signals by depth
            var ranked = RankSignals(signals);

            // Determine confidence level
            var confidence = DetermineConfidence(ranked, compat, sharedInterests.Count);

            // Build the primary hook from the best signal
            var hook = BuildHook(ranked, compat, sharedInterests, commonalities, confidence);

            // Serialize
            var hookJson = JsonSerializer.Serialize(hook);
            var signalsJson = JsonSerializer.Serialize(ranked);
            var confidenceStr = confidence.ToString();

            _logger.LogDebug(
                "Connection insight composed for match {MatchId}: confidence={Confidence} hook={Hook}",
                matchId, confidenceStr, hook.Headline);

            return Task.FromResult<ConnectionInsightResult?>(new ConnectionInsightResult(hookJson, signalsJson, confidenceStr));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection insight composition failed for match {MatchId} — non-fatal", matchId);
            return Task.FromResult<ConnectionInsightResult?>(null);
        }
    }

    private static void AddPsychometricSignals(List<ConnectionSignal> signals, PsychometricProfile a, PsychometricProfile b)
    {
        // Compute dimension agreement (lower diff = higher agreement)
        var dims = new (string Dim, double ScoreA, double ScoreB)[]
        {
            (PsychometricQuestionBank.Dimensions.SocialEnergy, a.SocialEnergy, b.SocialEnergy),
            (PsychometricQuestionBank.Dimensions.Warmth, a.Warmth, b.Warmth),
            (PsychometricQuestionBank.Dimensions.PlanningRhythm, a.PlanningRhythm, b.PlanningRhythm),
            (PsychometricQuestionBank.Dimensions.Steadiness, a.Steadiness, b.Steadiness),
            (PsychometricQuestionBank.Dimensions.Curiosity, a.Curiosity, b.Curiosity),
        };

        // Similar dimensions (diff <= 20 points)
        var similar = dims.Where(d => Math.Abs(d.ScoreA - d.ScoreB) <= 20).ToList();
        foreach (var (dim, sa, sb) in similar.Take(2))
        {
            var avg = (sa + sb) / 2.0;
            var label = DimensionLabels.GetValueOrDefault(dim, dim);
            signals.Add(new ConnectionSignal(
                SignalType.PersonalityFit,
                label,
                $"You both lean similar on {label.ToLowerInvariant()} ({avg:F0}% similar).",
                new[] { $"{label}: {sa:F0}/{sb:F0}" },
                avg / 100.0,
                false));
        }

        // Contrast dimensions (diff >= 40 points) — GrowthEdge
        var contrasting = dims.Where(d => Math.Abs(d.ScoreA - d.ScoreB) >= 40).ToList();
        foreach (var (dim, sa, sb) in contrasting.Take(1))
        {
            var label = DimensionLabels.GetValueOrDefault(dim, dim);
            signals.Add(new ConnectionSignal(
                SignalType.GrowthEdge,
                $"{label} contrast",
                $"You complement each other on {label.ToLowerInvariant()}.",
                new[] { $"{label}: {sa:F0} vs {sb:F0}" },
                0.6,
                false));
        }
    }

    private static List<ConnectionSignal> RankSignals(List<ConnectionSignal> signals)
    {
        // Depth ranking: ValueAlignment > PersonalityFit > CommunicationStyle >
        // LifestyleRhythm > SharedInterest > GrowthEdge > Caution
        static int Depth(SignalType t) => t switch
        {
            SignalType.ValueAlignment => 0,
            SignalType.PersonalityFit => 1,
            SignalType.CommunicationStyle => 2,
            SignalType.LifestyleRhythm => 3,
            SignalType.SharedInterest => 4,
            SignalType.GrowthEdge => 5,
            SignalType.Caution => 6,
            _ => 99,
        };

        return signals.OrderBy(s => Depth(s.Type)).ThenByDescending(s => s.Confidence).ToList();
    }

    private static ConfidenceLevel DetermineConfidence(
        List<ConnectionSignal> ranked, CompatibilityResult compat, int sharedInterestCount)
    {
        if (compat.SharedAnswerCount == 0 && sharedInterestCount == 0)
            return ConfidenceLevel.InsufficientData;

        if (compat.OverallScore >= 75 && ranked.Any(s => !s.IsCaution && s.Confidence >= 0.7))
            return ConfidenceLevel.High;

        if (compat.OverallScore >= 55 || sharedInterestCount > 0)
            return ConfidenceLevel.Medium;

        return ConfidenceLevel.Low;
    }

    private static ConnectionHook BuildHook(
        List<ConnectionSignal> ranked,
        CompatibilityResult compat,
        List<string> sharedInterests,
        List<string> commonalities,
        ConfidenceLevel confidence)
    {
        if (confidence == ConfidenceLevel.InsufficientData)
        {
            return new ConnectionHook(
                "Still learning what connects you",
                "As you both share more, insights will appear here.",
                Array.Empty<string>(),
                "Start the conversation!",
                "neutral",
                "Insufficient data");
        }

        // Pick the best non-caution signal
        var bestSignal = ranked.FirstOrDefault(s => !s.IsCaution);

        // Pick the best signal overall (may be caution if only caution)
        var primarySignal = bestSignal ?? ranked.FirstOrDefault();

        if (primarySignal == null)
        {
            return new ConnectionHook(
                "A new connection",
                "You matched — start chatting to discover what you have in common.",
                Array.Empty<string>(),
                "Say hello!",
                "neutral",
                "New match");
        }

        // Build headline from best signal
        var headline = primarySignal.Type switch
        {
            SignalType.ValueAlignment => "You both seem to share important values",
            SignalType.PersonalityFit => primarySignal.Confidence >= 0.7
                ? $"You both lean toward {primarySignal.Title.ToLowerInvariant()}"
                : "You have some personality overlap to explore",
            SignalType.SharedInterest => $"You both enjoy {sharedInterests.FirstOrDefault()?.ToLowerInvariant() ?? "similar things"}",
            SignalType.LifestyleRhythm => "Your daily rhythms seem compatible",
            SignalType.GrowthEdge => "You complement each other in interesting ways",
            SignalType.Caution => compat.OverallScore < 55
                ? "Different rhythms here — worth checking early"
                : "Some contrasts to explore",
            _ => "You have things in common"
        };

        // Build evidence chips
        var chips = new List<string>();
        chips.AddRange(sharedInterests.Take(3));
        chips.AddRange(commonalities.Take(2));
        chips.AddRange(primarySignal.Evidence.Take(3 - chips.Count));

        // Build suggested prompt
        var prompt = primarySignal.Type switch
        {
            SignalType.ValueAlignment => $"Ask what {compat.TopReasons.FirstOrDefault()?.ToLowerInvariant()?.Replace("you both agree on", "").Trim() ?? "matters most to them"}",
            SignalType.PersonalityFit => $"Ask what their perfect {primarySignal.Title.ToLowerInvariant()} day looks like",
            SignalType.SharedInterest => $"Ask what got them into {sharedInterests.FirstOrDefault()?.ToLowerInvariant() ?? "it"}",
            SignalType.GrowthEdge => "Ask how they like plans to happen",
            SignalType.Caution => "Ask how spontaneous they like plans to be",
            _ => "Ask what brought them to the app"
        };

        // Determine tone
        var tone = confidence switch
        {
            ConfidenceLevel.High => "warm",
            ConfidenceLevel.Medium => "curious",
            ConfidenceLevel.Low => "honest",
            _ => "neutral"
        };

        // Confidence label
        var confidenceLabel = confidence switch
        {
            ConfidenceLevel.High => "Strong signal",
            ConfidenceLevel.Medium => "Worth exploring",
            ConfidenceLevel.Low => "Different rhythms",
            _ => "Still learning"
        };

        return new ConnectionHook(
            headline,
            string.Empty,
            chips.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray(),
            prompt,
            tone,
            confidenceLabel);
    }
}
