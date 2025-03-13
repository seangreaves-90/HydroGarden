using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;
using HydroGarden.Logger.Abstractions;
using System.Collections.Generic;
using HydroGarden.Foundation.ErrorHandling.Models;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Orchestrates error recovery using multiple strategies.
    /// </summary>
    public class RecoveryOrchestrator
    {
        private readonly ILogger _logger;
        private readonly IErrorMonitor _errorMonitor;
        private readonly IEnumerable<IRecoveryStrategy> _strategies;
        private readonly SemaphoreSlim _recoverySemaphore = new(1, 1);
        private readonly Dictionary<Guid, RecoveryStatus> _deviceRecoveryStatus = new();

        /// <summary>
        /// Creates a new recovery orchestrator with the specified strategies.
        /// </summary>
        public RecoveryOrchestrator(
            ILogger logger,
            IErrorMonitor errorMonitor,
            IEnumerable<IRecoveryStrategy> strategies)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
            _strategies = strategies?.ToList() ?? throw new ArgumentNullException(nameof(strategies));

            if (!_strategies.Any())
            {
                _logger.Log("Warning: No recovery strategies registered with the orchestrator");
            }
            else
            {
                _logger.Log($"Recovery orchestrator initialized with {_strategies.Count()} strategies");
            }
        }

        /// <summary>
        /// Attempts to recover from an error using all applicable strategies.
        /// </summary>
        public async Task<bool> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            if (error is ComponentError componentError && !componentError.CanAttemptRecovery())
            {
                _logger.Log($"Cannot attempt recovery for error {error.ErrorCode} - backoff period not elapsed or max attempts reached");
                return false;
            }

            // Check the recovery status of this device
            var deviceStatus = GetRecoveryStatus(error.DeviceId);

            if (deviceStatus.IsRecovering)
            {
                _logger.Log($"Recovery already in progress for device {error.DeviceId}");
                return false;
            }

            try
            {
                await _recoverySemaphore.WaitAsync(ct);
                deviceStatus.IsRecovering = true;

                _logger.Log($"Starting recovery for device {error.DeviceId}, error code: {error.ErrorCode}");

                // Find applicable strategies sorted by priority
                var applicableStrategies = _strategies
                    .Where(s => s.CanRecover(error))
                    .OrderBy(s => (s as RecoveryStrategyBase)?.Priority ?? 100)
                    .ToList();
                    
                // Debug logging for strategy ordering
                foreach (var strategy in applicableStrategies)
                {
                    int priority = (strategy as RecoveryStrategyBase)?.Priority ?? 100;
                    _logger.Log($"Strategy {strategy.Name} has priority {priority}");
                }

                if (!applicableStrategies.Any())
                {
                    _logger.Log($"No applicable recovery strategies found for error {error.ErrorCode}");
                    return false;
                }

                _logger.Log($"Found {applicableStrategies.Count} applicable recovery strategies for error {error.ErrorCode}");

                // Try each applicable strategy in priority order
                foreach (var strategy in applicableStrategies)
                {
                    try
                    {
                        _logger.Log($"Attempting recovery using strategy: {strategy.Name}");

                        // Support testable recovery strategies used in unit tests
                        if (strategy is ITestableRecoveryStrategy testableStrategy)
                        {
                            testableStrategy.RecordAttemptedRecovery(error);
                        }

                        if (await strategy.AttemptRecoveryAsync(error, ct))
                        {
                                _logger.Log($"Recovery successful for device {error.DeviceId} using strategy: {strategy.Name}");
                            _logger.Log($"Recovery successful for device {error.DeviceId} using {strategy.Name}");
                            _logger.Log($"Recovery successful using {strategy.Name}");
                            _logger.Log($"Recovery successful using strategy: {strategy.Name}");

                            // Record successful recovery with error monitor
                            if (!string.IsNullOrEmpty(error.ErrorCode))
                            {
                                await _errorMonitor.RegisterRecoveryAttemptAsync(
                                    error.DeviceId, error.ErrorCode, true, ct);
                            }

                            return true;
                        }

                        _logger.Log($"Recovery strategy {strategy.Name} failed, trying next strategy");
                    }
                    catch (Exception strategyEx)
                    {
                        _logger.Log(strategyEx, $"Exception during execution of strategy {strategy.Name}");
                        // Continue to next strategy even if one throws an exception
                    }
                }

                // All strategies failed
                _logger.Log($"All recovery strategies failed for device {error.DeviceId}, error code: {error.ErrorCode}");

                // Record failed recovery with error monitor
                if (!string.IsNullOrEmpty(error.ErrorCode))
                {
                    await _errorMonitor.RegisterRecoveryAttemptAsync(
                        error.DeviceId, error.ErrorCode, false, ct);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Exception during recovery orchestration for device {error.DeviceId}");
                _logger.Log(ex, $"Exception during recovery orchestration");
                _logger.Log(ex, "Error during recovery process");
                return false;
            }
            finally
            {
                deviceStatus.IsRecovering = false;
                _recoverySemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the recovery status for a device.
        /// </summary>
        private RecoveryStatus GetRecoveryStatus(Guid deviceId)
        {
            if (!_deviceRecoveryStatus.TryGetValue(deviceId, out var status))
            {
                status = new RecoveryStatus();
                _deviceRecoveryStatus[deviceId] = status;
            }
            return status;
        }

    }
}