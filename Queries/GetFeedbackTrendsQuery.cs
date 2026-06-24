using MediatR;
using MatchmakingService.Common;
using MatchmakingService.Data;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Queries;

public record FeedbackTrendsResult(
    int TotalFeedbacks,
    double AvgOverallRating,
    double AvgChemistryRating,
    double AvgConversationRating,
    int WouldMeetAgainCount,
    double? PreviousAvgOverall,
    double ImprovementPercent);

public record GetFeedbackTrendsQuery(string KeycloakId)
    : IRequest<Result<FeedbackTrendsResult>>;

public class GetFeedbackTrendsHandler
    : IRequestHandler<GetFeedbackTrendsQuery, Result<FeedbackTrendsResult>>
{
    private readonly MatchmakingDbContext _context;

    public GetFeedbackTrendsHandler(MatchmakingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<FeedbackTrendsResult>> Handle(
        GetFeedbackTrendsQuery request, CancellationToken ct)
    {
        var allFeedbacks = await _context.PostDateFeedbacks
            .Where(f => f.KeycloakId == request.KeycloakId)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);

        if (allFeedbacks.Count == 0)
            return Result<FeedbackTrendsResult>.Success(new FeedbackTrendsResult(
                0, 0, 0, 0, 0, null, 0));

        var currentAvg = allFeedbacks.Average(f => f.OverallRating);
        var wouldMeetAgain = allFeedbacks.Count(f => f.WouldMeetAgain);

        // Compare first half vs second half for trend
        double? prevAvg = null;
        double improvement = 0;

        if (allFeedbacks.Count >= 4)
        {
            var half = allFeedbacks.Count / 2;
            var firstHalf = allFeedbacks.Take(half).Average(f => f.OverallRating);
            var secondHalf = allFeedbacks.Skip(half).Average(f => f.OverallRating);
            prevAvg = firstHalf;
            improvement = firstHalf > 0 ? ((secondHalf - firstHalf) / firstHalf) * 100 : 0;
        }

        return Result<FeedbackTrendsResult>.Success(new FeedbackTrendsResult(
            allFeedbacks.Count,
            Math.Round(currentAvg, 1),
            Math.Round(allFeedbacks.Average(f => f.ChemistryRating), 1),
            Math.Round(allFeedbacks.Average(f => f.ConversationRating), 1),
            wouldMeetAgain,
            prevAvg.HasValue ? Math.Round(prevAvg.Value, 1) : null,
            Math.Round(improvement, 0)));
    }
}
