using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Services;

/// <summary>
/// Manages user psychometric profiles — computing, storing, and retrieving them.
/// </summary>
public interface IPsychometricProfileService
{
    /// <summary>
    /// Computes a psychometric profile from responses and persists it.
    /// Upserts — if a profile already exists for this user, it is updated.
    /// </summary>
    Task<PsychometricProfile> ComputeAndSaveAsync(
        int userId,
        string keycloakId,
        IReadOnlyDictionary<string, int> responses,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the existing psychometric profile for a user, or null if none.
    /// </summary>
    Task<PsychometricProfile?> GetByUserIdAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets profiles for both users in a match — used by ConnectionInsightComposer.
    /// </summary>
    Task<(PsychometricProfile? ProfileA, PsychometricProfile? ProfileB)> GetPairAsync(
        int userIdA, int userIdB, CancellationToken ct = default);
}

public class PsychometricProfileService : IPsychometricProfileService
{
    private readonly MatchmakingDbContext _db;
    private readonly IPsychometricScorer _scorer;
    private readonly ILogger<PsychometricProfileService> _logger;

    public PsychometricProfileService(
        MatchmakingDbContext db,
        IPsychometricScorer scorer,
        ILogger<PsychometricProfileService> logger)
    {
        _db = db;
        _scorer = scorer;
        _logger = logger;
    }

    public async Task<PsychometricProfile> ComputeAndSaveAsync(
        int userId, string keycloakId,
        IReadOnlyDictionary<string, int> responses,
        CancellationToken ct = default)
    {
        var scores = _scorer.ComputeScores(responses);

        var existing = await _db.PsychometricProfiles
            .FirstOrDefaultAsync(pp => pp.UserId == userId, ct);

        if (existing != null)
        {
            existing.KeycloakId = keycloakId;
            existing.SocialEnergy = scores[PsychometricQuestionBank.Dimensions.SocialEnergy];
            existing.Warmth = scores[PsychometricQuestionBank.Dimensions.Warmth];
            existing.PlanningRhythm = scores[PsychometricQuestionBank.Dimensions.PlanningRhythm];
            existing.Steadiness = scores[PsychometricQuestionBank.Dimensions.Steadiness];
            existing.Curiosity = scores[PsychometricQuestionBank.Dimensions.Curiosity];
            existing.SchemaVersion = 1;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new PsychometricProfile
            {
                UserId = userId,
                KeycloakId = keycloakId,
                SocialEnergy = scores[PsychometricQuestionBank.Dimensions.SocialEnergy],
                Warmth = scores[PsychometricQuestionBank.Dimensions.Warmth],
                PlanningRhythm = scores[PsychometricQuestionBank.Dimensions.PlanningRhythm],
                Steadiness = scores[PsychometricQuestionBank.Dimensions.Steadiness],
                Curiosity = scores[PsychometricQuestionBank.Dimensions.Curiosity],
                SchemaVersion = 1,
                ComputedAt = DateTime.UtcNow,
            };
            _db.PsychometricProfiles.Add(existing);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Psychometric profile computed for user {UserId}: SE={SE} WA={WA} PR={PR} ST={ST} CU={CU}",
            userId,
            existing.SocialEnergy, existing.Warmth, existing.PlanningRhythm,
            existing.Steadiness, existing.Curiosity);

        return existing;
    }

    public async Task<PsychometricProfile?> GetByUserIdAsync(int userId, CancellationToken ct = default)
        => await _db.PsychometricProfiles.FirstOrDefaultAsync(pp => pp.UserId == userId, ct);

    public async Task<(PsychometricProfile? ProfileA, PsychometricProfile? ProfileB)> GetPairAsync(
        int userIdA, int userIdB, CancellationToken ct = default)
    {
        var profiles = await _db.PsychometricProfiles
            .Where(pp => pp.UserId == userIdA || pp.UserId == userIdB)
            .ToListAsync(ct);

        return (
            profiles.FirstOrDefault(p => p.UserId == userIdA),
            profiles.FirstOrDefault(p => p.UserId == userIdB)
        );
    }
}
