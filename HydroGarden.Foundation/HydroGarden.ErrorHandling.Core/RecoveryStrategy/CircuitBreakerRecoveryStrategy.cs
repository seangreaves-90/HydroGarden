using HydroGarden.ErrorHandling.Core.Common;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.ErrorHandling.Core.RecoveryStrategy;

/// <summary>
/// Recovery strategy for circuit breaker related issues.
/// </summary>
public class CircuitBreakerRecoveryStrategy(ILogger logger) : RecoveryStrategyBase(logger)
{
    /// <summary>
    /// Gets the name of this recovery strategy.
    /// </summary>
    public override string Name => "Circuit Breaker Recovery";

    /// <summary>
    /// Circuit breaker recovery is a high-priority strategy.
    /// </summary>
    public override int Priority => 15;

    /// <summary>
    /// Maximum attempts is higher for circuit breaker recovery.
    /// </summary>
    protected override int MaxRecoveryAttempts => 5;

    /// <summary>
    /// This strategy can recover from circuit breaker errors.
    /// </summary>
    public override bool CanRecover(IApplicationError error)
    {
        return error.ErrorCode == ErrorCodes.Recovery.CIRCUIT_OPEN;
    }

    /// <summary>
    /// Attempts to recover from circuit breaker by waiting for reset timeout.
    /// </summary>
    protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError error, CancellationToken ct)
    {
        try
        {
            // Wait longer to allow circuit to reset
            await Task.Delay(TimeSpan.FromSeconds(30), ct);

            // The circuit breaker itself handles the transition to half-open
            // This strategy just ensures enough time passes to allow that
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, $"Error during circuit breaker recovery for device {error.DeviceId}");
            return false;
        }
    }
}