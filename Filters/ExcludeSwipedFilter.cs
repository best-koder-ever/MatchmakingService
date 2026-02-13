using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Order 40: Exclude users already swiped on.
/// EF Core translates Contains on HashSet to NOT IN (...) or NOT EXISTS.
/// </summary>
public class ExcludeSwipedFilter : ICandidateFilter
{
    public string Name => "ExcludeSwiped";
    public int Order => 40;
    public FilterType Type => FilterType.Dealbreaker;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        if (context.SwipedUserIds.Count == 0)
            return candidates;

        var swipedIds = context.SwipedUserIds;
        return candidates.Where(c => !swipedIds.Contains(c.UserId));
    }
}
