using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Core.Events;
using HydroGarden.Foundation.Common.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Common.Events.Extensions
{
    /// <summary>
    /// Extension methods for the EventBus to handle error events.
    /// </summary>
    public static class ErrorEventBusExtensions
    {
        /// <summary>
        /// Subscribes to error events.
        /// </summary>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="handler">The handler for error events.</param>
        /// <param name="options">The subscription options.</param>
        /// <returns>A subscription ID that can be used to unsubscribe.</returns>
        public static Guid SubscribeToErrorEvents(
            this IEventBus eventBus,
            Func<IApplicationError, CancellationToken, Task> handler,
            IEventSubscriptionOptions options = null)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // Create an event handler that extracts the error from the event
            var errorEventHandler = new ErrorEventHandler(handler);
            
            // Use the default options if none provided
            var subscriptionOptions = options ?? new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Alert },
            };

            return eventBus.Subscribe(errorEventHandler, subscriptionOptions);
        }

        /// <summary>
        /// Subscribes to recovery events.
        /// </summary>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="handler">The handler for recovery events.</param>
        /// <param name="options">The subscription options.</param>
        /// <returns>A subscription ID that can be used to unsubscribe.</returns>
        public static Guid SubscribeToRecoveryEvents(
            this IEventBus eventBus,
            Func<Guid, string, bool, string, Guid, CancellationToken, Task> handler,
            IEventSubscriptionOptions options = null)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // Create an event handler that extracts the recovery data from the event
            var recoveryEventHandler = new RecoveryEventHandler(handler);
            
            // Use the default options if none provided
            var subscriptionOptions = options ?? new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.System },
            };

            return eventBus.Subscribe(recoveryEventHandler, subscriptionOptions);
        }

        /// <summary>
        /// Implementation of IEventHandler for error events.
        /// </summary>
        private class ErrorEventHandler : IEventHandler
        {
            private readonly Func<IApplicationError, CancellationToken, Task> _handler;

            public ErrorEventHandler(Func<IApplicationError, CancellationToken, Task> handler)
            {
                _handler = handler;
            }

            public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default) where T : IEvent
            {
                if (evt is ErrorOccurredEvent errorEvent)
                {
                    // Convert the error event data to an application error
                    var applicationError = new ApplicationError(
                        errorEvent.ErrorData.DeviceId,
                        errorEvent.ErrorData.ErrorCode,
                        errorEvent.ErrorData.Message,
                        (ErrorSeverity)errorEvent.ErrorData.Severity,
                        errorEvent.ErrorData.Context,
                        errorEvent.ErrorData.Timestamp,
                        null, // We don't have the original exception
                        errorEvent.ErrorData.CorrelationId,
                        (ErrorSource)errorEvent.ErrorData.Source,
                        errorEvent.ErrorData.IsTransient);

                    await _handler(applicationError, ct);
                }
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>
        /// Implementation of IEventHandler for recovery events.
        /// </summary>
        private class RecoveryEventHandler : IEventHandler
        {
            private readonly Func<Guid, string, bool, string, Guid, CancellationToken, Task> _handler;

            public RecoveryEventHandler(Func<Guid, string, bool, string, Guid, CancellationToken, Task> handler)
            {
                _handler = handler;
            }

            public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default) where T : IEvent
            {
                if (evt is RecoveryAttemptedEvent recoveryEvent)
                {
                    await _handler(
                        recoveryEvent.RecoveryData.DeviceId,
                        recoveryEvent.RecoveryData.ErrorCode,
                        recoveryEvent.RecoveryData.IsSuccessful,
                        recoveryEvent.RecoveryData.Message,
                        recoveryEvent.RecoveryData.CorrelationId,
                        ct);
                }
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Application error implementation for errors received from events.
    /// </summary>
    internal class ApplicationError : IApplicationError
    {
        public ApplicationError(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity,
            IDictionary<string, object> context,
            DateTimeOffset timestamp,
            Exception exception,
            Guid correlationId,
            ErrorSource source,
            bool isTransient)
        {
            DeviceId = deviceId;
            ErrorCode = errorCode;
            Message = message;
            Severity = severity;
            Context = context ?? new Dictionary<string, object>();
            Timestamp = timestamp;
            Exception = exception;
            CorrelationId = correlationId;
            Source = source;
            IsTransient = isTransient;
        }

        public Guid DeviceId { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public ErrorSeverity Severity { get; }
        public IDictionary<string, object> Context { get; }
        public DateTimeOffset Timestamp { get; }
        public Exception Exception { get; }
        public Guid CorrelationId { get; }
        public ErrorSource Source { get; }
        public bool IsTransient { get; }

        public void RecordRecoveryAttempt()
        {
            // This is a dummy implementation since we can't actually record recovery attempts
            // for errors received from events
        }
    }
}