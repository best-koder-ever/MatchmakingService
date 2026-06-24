using MediatR;
using MatchmakingService.Common;
using MatchmakingService.Data;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Queries;

// ── DTOs ──────────────────────────────────────────────────────────────────

public record CompatibilityQuestionDto(
    int Id,
    string Category,
    string? Emoji,
    string TextEn,
    string TextSv,
    string? OptionsJson,
    double Weight,
    bool VoiceEligible);

public record GetQuestionsResult(
    List<CompatibilityQuestionDto> Questions,
    Dictionary<string, List<CompatibilityQuestionDto>> Grouped);

// ── Query ─────────────────────────────────────────────────────────────────

public record GetCompatibilityQuestionsQuery : IRequest<Result<GetQuestionsResult>>;

// ── Handler ───────────────────────────────────────────────────────────────

public class GetCompatibilityQuestionsHandler
    : IRequestHandler<GetCompatibilityQuestionsQuery, Result<GetQuestionsResult>>
{
    private readonly MatchmakingDbContext _context;

    public GetCompatibilityQuestionsHandler(MatchmakingDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GetQuestionsResult>> Handle(
        GetCompatibilityQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var questions = await _context.CompatibilityQuestions
            .Where(q => q.IsActive)
            .OrderBy(q => q.SortOrder)
            .Select(q => new CompatibilityQuestionDto(
                q.Id,
                q.Category.ToString(),
                q.Emoji,
                q.TextEn,
                q.TextSv,
                q.OptionsJson,
                q.Weight,
                q.VoiceEligible))
            .ToListAsync(cancellationToken);

        var grouped = questions
            .GroupBy(q => q.Category)
            .ToDictionary(g => g.Key, g => g.ToList());

        return Result<GetQuestionsResult>.Success(new GetQuestionsResult(questions, grouped));
    }
}
