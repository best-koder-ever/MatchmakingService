using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Order 30: Bidirectional age range match.
/// Candidate's age within user's [MinAge, MaxAge] AND user's age within candidate's [MinAge, MaxAge].
/// Prevents 50yo seeing 20yo who set max age to 30.
/// </summary>
public class AgeRangeFilter : ICandidateFilter
{
    public string Name => "AgeRange";
    public int Order => 30;
    public FilterType Type => FilterType.Dealbreaker;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        var user = context.RequestingUser;

        // Candidate's age within user's range
        candidates = candidates.Where(c =>
            c.Age >= user.MinAge && c.Age <= user.MaxAge);

        // User's age within candidate's range (bidirectional)
        candidates = candidates.Where(c =>
            user.Age >= c.MinAge && user.Age <= c.MaxAge);

        return candidates;
    }
}
