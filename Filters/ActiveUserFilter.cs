using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>Order 10: Only active (non-deactivated/deleted) accounts.</summary>
public class ActiveUserFilter : ICandidateFilter
{
    public string Name => "ActiveUser";
    public int Order => 10;
    public FilterType Type => FilterType.Dealbreaker;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        return candidates.Where(c => c.IsActive);
    }
}
