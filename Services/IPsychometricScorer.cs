using MatchmakingService.Data;
using MatchmakingService.Models;

namespace MatchmakingService.Services;

/// <summary>
/// Scores psychometric item responses into product-safe dimension scores.
/// Uses deterministic IPIP-based scoring — no AI involved.
/// </summary>
public interface IPsychometricScorer
{
    /// <summary>
    /// Computes all dimension scores from a user's raw Likert responses.
    /// Returns a dictionary of dimension → score (0.0–100.0).
    /// Items not present in <paramref name="responses"/> are skipped.
    /// </summary>
    IReadOnlyDictionary<string, double> ComputeScores(IReadOnlyDictionary<string, int> responses);

    /// <summary>
    /// Computes a single dimension score.
    /// </summary>
    double ComputeDimensionScore(string dimension, IReadOnlyDictionary<string, int> responses);
}

/// <summary>
/// Deterministic IPIP-based psychometric scorer.
/// Delegates to <see cref="PsychometricQuestionBank"/> for item definitions and scoring rules.
/// </summary>
public class PsychometricScorer : IPsychometricScorer
{
    private static readonly string[] AllDimensions =
    {
        PsychometricQuestionBank.Dimensions.SocialEnergy,
        PsychometricQuestionBank.Dimensions.Warmth,
        PsychometricQuestionBank.Dimensions.PlanningRhythm,
        PsychometricQuestionBank.Dimensions.Steadiness,
        PsychometricQuestionBank.Dimensions.Curiosity,
    };

    public IReadOnlyDictionary<string, double> ComputeScores(IReadOnlyDictionary<string, int> responses)
    {
        var scores = new Dictionary<string, double>(AllDimensions.Length);

        foreach (var dim in AllDimensions)
        {
            scores[dim] = PsychometricQuestionBank.ComputeDimensionScore(dim, responses);
        }

        return scores;
    }

    public double ComputeDimensionScore(string dimension, IReadOnlyDictionary<string, int> responses)
        => PsychometricQuestionBank.ComputeDimensionScore(dimension, responses);
}
