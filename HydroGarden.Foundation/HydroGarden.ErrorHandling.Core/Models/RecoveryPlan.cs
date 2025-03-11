using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;

namespace HydroGarden.Foundation.ErrorHandling.Models
{
    public class RecoveryPlan : IRecoveryPlan
    {
        /// <inheritdoc/>
        public Guid PlanId { get; set; } = Guid.NewGuid();

        /// <inheritdoc/>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <inheritdoc/>
        public IApplicationError? Error { get; set; } = null!;

        /// <inheritdoc/>
        public List<IRecoveryStrategy> Strategies { get; set; } = new();

        /// <inheritdoc/>
        public IDictionary<string, object> Context { get; set; } = new Dictionary<string, object>();

        /// <inheritdoc/>
        public int MaxAttemptsPerStrategy { get; set; } = 3;

        /// <inheritdoc/>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <inheritdoc/>
        public bool ContinueAfterSuccess { get; set; } = false;
    }
}
