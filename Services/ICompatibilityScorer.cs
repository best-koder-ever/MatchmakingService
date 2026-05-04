using MatchmakingService.DTOs;
using MatchmakingService.Models;

namespace MatchmakingService.Services;

public interface ICompatibilityScorer
{
    CompatibilityScoreDto Score(
        string userId1,
        string userId2,
        IReadOnlyList<CompatibilityAnswer> answers1,
        IReadOnlyList<CompatibilityAnswer> answers2);
}
