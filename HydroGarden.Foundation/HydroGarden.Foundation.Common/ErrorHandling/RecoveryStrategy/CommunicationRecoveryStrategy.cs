using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.ErrorHandling.Constants;

namespace HydroGarden.Foundation.Common.ErrorHandling.RecoveryStrategy;

/// <summary>
/// Recovery strategy for communication-related issues.
/// </summary>
public class CommunicationRecoveryStrategy(ILogger logger) : RecoveryStrategyBase(logger)
{
    /// <summary>
    /// Gets the name of this recovery strategy.
    /// </summary>
    public override string Name => "Communication Recovery";

    /// <summary>
    /// Communication recovery is a medium-priority strategy.
    /// </summary>
    public override int Priority => 30;

    /// <summary>
    /// This strategy can recover from communication errors.
    /// </summary>
    public override bool CanRecover(IApplicationError error)
    {
        return error.Source == ErrorSource.Communication ||
               error.ErrorCode?.StartsWith("COMM_") == true ||
               error.ErrorCode == ErrorCodes.Device.COMMUNICATION_LOST;
    }

    /// <summary>
    /// Attempts to recover communication by waiting and retrying.
    /// </summary>
    protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError error, CancellationToken ct)
    {
        // Communication recovery often just needs a pause before retrying
        try
        {
            // Implement exponential backoff
            int attemptCount = error is ComponentError compError ? compError.RecoveryAttemptCount : 0;
            var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attemptCount)));

            Logger.Log($"Communication recovery waiting {delay.TotalSeconds} seconds before retry");
            await Task.Delay(delay, ct);

            // This strategy doesn't actively do anything except delay, 
            // assuming the next attempt will succeed after waiting
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, $"Error during communication recovery for device {error.DeviceId}");
            return false;
        }
    }
}