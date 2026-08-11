using MatchmakingService.DTOs;

namespace MatchmakingService.Data;

/// <summary>
/// Curated public-domain IPIP items used for the onboarding micro-quiz.
///
/// Each item maps to a product-safe dimension and has a scoring direction:
///   +keyed:  1=S1  2=S2  3=S3  4=S4  5=S5  (raw = response value)
///   -keyed:  1=S5  2=S4  3=S3  4=S2  5=S1  (raw = 6 - response value)
///
/// All items are derived from the International Personality Item Pool (IPIP)
/// which is explicitly public domain for any purpose, commercial or non-commercial.
/// https://ipip.ori.org
///
/// IPIP citation: Goldberg, L. R. et al. The International Personality Item Pool:
/// A scientific collaboratory for the development of advanced measures of
/// personality and other individual differences. https://ipip.ori.org
///
/// Future: load from database or config for hot-reload without code changes.
/// </summary>
public static class PsychometricQuestionBank
{
    /// <summary>Product-safe dimension identifiers.</summary>
    public static class Dimensions
    {
        public const string SocialEnergy = "socialEnergy";
        public const string Warmth = "warmth";
        public const string PlanningRhythm = "planningRhythm";
        public const string Steadiness = "steadiness";
        public const string Curiosity = "curiosity";
    }

    /// <summary>
    /// A single psychometric item with its dimension mapping and scoring key.
    /// </summary>
    public sealed record Item(
        string Id,
        string TextEn,
        string TextSv,
        string Dimension,
        bool IsReversed  // true = -keyed (reverse score)
    );

    /// <summary>
    /// 15-item micro-quiz — keeps onboarding light while covering all 5 dimensions.
    /// Can be expanded to 20+ later without breaking existing scores.
    /// </summary>
    public static IReadOnlyList<Item> Items { get; } = new List<Item>
    {
        // ─── Social Energy (3 items) ───
        new("SE1", "I enjoy being the center of attention.",       "Jag tycker om att vara i centrum för uppmärksamhet.",        Dimensions.SocialEnergy,   false),
        new("SE2", "I prefer to stay in the background.",          "Jag föredrar att hålla mig i bakgrunden.",                   Dimensions.SocialEnergy,   true),
        new("SE3", "I feel energized around other people.",        "Jag blir energisk av att vara med andra människor.",         Dimensions.SocialEnergy,   false),

        // ─── Warmth (3 items) ───
        new("WA1", "I sympathize with others' feelings.",          "Jag sympatiserar med andras känslor.",                        Dimensions.Warmth,         false),
        new("WA2", "I am not really interested in others.",        "Jag är inte särskilt intresserad av andra.",                  Dimensions.Warmth,         true),
        new("WA3", "I take time out for others.",                  "Jag tar mig tid för andra.",                                  Dimensions.Warmth,         false),

        // ─── Planning Rhythm (3 items) ───
        new("PR1", "I like to have a clear schedule.",             "Jag gillar att ha ett tydligt schema.",                       Dimensions.PlanningRhythm, false),
        new("PR2", "I often leave my belongings around.",          "Jag lämnar ofta mina saker framme.",                          Dimensions.PlanningRhythm, true),
        new("PR3", "I follow a routine.",                          "Jag följer en rutin.",                                        Dimensions.PlanningRhythm, false),

        // ─── Steadiness (3 items) ───
        new("ST1", "I am relaxed most of the time.",               "Jag är avslappnad för det mesta.",                            Dimensions.Steadiness,     false),
        new("ST2", "I get upset easily.",                          "Jag blir lätt upprörd.",                                      Dimensions.Steadiness,     true),
        new("ST3", "I seldom feel blue.",                          "Jag känner mig sällan nere.",                                 Dimensions.Steadiness,     false),

        // ─── Curiosity (3 items) ───
        new("CU1", "I have a vivid imagination.",                  "Jag har en livlig fantasi.",                                  Dimensions.Curiosity,      false),
        new("CU2", "I am not interested in abstract ideas.",       "Jag är inte intresserad av abstrakta idéer.",                  Dimensions.Curiosity,      true),
        new("CU3", "I spend time reflecting on things.",           "Jag lägger tid på att reflektera över saker.",                Dimensions.Curiosity,      false),
    };

    /// <summary>
    /// Returns which items belong to a given dimension.
    /// </summary>
    public static IReadOnlyList<Item> GetItemsForDimension(string dimension)
        => Items.Where(i => i.Dimension == dimension).ToArray();

    /// <summary>
    /// Computes the dimension score (0–100) from raw Likert responses (1–5).
    /// Each item contributes 1–5, normalized so the sum maps to 0–100.
    /// </summary>
    public static double ComputeDimensionScore(string dimension, IReadOnlyDictionary<string, int> responses)
    {
        var items = GetItemsForDimension(dimension);
        if (items.Count == 0) return 50.0; // neutral default

        var sum = 0.0;
        var count = 0;

        foreach (var item in items)
        {
            if (!responses.TryGetValue(item.Id, out var raw)) continue;

            var scored = item.IsReversed ? 6 - raw : raw; // 1..5
            sum += scored;
            count++;
        }

        if (count == 0) return 50.0;

        // sum ranges from 1*count to 5*count → normalize to 0..100
        var min = 1.0 * count;
        var max = 5.0 * count;
        return Math.Round((sum - min) / (max - min) * 100.0, 1);
    }
}
