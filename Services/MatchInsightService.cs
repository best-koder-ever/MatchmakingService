using System.Text.Json;
using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Services
{
    /// <summary>
    /// Generates and persists <see cref="MatchInsight"/> rows for a newly created match
    /// (spec 005 T532/T533). Asymmetric per-user storage — one row per side so that
    /// future viewer-specific framing has a stable schema to land on.
    ///
    /// T540+: After scoring compatibility, also composes a connection insight via
    /// <see cref="IConnectionInsightComposer"/> using psychometric profiles and profile facts.
    /// </summary>
    public interface IMatchInsightService
    {
        /// <summary>
        /// Generates insight rows for both users in the match. Idempotent — existing
        /// rows for the (matchId, keycloakId) pair are left alone. Never throws on the
        /// happy path; logs and swallows on errors since insight is a soft enrichment.
        /// </summary>
        Task GenerateForMatchAsync(int matchId, int user1Id, int user2Id, double? fallbackScore, CancellationToken ct = default);
    }

    public class MatchInsightService : IMatchInsightService
    {
        private readonly MatchmakingDbContext _db;
        private readonly ICompatibilityScorer _scorer;
        private readonly IConnectionInsightComposer _composer;
        private readonly IPsychometricProfileService _profileService;
        private readonly ILogger<MatchInsightService> _logger;

        public MatchInsightService(
            MatchmakingDbContext db,
            ICompatibilityScorer scorer,
            IConnectionInsightComposer composer,
            IPsychometricProfileService profileService,
            ILogger<MatchInsightService> logger)
        {
            _db = db;
            _scorer = scorer;
            _composer = composer;
            _profileService = profileService;
            _logger = logger;
        }

        public async Task GenerateForMatchAsync(
            int matchId, int user1Id, int user2Id, double? fallbackScore, CancellationToken ct = default)
        {
            try
            {
                var profiles = await _db.UserProfiles
                    .Where(p => p.UserId == user1Id || p.UserId == user2Id)
                    .Select(p => new { p.UserId, p.KeycloakId })
                    .ToListAsync(ct);

                var kc1 = profiles.FirstOrDefault(p => p.UserId == user1Id)?.KeycloakId;
                var kc2 = profiles.FirstOrDefault(p => p.UserId == user2Id)?.KeycloakId;

                if (string.IsNullOrWhiteSpace(kc1) || string.IsNullOrWhiteSpace(kc2))
                {
                    _logger.LogDebug(
                        "Skip MatchInsight generation for match {MatchId}: missing KeycloakId (u1={U1} u2={U2})",
                        matchId, user1Id, user2Id);
                    return;
                }

                var compat = await _scorer.ScoreAsync(kc1!, kc2!, ct);

                // T540+: Fetch profile facts for connection insight composer
                var userProfiles = await _db.UserProfiles
                    .Where(up => up.UserId == user1Id || up.UserId == user2Id)
                    .ToListAsync(ct);
                var up1 = userProfiles.FirstOrDefault(up => up.UserId == user1Id);
                var up2 = userProfiles.FirstOrDefault(up => up.UserId == user2Id);

                // Fetch psychometric profiles
                var (psychA, psychB) = await _profileService.GetPairAsync(user1Id, user2Id, ct);

                // Compute connection insight (non-fatal)
                var insightResult = await _composer.ComposeAsync(
                    matchId, compat,
                    psychA, psychB,
                    ParseJsonList(up1?.Interests),
                    ParseJsonList(up2?.Interests),
                    new List<string>(), // Languages not on local UserProfile
                    new List<string>(), // Languages not on local UserProfile
                    up1?.City, up2?.City,
                    up1?.Occupation, up2?.Occupation,
                    ct);

                var overall = compat.SharedAnswerCount > 0 ? compat.OverallScore : (fallbackScore ?? 50.0);

                var reasonsJson = JsonSerializer.Serialize(compat.TopReasons);
                var frictionJson = JsonSerializer.Serialize(compat.FrictionPoints);
                const string growthJson = "[]";

                // Look up existing rows so we can update them (regeneration) or skip (initial creation).
                var existingRows = await _db.MatchInsights
                    .Where(mi => mi.MatchId == matchId && (mi.ForKeycloakId == kc1 || mi.ForKeycloakId == kc2))
                    .ToListAsync(ct);
                var existingKeycloakIds = existingRows.Select(mi => mi.ForKeycloakId).ToHashSet();

                foreach (var kc in new[] { kc1!, kc2! })
                {
                    if (existingKeycloakIds.Contains(kc))
                    {
                        // Update existing row (used by regenerate-insight endpoint).
                        var row = existingRows.First(mi => mi.ForKeycloakId == kc);
                        row.ReasonsJson = reasonsJson;
                        row.FrictionJson = frictionJson;
                        row.GrowthJson = growthJson;
                        row.OverallScore = overall;
                        if (insightResult != null)
                        {
                            row.ConnectionHookJson = insightResult.HookJson;
                            row.ConnectionSignalsJson = insightResult.SignalsJson;
                            row.ConfidenceLevel = insightResult.ConfidenceLevel;
                        }
                    }
                    else
                    {
                        // Create new row.
                        var insight = new MatchInsight
                        {
                            MatchId = matchId,
                            ForKeycloakId = kc,
                            ReasonsJson = reasonsJson,
                            FrictionJson = frictionJson,
                            GrowthJson = growthJson,
                            OverallScore = overall,
                            CreatedAt = DateTime.UtcNow,
                        };

                        if (insightResult != null)
                        {
                            insight.ConnectionHookJson = insightResult.HookJson;
                            insight.ConnectionSignalsJson = insightResult.SignalsJson;
                            insight.ConfidenceLevel = insightResult.ConfidenceLevel;
                        }

                        _db.MatchInsights.Add(insight);
                    }
                }

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "MatchInsight rows generated for match {MatchId}: score={Score:F1} shared={Shared} reasons={Reasons} frictions={Frictions} confidence={Confidence}",
                    matchId, overall, compat.SharedAnswerCount, compat.TopReasons.Count, compat.FrictionPoints.Count,
                    insightResult?.ConfidenceLevel ?? "none");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MatchInsight generation failed for match {MatchId} — non-fatal", matchId);
            }
        }

        /// <summary>
        /// Parses a JSON array string (e.g. from UserProfile.Languages) into a list.
        /// </summary>
        private static List<string> ParseJsonList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(json);
                return parsed ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
