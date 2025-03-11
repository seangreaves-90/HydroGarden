using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.Services
{
    /// <summary>
    /// Implements the Recovery Orchestration Service that coordinates recovery efforts
    /// across the system by planning and executing recovery strategies.
    /// </summary>
    public class RecoveryOrchestrationService : IRecoveryOrchestrationService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IErrorMonitor _errorMonitor;
        private readonly List<IRecoveryStrategy> _registeredStrategies = new();
        private readonly SemaphoreSlim _recoverySemaphore = new(1, 1);
        private readonly ConcurrentDictionary<Guid, ActiveRecoveryOperation> _activeRecoveries = new();
        private readonly ConcurrentQueue<RecoveryRecord> _recoveryHistory = new();
        private readonly object _strategiesLock = new();
        private readonly ConcurrentDictionary<string, AsyncCircuitBreakerPolicy> _circuitBreakers = new();
        private readonly Timer _cleanupTimer;
        private const int MaxHistorySize = 1000;
        private bool _disposed;

        /// <summary>
        /// Creates a new instance of the Recovery Orchestration Service.
        /// </summary>
        /// <param name="logger">Logger for recording recovery activities.</param>
        /// <param name="errorMonitor">Error monitoring service.</param>
        /// <param name="initialStrategies">Initial set of recovery strategies.</param>
        public RecoveryOrchestrationService(
            ILogger logger,
            IErrorMonitor errorMonitor,
            IEnumerable<IRecoveryStrategy>? initialStrategies = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));

            // Register initial strategies if provided
            if (initialStrategies != null)
            {
                foreach (var strategy in initialStrategies)
                {
                    RegisterStrategy(strategy);
                }
            }

            // Initialize cleanup timer to run every 30 minutes
            _cleanupTimer = new Timer(CleanupHistoryCallback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            _logger.Log($"Recovery Orchestration Service initialized with {_registeredStrategies.Count} strategies");
        }

        /// <inheritdoc />
        public void RegisterStrategy(IRecoveryStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            lock (_strategiesLock)
            {
                // Check if strategy is already registered
                if (_registeredStrategies.Any(s => s.GetType() == strategy.GetType()))
                {
                    _logger.Log($"Strategy of type {strategy.GetType().Name} is already registered");
                    return;
                }

                _registeredStrategies.Add(strategy);
                _logger.Log($"Registered recovery strategy: {strategy.Name}");
            }
        }

        /// <inheritdoc />
        public async Task<RecoveryStatus> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            // Check if recovery can be attempted
            if (error is ComponentError componentError && !componentError.CanAttemptRecovery())
            {
                _logger.Log($"Cannot attempt recovery for error {error.ErrorCode} - backoff period not elapsed or max attempts reached");
                return CreateFailedStatus(error);
            }

            // Check if the device is already being recovered
            if (IsDeviceRecovering(error.DeviceId))
            {
                _logger.Log($"Recovery already in progress for device {error.DeviceId}");
                return CreateFailedStatus(error, "Recovery already in progress");
            }

            // Create and execute a recovery plan
            var plan = await CreateRecoveryPlanAsync(error, ct);
            return await ExecuteRecoveryPlanAsync(plan, ct);
        }

        /// <inheritdoc />
        public async Task<RecoveryPlan> CreateRecoveryPlanAsync(IApplicationError? error, CancellationToken ct = default)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            _logger.Log($"Creating recovery plan for device {error.DeviceId}, error code: {error.ErrorCode}");

            // Create error profile for advanced strategy selection
            var errorProfile = ErrorTaxonomy.CreateErrorProfile(error);

            List<IRecoveryStrategy> strategies;
            lock (_strategiesLock)
            {
                // Find applicable strategies sorted by priority
                strategies = _registeredStrategies
                    .Where(s => s.CanRecover(error))
                    .OrderBy(s => (s as RecoveryStrategyBase)?.Priority ?? 100)
                    .ToList();
            }

            if (!strategies.Any())
            {
                _logger.Log($"No applicable recovery strategies found for error {error.ErrorCode}");
            }
            else
            {
                _logger.Log($"Found {strategies.Count} applicable recovery strategies for error {error.ErrorCode}");
            }

            // Create the recovery plan
            var plan = new RecoveryPlan
            {
                Error = error,
                Strategies = strategies,
                Context = new Dictionary<string, object>(errorProfile)
            };

            // Determine appropriate timeout based on complexity
            var complexity = (ErrorTaxonomy.RecoveryComplexity)errorProfile["RecoveryComplexity"];
            plan.Timeout = complexity switch
            {
                ErrorTaxonomy.RecoveryComplexity.Simple => TimeSpan.FromSeconds(30),
                ErrorTaxonomy.RecoveryComplexity.Moderate => TimeSpan.FromMinutes(2),
                ErrorTaxonomy.RecoveryComplexity.Complex => TimeSpan.FromMinutes(5),
                ErrorTaxonomy.RecoveryComplexity.VeryComplex => TimeSpan.FromMinutes(10),
                _ => TimeSpan.FromMinutes(5)
            };

            // Adjust max attempts based on recovery complexity
            plan.MaxAttemptsPerStrategy = complexity switch
            {
                ErrorTaxonomy.RecoveryComplexity.Simple => 5,
                ErrorTaxonomy.RecoveryComplexity.Moderate => 3,
                ErrorTaxonomy.RecoveryComplexity.Complex => 2,
                ErrorTaxonomy.RecoveryComplexity.VeryComplex => 1,
                _ => 3
            };

            return plan;
        }

        /// <inheritdoc />
        public async Task<RecoveryStatus> ExecuteRecoveryPlanAsync(RecoveryPlan plan, CancellationToken ct = default)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            if (plan.Error == null)
                throw new ArgumentException("Recovery plan must have an error", nameof(plan));

            var error = plan.Error;
            var deviceId = error.DeviceId;

            // Create a linked cancellation token with timeout
            using var timeoutCts = new CancellationTokenSource(plan.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
            var linkedToken = linkedCts.Token;

            try
            {
                await _recoverySemaphore.WaitAsync(linkedToken);
                
                // Create active recovery operation
                var operation = new ActiveRecoveryOperation
                {
                    DeviceId = deviceId,
                    Error = error,
                    Plan = plan
                };

                // Register the active recovery
                if (!_activeRecoveries.TryAdd(operation.OperationId, operation))
                {
                    _logger.Log($"Failed to register active recovery for device {deviceId}");
                    return CreateFailedStatus(error, "Failed to register active recovery");
                }

                try
                {
                    _logger.Log($"Starting recovery execution for device {deviceId}, error code: {error.ErrorCode}");

                    if (!plan.Strategies.Any())
                    {
                        _logger.Log($"No applicable recovery strategies in plan for error {error.ErrorCode}");
                        return CreateFailedStatus(error, "No applicable recovery strategies");
                    }

                    // Start timer for duration tracking
                    var startTime = DateTimeOffset.UtcNow;
                    var record = new RecoveryRecord
                    {
                        DeviceId = deviceId,
                        ErrorCode = error.ErrorCode,
                        CorrelationId = error.CorrelationId
                    };

                    // Try each applicable strategy in priority order
                    foreach (var strategy in plan.Strategies)
                    {
                        operation.CurrentStrategy = strategy.Name;
                        operation.CurrentAttempt = 1;
                        _logger.Log($"Attempting recovery using strategy: {strategy.Name}");

                        // Apply resilience policy for the strategy
                        var retryPolicy = CreateRetryPolicy(strategy.Name);
                        var circuitBreakerPolicy = GetCircuitBreakerForStrategy(strategy.Name);
                        
                        try
                        {
                            // Execute with retry and circuit breaker policies
                            bool success = await Policy
                                .WrapAsync(retryPolicy, circuitBreakerPolicy)
                                .ExecuteAsync(async (ctx, token) =>
                                {
                                    int attempt = ctx.ContainsKey("RetryCount") ? (int)ctx["RetryCount"] + 1 : 1;
                                    operation.CurrentAttempt = attempt;
                                    
                                    if (attempt > 1)
                                    {
                                        _logger.Log($"Retry {attempt}/{plan.MaxAttemptsPerStrategy} for strategy {strategy.Name}");
                                    }
                                    
                                    return await strategy.AttemptRecoveryAsync(error, token);
                                }, 
                                new Dictionary<string, object>(), 
                                linkedToken);

                            if (success)
                            {
                                record.IsSuccessful = true;
                                record.StrategyUsed = strategy.Name;
                                record.Duration = DateTimeOffset.UtcNow - startTime;
                                record.Details = $"Recovery successful using strategy: {strategy.Name}";
                                
                                _logger.Log($"Recovery successful for device {deviceId} using strategy: {strategy.Name}");

                                // Record successful recovery with error monitor
                                if (!string.IsNullOrEmpty(error.ErrorCode))
                                {
                                    await _errorMonitor.RegisterRecoveryAttemptAsync(
                                        deviceId, error.ErrorCode, true, linkedToken);
                                }

                                // Create success status
                                var status = new RecoveryStatus
                                {
                                    IsSuccessful = true,
                                    SuccessfulStrategy = strategy.Name,
                                    ErrorCodes = new[] { error.ErrorCode ?? "UNKNOWN" },
                                    AttemptCount = operation.CurrentAttempt,
                                    SuccessCount = 1,
                                    Timestamp = DateTimeOffset.UtcNow,
                                    LastAttempt = DateTimeOffset.UtcNow
                                };

                                // Add to history
                                AddToHistory(record);
                                
                                return status;
                            }
                            
                            _logger.Log($"Recovery strategy {strategy.Name} failed for device {deviceId}");
                        }
                        catch (BrokenCircuitException)
                        {
                            _logger.Log($"Circuit breaker open for strategy {strategy.Name}");
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.Log(ex, $"Exception during recovery with strategy {strategy.Name} for device {deviceId}");
                        }
                    }

                    // All strategies failed
                    record.IsSuccessful = false;
                    record.Duration = DateTimeOffset.UtcNow - startTime;
                    record.Details = "All recovery strategies failed";
                    
                    _logger.Log($"All recovery strategies failed for device {deviceId}, error code: {error.ErrorCode}");

                    // Record failed recovery with error monitor
                    if (!string.IsNullOrEmpty(error.ErrorCode))
                    {
                        await _errorMonitor.RegisterRecoveryAttemptAsync(
                            deviceId, error.ErrorCode, false, CancellationToken.None);
                    }

                    // Add to history
                    AddToHistory(record);
                    
                    return CreateFailedStatus(error, "All strategies failed");
                }
                catch (OperationCanceledException)
                {
                    _logger.Log($"Recovery operation timed out or was canceled for device {deviceId}");
                    return CreateFailedStatus(error, "Recovery operation timed out or was canceled");
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Exception during recovery orchestration for device {deviceId}");
                    return CreateFailedStatus(error, $"Exception: {ex.Message}");
                }
                finally
                {
                    // Remove the active recovery
                    _activeRecoveries.TryRemove(operation.OperationId, out _);
                }
            }
            finally
            {
                _recoverySemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<RecoveryStatus> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default)
        {
            if (deviceId == Guid.Empty)
                throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));

            // Check if the device is already being recovered
            if (IsDeviceRecovering(deviceId))
            {
                _logger.Log($"Recovery already in progress for device {deviceId}");
                return new RecoveryStatus
                {
                    IsSuccessful = false,
                    AttemptCount = 0,
                    ErrorCodes = Array.Empty<string>(),
                    Timestamp = DateTimeOffset.UtcNow,
                    LastAttempt = DateTimeOffset.UtcNow
                };
            }

            // Get active errors for the device
            var errors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId, ct);
            if (!errors.Any())
            {
                _logger.Log($"No active errors found for device {deviceId}");
                return new RecoveryStatus
                {
                    IsSuccessful = true,
                    AttemptCount = 0,
                    ErrorCodes = Array.Empty<string>(),
                    Timestamp = DateTimeOffset.UtcNow,
                    LastAttempt = DateTimeOffset.UtcNow
                };
            }

            _logger.Log($"Found {errors.Count} active errors for device {deviceId}");

            // Sort errors by severity and recoverability
            var orderedErrors = errors
                .OrderByDescending(e => e.Severity)
                .ThenBy(e => e is ComponentError ce && ce.IsUnrecoverable)
                .ToList();

            // Track success count
            int successCount = 0;
            var errorCodes = new HashSet<string>();
            string? successfulStrategy = null;
            int attemptCount = 0;

            // Try to recover each error
            foreach (var error in orderedErrors)
            {
                if (!string.IsNullOrEmpty(error.ErrorCode))
                {
                    errorCodes.Add(error.ErrorCode);
                }

                var recoveryResult = await AttemptRecoveryAsync(error, ct);
                attemptCount += recoveryResult.AttemptCount;

                if (recoveryResult.IsSuccessful)
                {
                    successCount++;
                    successfulStrategy = recoveryResult.SuccessfulStrategy;
                }

                // If canceled, stop processing
                if (ct.IsCancellationRequested)
                {
                    break;
                }
            }

            // Return combined status
            return new RecoveryStatus
            {
                IsSuccessful = successCount > 0,
                SuccessCount = successCount,
                AttemptCount = attemptCount,
                ErrorCodes = errorCodes.ToArray(),
                SuccessfulStrategy = successfulStrategy,
                Timestamp = DateTimeOffset.UtcNow,
                LastAttempt = DateTimeOffset.UtcNow
            };
        }

        /// <inheritdoc />
        public bool IsDeviceRecovering(Guid deviceId)
        {
            return _activeRecoveries.Values.Any(r => r.DeviceId == deviceId);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ActiveRecoveryOperation>> GetActiveRecoveriesAsync(CancellationToken ct = default)
        {
            var activeRecoveries = _activeRecoveries.Values.ToList();
            return Task.FromResult<IReadOnlyList<ActiveRecoveryOperation>>(activeRecoveries);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<RecoveryRecord>> GetRecoveryHistoryAsync(
            Guid deviceId, 
            int maxEntries = 50, 
            CancellationToken ct = default)
        {
            var history = _recoveryHistory
                .Where(r => r.DeviceId == deviceId)
                .OrderByDescending(r => r.Timestamp)
                .Take(maxEntries)
                .ToList();
                
            return Task.FromResult<IReadOnlyList<RecoveryRecord>>(history);
        }

        /// <inheritdoc />
        public Task<IDictionary<string, RecoveryMetrics>> GetRecoveryStatisticsAsync(
            DateTimeOffset since, 
            CancellationToken ct = default)
        {
            var metrics = new Dictionary<string, RecoveryMetrics>();
            
            // Group history by error code
            var groups = _recoveryHistory
                .Where(r => r.Timestamp >= since)
                .GroupBy(r => r.ErrorCode ?? "UNKNOWN");
                
            foreach (var group in groups)
            {
                var entries = group.ToList();
                var successfulEntries = entries.Where(e => e.IsSuccessful).ToList();
                
                // Calculate most successful strategy
                string? mostSuccessfulStrategy = successfulEntries
                    .GroupBy(e => e.StrategyUsed)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;
                    
                // Calculate average recovery time
                double avgTime = successfulEntries.Any() 
                    ? successfulEntries.Average(e => e.Duration.TotalMilliseconds) 
                    : 0;
                    
                metrics[group.Key] = new RecoveryMetrics
                {
                    TotalAttempts = entries.Count,
                    SuccessfulAttempts = successfulEntries.Count,
                    FailedAttempts = entries.Count - successfulEntries.Count,
                    MostSuccessfulStrategy = mostSuccessfulStrategy,
                    AverageRecoveryTimeMs = avgTime,
                    LastAttemptTimestamp = entries.Max(e => e.Timestamp)
                };
            }
            
            return Task.FromResult<IDictionary<string, RecoveryMetrics>>(metrics);
        }

        /// <summary>
        /// Creates a retry policy for recovery strategies.
        /// </summary>
        private AsyncRetryPolicy CreateRetryPolicy(string strategyName)
        {
            return Policy
                .Handle<Exception>(ex => !(ex is OperationCanceledException))
                .WaitAndRetryAsync(
                    3, // Max retries
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (ex, timeSpan, retryCount, context) =>
                    {
                        context["RetryCount"] = retryCount;
                        _logger.Log($"Retry {retryCount} for strategy {strategyName} after exception: {ex.Message}");
                    });
        }

        /// <summary>
        /// Gets or creates a circuit breaker policy for a strategy.
        /// </summary>
        private AsyncCircuitBreakerPolicy GetCircuitBreakerForStrategy(string strategyName)
        {
            return _circuitBreakers.GetOrAdd(strategyName, _ => 
                Policy
                    .Handle<Exception>(ex => !(ex is OperationCanceledException))
                    .CircuitBreakerAsync(
                        exceptionsAllowedBeforeBreaking: 5,
                        durationOfBreak: TimeSpan.FromMinutes(5),
                        onBreak: (ex, timespan) => 
                            _logger.Log($"Circuit breaker for strategy {strategyName} tripped due to: {ex.Message}"),
                        onReset: () => 
                            _logger.Log($"Circuit breaker for strategy {strategyName} reset"),
                        onHalfOpen: () => 
                            _logger.Log($"Circuit breaker for strategy {strategyName} half-open")
                    ));
        }

        /// <summary>
        /// Adds a recovery record to the history queue.
        /// </summary>
        private void AddToHistory(RecoveryRecord record)
        {
            _recoveryHistory.Enqueue(record);
            
            // If queue is too large, remove old entries
            while (_recoveryHistory.Count > MaxHistorySize && _recoveryHistory.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Creates a failed recovery status.
        /// </summary>
        private static RecoveryStatus CreateFailedStatus(IApplicationError? error, string? details = null)
        {
            return new RecoveryStatus
            {
                IsSuccessful = false,
                ErrorCodes = new[] { error.ErrorCode ?? "UNKNOWN" },
                AttemptCount = 1,
                SuccessCount = 0,
                Timestamp = DateTimeOffset.UtcNow,
                LastAttempt = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// Cleanup timer callback to manage history size.
        /// </summary>
        private void CleanupHistoryCallback(object? state)
        {
            // Remove old entries to maintain queue size
            while (_recoveryHistory.Count > MaxHistorySize && _recoveryHistory.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Disposes resources used by the service.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _recoverySemaphore.Dispose();
            _cleanupTimer.Dispose();
            
            GC.SuppressFinalize(this);
        }
    }
}
