namespace MatchmakingService.DTOs;

/// <summary>
/// The primary payload sent to the Flutter connection insight card.
/// </summary>
public sealed record ConnectionHook(
    string Headline,
    string Body,
    IReadOnlyList<string> EvidenceChips,
    string SuggestedPrompt,
    string Tone,
    string ConfidenceLabel
);

/// <summary>
/// A single signal that contributed to the connection insight.
/// </summary>
public sealed record ConnectionSignal(
    SignalType Type,
    string Title,
    string Summary,
    IReadOnlyList<string> Evidence,
    double Confidence,
    bool IsCaution
);

/// <summary>
/// Categorised signal types — ranked by depth for hook selection.
/// </summary>
public enum SignalType
{
    /// <summary>Both users share similar values (highest depth).</summary>
    ValueAlignment,
    /// <summary>Personality trait complementarity or similarity.</summary>
    PersonalityFit,
    /// <summary>Communication and attachment/pace style.</summary>
    CommunicationStyle,
    /// <summary>Daily habits, social energy, routines.</summary>
    LifestyleRhythm,
    /// <summary>Shared interests, hobbies, activities.</summary>
    SharedInterest,
    /// <summary>An area where difference could be a growth edge.</summary>
    GrowthEdge,
    /// <summary>Low-score signal — honest caution, not overselling.</summary>
    Caution
}

/// <summary>
/// Overall confidence in the generated insight.
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>Strong signal from multiple aligned layers.</summary>
    High,
    /// <summary>Some signal but not conclusive.</summary>
    Medium,
    /// <summary>Weak or mostly surface-level signal.</summary>
    Low,
    /// <summary>Not enough data to produce a useful insight.</summary>
    InsufficientData
}
