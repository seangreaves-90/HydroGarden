using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation
{
    /// <summary>
    /// Defines a service that handles conversion between errors and events.
    /// </summary>
    public interface IErrorEventTransformationService
    {
        /// <summary>
        /// Transforms an application error into an error event.
        /// </summary>
        /// <param name="error">The application error to transform.</param>
        /// <returns>The transformed error event.</returns>
        IErrorEvent TransformErrorToEvent(IApplicationError error);

        /// <summary>
        /// Transforms an error event into an event that can be published on the event bus.
        /// </summary>
        /// <param name="errorEvent">The error event to transform.</param>
        /// <returns>The event that can be published.</returns>
        IEvent TransformToPublishableEvent(IErrorEvent errorEvent);

        /// <summary>
        /// Transforms a recovery event into an event that can be published on the event bus.
        /// </summary>
        /// <param name="recoveryEvent">The recovery event to transform.</param>
        /// <returns>The event that can be published.</returns>
        IEvent TransformToPublishableEvent(IRecoveryEvent recoveryEvent);

        /// <summary>
        /// Extracts an error event from a general event if it's an error event.
        /// </summary>
        /// <param name="event">The event to extract from.</param>
        /// <returns>The error event if the event is an error event, null otherwise.</returns>
        IErrorEvent? ExtractErrorEvent(IEvent @event);

        /// <summary>
        /// Extracts a recovery event from a general event if it's a recovery event.
        /// </summary>
        /// <param name="event">The event to extract from.</param>
        /// <returns>The recovery event if the event is a recovery event, null otherwise.</returns>
        IRecoveryEvent? ExtractRecoveryEvent(IEvent @event);

        /// <summary>
        /// Publishes an error as an event through the event bus.
        /// </summary>
        /// <param name="error">The error to publish.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PublishErrorAsEventAsync(IApplicationError error, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a recovery attempt as an event through the event bus.
        /// </summary>
        /// <param name="deviceId">The ID of the device the error is associated with.</param>
        /// <param name="errorCode">The error code being recovered.</param>
        /// <param name="isSuccessful">Whether the recovery was successful.</param>
        /// <param name="message">A message describing the recovery.</param>
        /// <param name="correlationId">The correlation ID for tracing.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PublishRecoveryAsEventAsync(
            Guid deviceId,
            string errorCode,
            bool isSuccessful,
            string message,
            Guid correlationId,
            CancellationToken cancellationToken = default);
    }
}