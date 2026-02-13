using MatchmakingService.Models;

namespace MatchmakingService.Filters;

/// <summary>
/// Order 60: Distance filter using bounding-box pre-filter.
/// Uses a square approximation (lat/lon bounding box) that IS translatable to SQL.
/// The Haversine formula may not translate in all EF Core providers, so we use
/// the bounding box for the DB query which is fast and indexable, then optionally
/// refine with Haversine in memory if needed (but for MVP, box is sufficient).
/// </summary>
public class DistanceFilter : ICandidateFilter
{
    public string Name => "Distance";
    public int Order => 60;
    public FilterType Type => FilterType.Dealbreaker;

    // 1 degree latitude ≈ 111 km
    private const double KmPerDegreeLat = 111.0;

    public IQueryable<UserProfile> Apply(IQueryable<UserProfile> candidates, FilterContext context)
    {
        var user = context.RequestingUser;
        var maxDistanceKm = user.MaxDistance;

        if (maxDistanceKm <= 0)
            return candidates;

        // Bounding box approximation
        var latDelta = maxDistanceKm / KmPerDegreeLat;
        // Longitude degrees vary by latitude: 1° lon ≈ 111 * cos(lat) km
        var lonDelta = maxDistanceKm / (KmPerDegreeLat * Math.Cos(user.Latitude * Math.PI / 180.0));

        var minLat = user.Latitude - latDelta;
        var maxLat = user.Latitude + latDelta;
        var minLon = user.Longitude - lonDelta;
        var maxLon = user.Longitude + lonDelta;

        return candidates.Where(c =>
            c.Latitude >= minLat && c.Latitude <= maxLat &&
            c.Longitude >= minLon && c.Longitude <= maxLon);
    }
}
