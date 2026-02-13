using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Order 20: Bidirectional gender match.
/// Candidate's gender matches user's preference AND user's gender matches candidate's preference.
/// "Everyone" preference matches all genders.
/// </summary>
public class GenderFilter : ICandidateFilter
{
    public string Name => "Gender";
    public int Order => 20;
    public FilterType Type => FilterType.Dealbreaker;

    private static readonly HashSet<string> EveryoneValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Everyone", "All", "Any", ""
    };

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        var user = context.RequestingUser;
        var userGender = user.Gender;
        var userPreference = user.PreferredGender;

        // Apply user's preference → filter candidate's gender
        if (!EveryoneValues.Contains(userPreference))
        {
            candidates = candidates.Where(c => c.Gender == userPreference);
        }

        // Apply candidate's preference → must include user's gender (bidirectional)
        candidates = candidates.Where(c =>
            c.PreferredGender == "Everyone" ||
            c.PreferredGender == "All" ||
            c.PreferredGender == "Any" ||
            c.PreferredGender == "" ||
            c.PreferredGender == userGender);

        return candidates;
    }
}
