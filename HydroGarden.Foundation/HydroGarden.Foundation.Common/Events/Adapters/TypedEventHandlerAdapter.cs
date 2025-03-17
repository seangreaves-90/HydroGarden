using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Adapters
{
    /// <summary>
    /// This adapter is deprecated as IEventHandler{TEvent} now implements IEventHandler directly.
    /// It is kept for backward compatibility.
    /// </summary>
    /// <typeparam name="TEvent">The specific event type this adapter handles</typeparam>
    [Obsolete("This adapter is no longer needed as IEventHandler<TEvent> now implements IEventHandler directly.")]
    public class TypedEventHandlerAdapter<TEvent> : IEventHandler<TEvent> where TEvent : IEvent 
    {
        private readonly IEventHandler<TEvent> _typedHandler;

        /// <summary>
        /// Creates a new adapter for the specified typed handler
        /// </summary>
        /// <param name="typedHandler">The typed event handler to adapt</param>
        public TypedEventHandlerAdapter(IEventHandler<TEvent> typedHandler)
        {
            _typedHandler = typedHandler ?? throw new ArgumentNullException(nameof(typedHandler));
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (_typedHandler is IAsyncDisposable disposable)
            {
                return disposable.DisposeAsync();
            }
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public async Task HandleAsync(TEvent @event, CancellationToken ct = default)
        {
            if (@event is { } typedEvent)
            {
                await _typedHandler.HandleAsync(typedEvent, ct);
            }
        }
    }
}