using MediatR;
using MatchmakingService.Common;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MatchmakingService.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────

public record AnswerItemDto(int QuestionId, int Value, string? AnswerType = "tap");

public record SubmitAnswersResult(int Saved, int TotalAnswered);

// ── Command ───────────────────────────────────────────────────────────────

public record SubmitAnswersCommand(
    string KeycloakId,
    List<AnswerItemDto> Answers) : IRequest<Result<SubmitAnswersResult>>;

// ── Handler ───────────────────────────────────────────────────────────────

public class SubmitAnswersHandler
    : IRequestHandler<SubmitAnswersCommand, Result<SubmitAnswersResult>>
{
    private readonly MatchmakingDbContext _context;
    private readonly IRadarProfileCalculator _radar;
    private readonly ILogger<SubmitAnswersHandler> _logger;

    public SubmitAnswersHandler(
        MatchmakingDbContext context,
        IRadarProfileCalculator radar,
        ILogger<SubmitAnswersHandler> logger)
    {
        _context = context;
        _radar = radar;
        _logger = logger;
    }

    public async Task<Result<SubmitAnswersResult>> Handle(
        SubmitAnswersCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Answers.Count == 0)
            return Result<SubmitAnswersResult>.Failure("At least one answer is required.", "EMPTY_ANSWERS");

        // Validate question IDs
        var questionIds = request.Answers.Select(a => a.QuestionId).Distinct().ToList();
        var validIds = await _context.CompatibilityQuestions
            .Where(q => questionIds.Contains(q.Id) && q.IsActive)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);
        var invalidIds = questionIds.Except(validIds).ToList();
        if (invalidIds.Count > 0)
            return Result<SubmitAnswersResult>.Failure($"Unknown question IDs: {string.Join(", ", invalidIds)}", "INVALID_QUESTIONS");

        // Validate value range
        var outOfRange = request.Answers.Where(a => a.Value < 1 || a.Value > 7).Select(a => a.QuestionId).ToList();
        if (outOfRange.Count > 0)
            return Result<SubmitAnswersResult>.Failure($"Answer values must be between 1 and 7 for questions: {string.Join(", ", outOfRange)}", "OUT_OF_RANGE");

        // Upsert answers
        foreach (var item in request.Answers)
        {
            var existing = await _context.UserQuestionAnswers
                .FirstOrDefaultAsync(
                    a => a.KeycloakId == request.KeycloakId && a.QuestionId == item.QuestionId,
                    cancellationToken);
            if (existing != null)
            {
                existing.Value = item.Value;
                existing.AnsweredAt = DateTime.UtcNow;
                existing.AnswerType = item.AnswerType ?? "tap";
            }
            else
            {
                _context.UserQuestionAnswers.Add(new UserQuestionAnswer
                {
                    KeycloakId = request.KeycloakId,
                    QuestionId = item.QuestionId,
                    Value = item.Value,
                    AnsweredAt = DateTime.UtcNow,
                    AnswerType = item.AnswerType ?? "tap"
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var total = await _context.UserQuestionAnswers
            .CountAsync(a => a.KeycloakId == request.KeycloakId, cancellationToken);

        // T588: auto-refresh radar after answers submitted (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try { await _radar.ComputeAndSaveAsync(request.KeycloakId); }
            catch (Exception ex) { _logger.LogWarning(ex, "Radar auto-update failed for {Id}", request.KeycloakId); }
        }, CancellationToken.None);

        return Result<SubmitAnswersResult>.Success(new SubmitAnswersResult(request.Answers.Count, total));
    }
}
