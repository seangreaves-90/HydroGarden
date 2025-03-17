using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Adapters
{
    /// <summary>
    /// Adapter class to convert typed IEventHandler<TEvent> to IEventHandler
    /// </summary>
    /// <typeparam name="TEvent">The specific event type this adapter handles</typeparam>
    public class TypedEventHandlerAdapter<TEvent> : IEventHandler where TEvent : IEvent
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
        public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
        {
            // Only handle events of the expected type
            if (evt is TEvent typedEvent)
            {
                await _typedHandler.HandleAsync(typedEvent, ct);
            }
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
    }
}