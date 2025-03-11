//using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
//using HydroGarden.Foundation.Abstractions.Interfaces.Events;

//namespace HydroGarden.Foundation.Common.Events.Extensions
//{
//    /// <summary>
//    /// Extension methods for the EventBus to handle error events.
//    /// </summary>
//    public static class ErrorEventBusExtensions
//    {
//        /// <summary>
//        /// Subscribes to error events.
//        /// </summary>
//        /// <param name="eventBus">The event bus.</param>
//        /// <param name="handler">The handler for error events.</param>
//        /// <param name="options">The subscription options.</param>
//        /// <returns>A subscription ID that can be used to unsubscribe.</returns>
//        public static Guid SubscribeToErrorEvents(
//            this IEventBus eventBus,
//            Func<IApplicationError, CancellationToken, Task> handler,
//            IEventSubscriptionOptions? options = null)
//        {
//            if (eventBus == null)
//            {
//                throw new ArgumentNullException(nameof(eventBus));
//            }

//            if (handler == null)
//            {
//                throw new ArgumentNullException(nameof(handler));
//            }

//            // Create an event handler that extracts the error from the event
//            var errorEventHandler = new ErrorEventHandler(handler);
            
//            // Use the default options if none provided
//            var subscriptionOptions = options ?? new EventSubscriptionOptions
//            {
//                EventTypes = [EventType.Alert],
//            };

//            return eventBus.Subscribe(errorEventHandler, subscriptionOptions);
//        }

//        /// <summary>
//        /// Subscribes to recovery events.
//        /// </summary>
//        /// <param name="eventBus">The event bus.</param>
//        /// <param name="handler">The handler for recovery events.</param>
//        /// <param name="options">The subscription options.</param>
//        /// <returns>A subscription ID that can be used to unsubscribe.</returns>
//        public static Guid SubscribeToRecoveryEvents(
//            this IEventBus eventBus,
//            Func<Guid, string, bool, string, Guid, CancellationToken, Task> handler,
//            IEventSubscriptionOptions? options = null)
//        {
//            if (eventBus == null)
//            {
//                throw new ArgumentNullException(nameof(eventBus));
//            }

//            if (handler == null)
//            {
//                throw new ArgumentNullException(nameof(handler));
//            }

//            // Create an event handler that extracts the recovery data from the event
//            var recoveryEventHandler = new RecoveryEventHandler(handler);
            
//            // Use the default options if none provided
//            var subscriptionOptions = options ?? new EventSubscriptionOptions
//            {
//                EventTypes = [EventType.System],
//            };

//            return eventBus.Subscribe(recoveryEventHandler, subscriptionOptions);
//        }

//        /// <summary>
//        /// Implementation of IEventHandler for error events.
//        /// </summary>
//        private class ErrorEventHandler(Func<IApplicationError, CancellationToken, Task> handler) : IEventHandler
//        {
//            public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
//            {
//                if (evt is ErrorOccurredEvent { ErrorData: not null } errorEvent)
//                    // Convert the error event data to an application error
//                {
//                    var applicationError = new ApplicationError(
//                        errorEvent.ErrorData.DeviceId,
//                        errorEvent.ErrorData.ErrorCode,
//                        errorEvent.ErrorData.Message,
//                        errorEvent.ErrorData.Severity,
//                        errorEvent.ErrorData.Context,
//                        errorEvent.ErrorData.Timestamp,
//                        null, // We don't have the original exception
//                        errorEvent.ErrorData.CorrelationId,
//                        errorEvent.ErrorData.Source,
//                        errorEvent.ErrorData.IsTransient);

//                    await handler(applicationError, ct);
//                }
//            }

//            public ValueTask DisposeAsync()
//            {
//                return ValueTask.CompletedTask;
//            }
//        }

//        /// <summary>
//        /// Implementation of IEventHandler for recovery events.
//        /// </summary>
//        private class RecoveryEventHandler(Func<Guid, string, bool, string, Guid, CancellationToken, Task> handler)
//            : IEventHandler
//        {
//            public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
//            {
//                if (evt is RecoveryAttemptedEvent { RecoveryData: not null } recoveryEvent)
//                    await handler(
//                        recoveryEvent.RecoveryData.DeviceId,
//                        recoveryEvent.RecoveryData.ErrorCode,
//                        recoveryEvent.RecoveryData.IsSuccessful,
//                        recoveryEvent.RecoveryData.Message,
//                        recoveryEvent.RecoveryData.CorrelationId,
//                        ct);
//            }

//            public ValueTask DisposeAsync()
//            {
//                return ValueTask.CompletedTask;
//            }
//        }
//    }

//    /// <summary>
//    /// Application error implementation for errors received from events.
//    /// </summary>
//    public class ApplicationError(
//        Guid deviceId,
//        string errorCode,
//        string message,
//        ErrorSeverity severity,
//        IDictionary<string, object>? context,
//        DateTimeOffset timestamp,
//        Exception? exception,
//        Guid correlationId,
//        ErrorSource source,
//        bool isTransient)
//        : IApplicationError
//    {
//        public Guid DeviceId { get; } = deviceId;
//        public string ErrorCode { get; } = errorCode;
//        public string Message { get; } = message;
//        public ErrorSeverity Severity { get; } = severity;
//        public IDictionary<string, object> Context { get; } = context ?? new Dictionary<string, object>();
//        public DateTimeOffset Timestamp { get; } = timestamp;
//        public Exception? Exception { get; } = exception;
//        public Guid CorrelationId { get; } = correlationId;
//        public ErrorSource Source { get; } = source;
//        public bool IsTransient { get; } = isTransient;

//        public void RecordRecoveryAttempt()
//        {
//            // This is a dummy implementation since we can't actually record recovery attempts
//            // for errors received from events
//        }
//    }
//}