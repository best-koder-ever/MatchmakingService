using MediatR;
using MatchmakingService.Common;
using MatchmakingService.Data;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Queries;

// ── DTOs ──────────────────────────────────────────────────────────────────

public record UserAnswerDto(int QuestionId, int Value, string? AnswerType, DateTime AnsweredAt);

public record GetUserAnswersResult(string KeycloakId, List<UserAnswerDto> Answers, int Count);

// ── Query ─────────────────────────────────────────────────────────────────

public record GetUserAnswersQuery(string KeycloakId) : IRequest<Result<GetUserAnswersResult>>;

// ── Handler ───────────────────────────────────────────────────────────────

public class GetUserAnswersHandler
    : IRequestHandler<GetUserAnswersQuery, Result<GetUserAnswersResult>>
{
    private readonly MatchmakingDbContext _context;

    public GetUserAnswersHandler(MatchmakingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GetUserAnswersResult>> Handle(
        GetUserAnswersQuery request,
        CancellationToken cancellationToken)
    {
        var answers = await _context.UserQuestionAnswers
            .Where(a => a.KeycloakId == request.KeycloakId)
            .OrderBy(a => a.QuestionId)
            .Select(a => new UserAnswerDto(a.QuestionId, a.Value, a.AnswerType, a.AnsweredAt))
            .ToListAsync(cancellationToken);

        return Result<GetUserAnswersResult>.Success(
            new GetUserAnswersResult(request.KeycloakId, answers, answers.Count));
    }
}
