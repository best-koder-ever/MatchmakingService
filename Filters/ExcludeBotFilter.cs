using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Controls bot visibility in candidate results.
/// - Bots requesting: only see real users (bots never see other bots)
/// - Real users requesting: CAN see bots (bots make the app feel alive)
/// Order 5: runs right after SelfExclusion (0), before ActiveUser (10).
/// </summary>
public class ExcludeBotFilter : ICandidateFilter
{
    public string Name => "ExcludeBot";
    public int Order => 5;
    public FilterType Type => FilterType.Dealbreaker;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        if (context.RequestingUser.IsBot)
        {
            // Bot requesting → exclude OTHER bots, only show real users
            return candidates.Where(c => !c.IsBot);
        }

        // Real user requesting → show everyone (including bots)
        // Bots make the app feel alive for real users
        return candidates;
    }
}
