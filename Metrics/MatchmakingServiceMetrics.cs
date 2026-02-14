using System.Diagnostics.Metrics;

namespace MatchmakingService.Metrics;

public sealed class MatchmakingServiceMetrics
{
    public const string MeterName = "MatchmakingService";

    private readonly Counter<long> _matchesCreated;
    private readonly Counter<long> _candidatesEvaluated;
    private readonly Histogram<double> _matchScore;
    private readonly Histogram<double> _algorithmDuration;

    public MatchmakingServiceMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _matchesCreated = meter.CreateCounter<long>("matches_created_total",
            description: "Total number of matches created");
        _candidatesEvaluated = meter.CreateCounter<long>("candidates_evaluated_total",
            description: "Total number of candidates evaluated for matching");
        _matchScore = meter.CreateHistogram<double>("match_score_value",
            description: "Distribution of match compatibility scores");
        _algorithmDuration = meter.CreateHistogram<double>("match_algorithm_duration_ms",
            unit: "ms",
            description: "Duration of match algorithm execution in milliseconds");
    }

    public void MatchCreated() => _matchesCreated.Add(1);
    public void CandidateEvaluated() => _candidatesEvaluated.Add(1);
    public void CandidatesEvaluated(int count) => _candidatesEvaluated.Add(count);
    public void RecordMatchScore(double score) => _matchScore.Record(score);
    public void RecordAlgorithmDuration(double ms) => _algorithmDuration.Record(ms);
}
