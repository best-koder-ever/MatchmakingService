using Microsoft.EntityFrameworkCore;
using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Orchestrates all ICandidateFilter implementations into a single IQueryable pipeline.
/// Resolves filters from DI, sorts by Order, chains them, executes the composed query.
/// T169: Filter pipeline.
/// </summary>
public class CandidateFilterPipeline
{
    private readonly IEnumerable<ICandidateFilter> _filters;
    private readonly ILogger<CandidateFilterPipeline> _logger;

    public CandidateFilterPipeline(
        IEnumerable<ICandidateFilter> filters,
        ILogger<CandidateFilterPipeline> logger)
    {
        _filters = filters;
        _logger = logger;
    }

    /// <summary>
    /// Execute the filter pipeline against the base query.
    /// All filters are composed as IQueryable transformations — single DB roundtrip.
    /// </summary>
    public async Task<FilterPipelineResult> ExecuteAsync(
        IQueryable<UserProfile> baseQuery,
        FilterContext context,
        int limit,
        CancellationToken ct = default)
    {
        var query = baseQuery;
        var metrics = new List<FilterMetric>();
        var orderedFilters = _filters.OrderBy(f => f.Order).ToList();

        _logger.LogDebug("Executing filter pipeline with {Count} filters for user {UserId}",
            orderedFilters.Count, context.RequestingUser.UserId);

        foreach (var filter in orderedFilters)
        {
            query = filter.Apply(query, context);
            metrics.Add(new FilterMetric(filter.Name, filter.Type, filter.Order));
            _logger.LogDebug("Applied filter {FilterName} (Order: {Order}, Type: {Type})",
                filter.Name, filter.Order, filter.Type);
        }

        var candidates = await query
            .Take(limit)
            .ToListAsync(ct);

        _logger.LogDebug("Filter pipeline returned {Count} candidates (limit: {Limit})",
            candidates.Count, limit);

        return new FilterPipelineResult(candidates, metrics);
    }
}
