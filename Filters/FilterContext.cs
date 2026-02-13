using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Bundles all data a filter might need, avoiding constructor injection per filter.
/// Keeps filters as pure query transformers.
/// T167: Filter contracts.
/// </summary>
public record FilterContext(
    UserProfile RequestingUser,
    HashSet<int> SwipedUserIds,
    HashSet<int> BlockedUserIds,
    CandidateOptions Options
);

/// <summary>
/// Result of executing the full filter pipeline.
/// </summary>
public record FilterPipelineResult(
    List<UserProfile> Candidates,
    List<FilterMetric> Metrics
);

/// <summary>
/// Per-filter execution metric for observability.
/// </summary>
public record FilterMetric(
    string FilterName,
    FilterType Type,
    int Order
);
