namespace MatchmakingService.DTOs
{
    /// <summary>
    /// Response for daily suggestion limit status
    /// </summary>
    public class DailySuggestionStatusResponse
    {
        public int SuggestionsShownToday { get; set; }
        public int MaxDailySuggestions { get; set; }
        public int SuggestionsRemaining { get; set; }
        public DateTime LastResetDate { get; set; }
        public DateTime NextResetDate { get; set; }
        public bool QueueExhausted { get; set; }
        public bool IsPremium { get; set; }
        public string Tier { get; set; } = "free";
    }

    /// <summary>
    /// Enhanced find matches response with limit tracking
    /// </summary>
    public class FindMatchesResponse
    {
        public List<MatchSuggestionResponse> Matches { get; set; } = new();
        public int Count { get; set; }
        public string RequestId { get; set; } = string.Empty;
        
        // Daily limit tracking
        public int SuggestionsRemaining { get; set; }
        public bool DailyLimitReached { get; set; }
        public DateTime? NextResetAt { get; set; }
        
        // Queue status
        public bool QueueExhausted { get; set; }
        public string? Message { get; set; }
    }
}
