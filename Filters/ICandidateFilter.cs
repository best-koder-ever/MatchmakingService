using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// A pluggable candidate filter that transforms an IQueryable pipeline.
/// All filtering happens at the database level — never materialize to in-memory.
/// T167: Filter contracts.
/// </summary>
public interface ICandidateFilter
{
    /// <summary>Display name for logging/metrics</summary>
    string Name { get; }

    /// <summary>Execution order (lower = first). Cheapest filters first.</summary>
    int Order { get; }

    /// <summary>Whether this filter is a hard exclusion (dealbreaker) or soft ranking signal</summary>
    FilterType Type { get; }

    /// <summary>Apply filter to candidate query. Must return IQueryable — never call ToList/ToArray.</summary>
    IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context);
}

public enum FilterType
{
    /// <summary>Hard exclusion — candidate is removed if they don't pass</summary>
    Dealbreaker,
    /// <summary>Soft preference — used for ranking but doesn't exclude</summary>
    Preference,
    /// <summary>Ranking signal — affects ordering only</summary>
    Ranking
}
