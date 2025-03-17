using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Adapters
{
    /// <summary>
    /// Adapter class to convert IEventHandler&lt;TEvent&gt; to IEventHandler&lt;IEvent&gt;
    /// </summary>
    /// <typeparam name="TEvent">The specific event type this adapter handles</typeparam>
    public class GenericEventHandlerAdapter<TEvent> : IEventHandler<IEvent> where TEvent : IEvent
    {
        private readonly IEventHandler<TEvent>? _typedHandler;
        private readonly IEventHandler? _untypedHandler;

        /// <summary>
        /// Creates a new adapter for the specified typed handler
        /// </summary>
        /// <param name="typedHandler">The typed event handler to adapt</param>
        public GenericEventHandlerAdapter(IEventHandler<TEvent> typedHandler)
        {
            _typedHandler = typedHandler ?? throw new ArgumentNullException(nameof(typedHandler));
            _untypedHandler = null;
        }

        /// <summary>
        /// Creates a new adapter for the specified untyped handler
        /// </summary>
        /// <param name="untypedHandler">The untyped event handler to adapt</param>
        public GenericEventHandlerAdapter(IEventHandler untypedHandler)
        {
            _untypedHandler = untypedHandler ?? throw new ArgumentNullException(nameof(untypedHandler));
            _typedHandler = null;
        }

        /// <inheritdoc />
        public Task HandleAsync(IEvent @event, CancellationToken ct = default)
        {
            if (_typedHandler != null)
            {
                // Only handle events of the expected type
                if (@event is TEvent typedEvent)
                {
                    return _typedHandler.HandleAsync(typedEvent, ct);
                }
            }
            else if (_untypedHandler != null)
            {
                // For untyped handlers, use the base HandleEventAsync method
                return _untypedHandler.HandleEventAsync(null, @event, ct);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
        {
            if (_typedHandler != null)
            {
                // Only handle events of the expected type
                if (evt is TEvent typedEvent)
                {
                    return _typedHandler.HandleAsync(typedEvent, ct);
                }
            }
            else if (_untypedHandler != null)
            {
                // For untyped handlers, forward to the base handler
                return _untypedHandler.HandleEventAsync(sender, evt, ct);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (_typedHandler is IAsyncDisposable typedDisposable)
            {
                return typedDisposable.DisposeAsync();
            }
            else if (_untypedHandler != null)
            {
                return _untypedHandler.DisposeAsync();
            }
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}