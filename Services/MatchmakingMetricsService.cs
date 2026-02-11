using System.Diagnostics.Metrics;

namespace MatchmakingService.Services
{
    /// <summary>
    /// T069: Matchmaking Metrics
    /// Tracks: API Latency, Match Rate, Queue Size, Algorithm Performance
    /// </summary>
    public class MatchmakingMetricsService
    {
        private static readonly Meter _meter = new("MatchmakingService", "1.0.0");

        // API Performance Metrics
        private static readonly Histogram<double> _apiLatency = _meter.CreateHistogram<double>(
            "matchmaking_api_latency_ms",
            unit: "milliseconds",
            description: "API request processing time");

        private static readonly Counter<long> _apiRequests = _meter.CreateCounter<long>(
            "matchmaking_api_requests_total",
            description: "Total API requests processed");

        private static readonly Counter<long> _apiErrors = _meter.CreateCounter<long>(
            "matchmaking_api_errors_total",
            description: "Total API errors encountered");

        // Match Quality Metrics
        private static readonly Counter<long> _matchesCreated = _meter.CreateCounter<long>(
            "matchmaking_matches_created_total",
            description: "Total matches created");

        private static readonly Histogram<double> _matchScore = _meter.CreateHistogram<double>(
            "matchmaking_match_score",
            description: "Compatibility scores of created matches");

        private static readonly Counter<long> _matchAccepted = _meter.CreateCounter<long>(
            "matchmaking_match_accepted_total",
            description: "Matches that led to conversation");

        private static readonly Counter<long> _matchRejected = _meter.CreateCounter<long>(
            "matchmaking_match_rejected_total",
            description: "Matches that were rejected");

        // Queue and Processing Metrics
        private static long _queueSize = 0;
        private static readonly ObservableGauge<long> _queueSizeGauge = _meter.CreateObservableGauge(
            "matchmaking_queue_size",
            () => _queueSize,
            description: "Current matchmaking queue size");

        private static long _activeMatches = 0;
        private static readonly ObservableGauge<long> _activeMatchesGauge = _meter.CreateObservableGauge(
            "matchmaking_active_matches",
            () => _activeMatches,
            description: "Current number of active matches");

        private static readonly Histogram<double> _processingTime = _meter.CreateHistogram<double>(
            "matchmaking_processing_time_ms",
            unit: "milliseconds",
            description: "Time to compute match suggestions");

        // Algorithm Performance
        private static readonly Counter<long> _cacheHits = _meter.CreateCounter<long>(
            "matchmaking_cache_hits_total",
            description: "Score cache hits");

        private static readonly Counter<long> _cacheMisses = _meter.CreateCounter<long>(
            "matchmaking_cache_misses_total",
            description: "Score cache misses");

        private static readonly Counter<long> _suggestionsGenerated = _meter.CreateCounter<long>(
            "matchmaking_suggestions_generated_total",
            description: "Total match suggestions generated");

        private static readonly Histogram<int> _candidatesEvaluated = _meter.CreateHistogram<int>(
            "matchmaking_candidates_evaluated",
            description: "Number of candidates evaluated per request");

        // Public methods
        public void RecordApiRequest(string endpoint, double latencyMs, bool success)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("status", success ? "success" : "error")
            };

            _apiRequests.Add(1, tags);
            _apiLatency.Record(latencyMs, tags);

            if (!success)
                _apiErrors.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));
        }

        public void RecordMatchCreated(double compatibilityScore, string matchType = "mutual_like")
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("type", matchType),
                new KeyValuePair<string, object?>("score_range", GetScoreRange(compatibilityScore))
            };

            _matchesCreated.Add(1, tags);
            _matchScore.Record(compatibilityScore);
            Interlocked.Increment(ref _activeMatches);
        }

        public void RecordMatchOutcome(bool accepted, double compatibilityScore)
        {
            var scoreRange = GetScoreRange(compatibilityScore);
            var tags = new KeyValuePair<string, object?>("score_range", scoreRange);

            if (accepted)
                _matchAccepted.Add(1, tags);
            else
                _matchRejected.Add(1, tags);
        }

        public void RecordMatchEnded()
        {
            Interlocked.Decrement(ref _activeMatches);
        }

        public void RecordProcessingMetrics(int candidatesEvaluated, int suggestionsReturned, double processingMs)
        {
            _candidatesEvaluated.Record(candidatesEvaluated);
            _suggestionsGenerated.Add(suggestionsReturned);
            _processingTime.Record(processingMs);
        }

        public void RecordCacheOperation(bool isHit)
        {
            if (isHit)
                _cacheHits.Add(1);
            else
                _cacheMisses.Add(1);
        }

        public void UpdateQueueSize(long size)
        {
            Interlocked.Exchange(ref _queueSize, size);
        }

        private static string GetScoreRange(double score) => score switch
        {
            >= 90 => "90-100",
            >= 80 => "80-89",
            >= 70 => "70-79",
            >= 60 => "60-69",
            >= 50 => "50-59",
            _ => "0-49"
        };
    }
}
