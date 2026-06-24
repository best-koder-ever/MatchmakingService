using MatchmakingService.DTOs;
using MatchmakingService.Models;

namespace MatchmakingService.Services;

/// <summary>
/// Composes a structured connection insight from compatibility results,
/// psychometric profiles, and profile facts.
///
/// This is the core "deeper human nature" logic — it chooses which signal
/// to surface, at what confidence level, and in what tone.
/// No AI is used in this first implementation; all copy is template-based.
/// </summary>
public interface IConnectionInsightComposer
{
    /// <summary>
    /// Produces a connection insight for the card.
    /// Safe to call with partial data — returns null/unset fields gracefully.
    /// </summary>
    Task<ConnectionInsightResult?> ComposeAsync(
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
        CancellationToken ct = default);
}

/// <summary>
/// The composed result — what gets serialized into MatchInsight.
/// </summary>
public sealed record ConnectionInsightResult(
    string HookJson,
    string SignalsJson,
    string ConfidenceLevel
);
