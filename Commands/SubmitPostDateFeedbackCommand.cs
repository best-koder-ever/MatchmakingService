using MediatR;
using MatchmakingService.Common;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MatchmakingService.Commands;

// ── DTOs ─────────────────────────────────────────────────────────────────

public record SubmitFeedbackRequest(
    int OverallRating,
    int ChemistryRating,
    int ConversationRating,
    bool WouldMeetAgain,
    string? FreeformReflection = null);

public record SubmitFeedbackResult(int FeedbackId);

// ── Command ───────────────────────────────────────────────────────────────

public record SubmitPostDateFeedbackCommand(
    string KeycloakId,
    string MatchId,
    int OverallRating,
    int ChemistryRating,
    int ConversationRating,
    bool WouldMeetAgain,
    string? FreeformReflection) : IRequest<Result<SubmitFeedbackResult>>;

// ── Handler ───────────────────────────────────────────────────────────────

public class SubmitPostDateFeedbackHandler
    : IRequestHandler<SubmitPostDateFeedbackCommand, Result<SubmitFeedbackResult>>
{
    private readonly MatchmakingDbContext _context;
    private readonly IRadarProfileCalculator _radar;
    private readonly ILogger<SubmitPostDateFeedbackHandler> _logger;

    public SubmitPostDateFeedbackHandler(
        MatchmakingDbContext context,
        IRadarProfileCalculator radar,
        ILogger<SubmitPostDateFeedbackHandler> logger)
    {
        _context = context;
        _radar = radar;
        _logger = logger;
    }

    public async Task<Result<SubmitFeedbackResult>> Handle(
        SubmitPostDateFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        // Validate ratings
        if (request.OverallRating < 1 || request.OverallRating > 5 ||
            request.ChemistryRating < 1 || request.ChemistryRating > 5 ||
            request.ConversationRating < 1 || request.ConversationRating > 5)
            return Result<SubmitFeedbackResult>.Failure("Ratings must be between 1 and 5.", "INVALID_RATING");

        // One feedback per user per match
        var existing = await _context.PostDateFeedbacks
            .FirstOrDefaultAsync(
                f => f.KeycloakId == request.KeycloakId && f.MatchId == request.MatchId,
                cancellationToken);
        if (existing != null)
            return Result<SubmitFeedbackResult>.Failure("Feedback already submitted for this match.", "DUPLICATE_FEEDBACK");

        var feedback = new PostDateFeedback
        {
            KeycloakId = request.KeycloakId,
            MatchId = request.MatchId,
            OverallRating = request.OverallRating,
            ChemistryRating = request.ChemistryRating,
            ConversationRating = request.ConversationRating,
            WouldMeetAgain = request.WouldMeetAgain,
            FreeformReflection = request.FreeformReflection,
            CreatedAt = DateTime.UtcNow
        };

        _context.PostDateFeedbacks.Add(feedback);
        await _context.SaveChangesAsync(cancellationToken);

        // T622: Trigger radar recalibration (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var profile = await _context.RadarProfiles
                    .FirstOrDefaultAsync(r => r.KeycloakId == request.KeycloakId);

                if (profile != null)
                    FeedbackRadarRecalibrator.Apply(profile, request.OverallRating,
                        request.ChemistryRating, request.ConversationRating);
                else
                    profile = await _radar.ComputeAndSaveAsync(request.KeycloakId);

                if (profile != null)
                {
                    _context.RadarProfiles.Update(profile);
                    await _context.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Radar recalibration failed after feedback for {Id}", request.KeycloakId);
            }
        }, CancellationToken.None);

        return Result<SubmitFeedbackResult>.Success(new SubmitFeedbackResult(feedback.Id));
    }
}
