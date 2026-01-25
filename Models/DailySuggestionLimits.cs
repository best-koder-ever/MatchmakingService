namespace MatchmakingService.Models;

public class DailySuggestionLimits
{
    public int MaxDailySuggestions { get; set; } = 50;  // Free tier: 50 profiles/day
    public int RefreshIntervalHours { get; set; } = 24;
    public int PremiumMaxDailySuggestions { get; set; } = 150;  // Premium: 150 profiles/day
    public bool EnableQueueExpansion { get; set; } = true;  // Expand criteria when queue exhausted
}

public class UserDailySuggestionState
{
    public int UserId { get; set; }
    public DateTime LastResetDate { get; set; }
    public int SuggestionsShownToday { get; set; }
    public bool QueueExhausted { get; set; }
}
