using MatchmakingService.Models;

namespace MatchmakingService.Strategies;

/// <summary>
/// A candidate with individual score components.
/// Exposes breakdown for debugging, A/B testing, and future "why this match" UI.
/// </summary>
public record ScoredCandidate(
    UserProfile Profile,
    double CompatibilityScore,
    double ActivityScore,
    double DesirabilityScore,
    double FinalScore,
    string StrategyUsed
);
