using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Excludes bot profiles from candidate results for real users.
/// Bots should only match with real humans, never with each other.
/// If the requesting user IS a bot, this filter is skipped (bots need candidates).
/// Order 5: runs right after SelfExclusion (0), before ActiveUser (10).
/// </summary>
public class ExcludeBotFilter : ICandidateFilter
{
    public string Name => "ExcludeBot";
    public int Order => 5;
    public FilterType Type => FilterType.Dealbreaker;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        // If the requesting user is a bot, don't filter out other bots
        // (bots need real users as candidates, not other bots)
        if (context.RequestingUser.IsBot)
        {
            // Bot requesting → exclude OTHER bots, only show real users
            return candidates.Where(c => !c.IsBot);
        }

        // Real user requesting → also exclude bots from their candidate pool
        // (bots should organically swipe on real users, not appear in discover)
        return candidates.Where(c => !c.IsBot);
    }
}
