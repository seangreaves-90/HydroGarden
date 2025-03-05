using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.RetryPolicies
{
    /// <summary>
    /// Implements an exponential backoff retry policy for failed event deliveries.
    /// </summary>
    public class ExponentialBackoffRetryPolicy : IEventRetryPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseDelay;

        public ExponentialBackoffRetryPolicy(int maxAttempts = 5, TimeSpan? baseDelay = null)
        {
            _maxAttempts = maxAttempts;
            _baseDelay = baseDelay ?? TimeSpan.FromSeconds(2);
        }

        /// <summary>
        /// Determines whether an event should be retried based on the attempt count.
        /// </summary>
        public async Task<bool> ShouldRetryAsync(IEvent evt, int attemptCount)
        {
            if (attemptCount >= _maxAttempts) return false;
            await Task.Delay((int)Math.Pow(2, attemptCount) * _baseDelay.Milliseconds);
            return true;
        }
    }
}
