using MatchmakingService.Models;
using Microsoft.Extensions.Options;

namespace MatchmakingService.Services
{
    public interface IDailySuggestionTracker
    {
        Task<(bool allowed, int remaining)> CheckAndIncrementAsync(int userId, bool isPremium);
        Task ResetIfNeededAsync(int userId);
        Task<DailySuggestionStatus> GetStatusAsync(int userId, bool isPremium);
    }

    public class DailySuggestionStatus
    {
        public int SuggestionsShownToday { get; set; }
        public int MaxDailySuggestions { get; set; }
        public int SuggestionsRemaining { get; set; }
        public DateTime LastResetDate { get; set; }
        public DateTime NextResetDate { get; set; }
        public bool QueueExhausted { get; set; }
    }

    public class InMemoryDailySuggestionTracker : IDailySuggestionTracker
    {
        private readonly Dictionary<int, UserDailySuggestionState> _state = new();
        private readonly DailySuggestionLimits _limits;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public InMemoryDailySuggestionTracker(IOptions<DailySuggestionLimits> limits)
        {
            _limits = limits.Value;
        }

        public async Task<(bool allowed, int remaining)> CheckAndIncrementAsync(int userId, bool isPremium)
        {
            await _lock.WaitAsync();
            try
            {
                await ResetIfNeededAsync(userId);

                if (!_state.TryGetValue(userId, out var state))
                {
                    state = new UserDailySuggestionState
                    {
                        UserId = userId,
                        LastResetDate = DateTime.UtcNow,
                        SuggestionsShownToday = 0,
                        QueueExhausted = false
                    };
                    _state[userId] = state;
                }

                var maxAllowed = isPremium ? _limits.PremiumMaxDailySuggestions : _limits.MaxDailySuggestions;
                var remaining = maxAllowed - state.SuggestionsShownToday;

                if (state.SuggestionsShownToday >= maxAllowed)
                {
                    return (false, 0);
                }

                state.SuggestionsShownToday++;
                return (true, remaining - 1);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ResetIfNeededAsync(int userId)
        {
            if (_state.TryGetValue(userId, out var state))
            {
                var hoursSinceReset = (DateTime.UtcNow - state.LastResetDate).TotalHours;
                if (hoursSinceReset >= _limits.RefreshIntervalHours)
                {
                    state.SuggestionsShownToday = 0;
                    state.QueueExhausted = false;
                    state.LastResetDate = DateTime.UtcNow;
                }
            }
            await Task.CompletedTask;
        }

        public async Task<DailySuggestionStatus> GetStatusAsync(int userId, bool isPremium)
        {
            await _lock.WaitAsync();
            try
            {
                await ResetIfNeededAsync(userId);

                if (!_state.TryGetValue(userId, out var state))
                {
                    state = new UserDailySuggestionState
                    {
                        UserId = userId,
                        LastResetDate = DateTime.UtcNow,
                        SuggestionsShownToday = 0,
                        QueueExhausted = false
                    };
                }

                var maxAllowed = isPremium ? _limits.PremiumMaxDailySuggestions : _limits.MaxDailySuggestions;
                var remaining = Math.Max(0, maxAllowed - state.SuggestionsShownToday);
                var nextReset = state.LastResetDate.AddHours(_limits.RefreshIntervalHours);

                return new DailySuggestionStatus
                {
                    SuggestionsShownToday = state.SuggestionsShownToday,
                    MaxDailySuggestions = maxAllowed,
                    SuggestionsRemaining = remaining,
                    LastResetDate = state.LastResetDate,
                    NextResetDate = nextReset,
                    QueueExhausted = state.QueueExhausted
                };
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
