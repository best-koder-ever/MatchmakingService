using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>Order 0: Exclude requesting user's own profile.</summary>
public class SelfExclusionFilter : ICandidateFilter
{
    public string Name => "SelfExclusion";
    public int Order => 0;
    public FilterType Type => FilterType.Dealbreaker;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        var userId = context.RequestingUser.UserId;
        return candidates.Where(c => c.UserId != userId);
    }
}
