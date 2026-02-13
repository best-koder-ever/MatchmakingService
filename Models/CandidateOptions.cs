using System.ComponentModel.DataAnnotations;

namespace MatchmakingService.Models
{
    /// <summary>
    /// Configuration for the candidate delivery system.
    /// Controls strategy selection, limits, filtering defaults, background scoring, and daily picks.
    /// Bound from appsettings.json "CandidateOptions" section.
    /// Supports hot-reload via IOptionsMonitor.
    /// </summary>
    public class CandidateOptions
    {
        /// <summary>
        /// Which candidate delivery strategy to use.
        /// "Auto" = choose based on user count thresholds.
        /// "Live" = always compute on request.
        /// "PreComputed" = always read from pre-scored cache.
        /// "Hybrid" = pre-computed with live fallback.
        /// </summary>
        [Required]
        public string Strategy { get; set; } = "Auto";

        /// <summary>Default number of candidates returned per request.</summary>
        [Range(1, 200)]
        public int DefaultLimit { get; set; } = 20;

        /// <summary>Maximum candidates a client can request in one call.</summary>
        [Range(1, 500)]
        public int MaxLimit { get; set; } = 50;

        /// <summary>Default minimum compatibility score (0-100). Candidates below this are excluded.</summary>
        [Range(0, 100)]
        public double DefaultMinScore { get; set; } = 0;

        /// <summary>
        /// Only consider users active within this many days for candidate generation.
        /// Prevents showing stale/abandoned profiles.
        /// </summary>
        [Range(1, 365)]
        public int ActiveWithinDays { get; set; } = 30;

        /// <summary>Whether to default to showing only verified users.</summary>
        public bool OnlyShowVerifiedDefault { get; set; } = false;

        /// <summary>
        /// If pre-computed strategy fails, fall back to Live scoring.
        /// Disabling this means errors return empty results instead of degraded-but-slow results.
        /// </summary>
        public bool FallbackToLiveOnError { get; set; } = true;

        /// <summary>Thresholds for Auto strategy selection.</summary>
        public AutoStrategyThresholdsOptions AutoStrategyThresholds { get; set; } = new();

        /// <summary>Background scoring service configuration.</summary>
        public BackgroundScoringOptions BackgroundScoring { get; set; } = new();

        /// <summary>Daily curated picks feature configuration.</summary>
        public DailyPicksOptions DailyPicks { get; set; } = new();
    }

    /// <summary>
    /// User count thresholds for Auto strategy selection.
    /// Below LiveMaxUsers → use Live. Below PreComputedMaxUsers → use PreComputed. Above → use Hybrid.
    /// </summary>
    public class AutoStrategyThresholdsOptions
    {
        /// <summary>Max active users for Live strategy. Above this, switch to PreComputed.</summary>
        [Range(100, 1_000_000)]
        public int LiveMaxUsers { get; set; } = 10_000;

        /// <summary>Max active users for PreComputed strategy. Above this, switch to Hybrid.</summary>
        [Range(1000, 10_000_000)]
        public int PreComputedMaxUsers { get; set; } = 500_000;
    }

    /// <summary>
    /// Configuration for the background scoring service (BackgroundScoringService).
    /// Controls how often scores are refreshed, how many users per cycle, and resource guards.
    /// </summary>
    public class BackgroundScoringOptions
    {
        /// <summary>Whether background scoring is enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Minutes between scoring refresh cycles.</summary>
        [Range(5, 1440)]
        public int RefreshIntervalMinutes { get; set; } = 30;

        /// <summary>Maximum users to process per refresh cycle (prevents runaway CPU).</summary>
        [Range(1, 10_000)]
        public int MaxUsersPerCycle { get; set; } = 100;

        /// <summary>Only refresh scores for users active within ActiveWithinDays.</summary>
        public bool OnlyRefreshActiveUsers { get; set; } = true;

        /// <summary>Pre-computed scores expire after this many hours.</summary>
        [Range(1, 168)]
        public int ScoreTtlHours { get; set; } = 24;

        /// <summary>Skip scoring refresh if system CPU usage exceeds this percentage.</summary>
        [Range(10, 100)]
        public int SkipRefreshWhenCpuAbove { get; set; } = 80;

        /// <summary>Maximum concurrent scoring tasks per cycle.</summary>
        [Range(1, 50)]
        public int MaxConcurrentScoring { get; set; } = 5;
    }

    /// <summary>
    /// Configuration for the daily curated picks feature.
    /// When enabled, generates a fixed set of top-match picks per user at a scheduled time.
    /// </summary>
    public class DailyPicksOptions
    {
        /// <summary>Whether daily picks generation is enabled.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Number of top picks generated per user each day.</summary>
        [Range(1, 50)]
        public int PicksPerUser { get; set; } = 10;

        /// <summary>UTC time when daily picks are generated (24h format, e.g. "03:00").</summary>
        public string GenerationTimeUtc { get; set; } = "03:00";

        /// <summary>Hours before daily picks expire and are regenerated.</summary>
        [Range(1, 72)]
        public int ExpiryHours { get; set; } = 24;
    }
}
