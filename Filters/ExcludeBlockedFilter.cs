using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Order 50: Exclude blocked users (both directions).
/// </summary>
public class ExcludeBlockedFilter : ICandidateFilter
{
    public string Name => "ExcludeBlocked";
    public int Order => 50;
    public FilterType Type => FilterType.Dealbreaker;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        if (context.BlockedUserIds.Count == 0)
            return candidates;

        var blockedIds = context.BlockedUserIds;
        return candidates.Where(c => !blockedIds.Contains(c.UserId));
    }
}
