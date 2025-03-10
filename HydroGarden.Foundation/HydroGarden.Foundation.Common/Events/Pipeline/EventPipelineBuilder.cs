using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events.Pipeline.Middleware;
using HydroGarden.Logger.Abstractions;
using System;

namespace HydroGarden.Foundation.Common.Events.Pipeline
{
    /// <summary>
    /// Builder for creating and configuring event processing pipelines.
    /// </summary>
    public class EventPipelineBuilder
    {
        private readonly IEventProcessingPipeline _pipeline;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventPipelineBuilder"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public EventPipelineBuilder(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipeline = new EventProcessingPipeline(logger);
        }

        /// <summary>
        /// Adds a middleware to the pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <returns>The builder for chaining.</returns>
        public EventPipelineBuilder AddMiddleware(IEventMiddleware middleware)
        {
            _pipeline.AddMiddleware(middleware);
            return this;
        }

        /// <summary>
        /// Adds a middleware to the pipeline for specific event types.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <param name="eventTypes">The event types to apply this middleware to.</param>
        /// <returns>The builder for chaining.</returns>
        public EventPipelineBuilder AddMiddleware(IEventMiddleware middleware, params EventType[] eventTypes)
        {
            _pipeline.AddMiddleware(middleware, eventTypes);
            return this;
        }

        /// <summary>
        /// Adds logging middleware to the pipeline.
        /// </summary>
        /// <param name="loggingLevel">The logging level.</param>
        /// <returns>The builder for chaining.</returns>
        public EventPipelineBuilder AddLogging(LoggingMiddleware.LoggingLevel loggingLevel = LoggingMiddleware.LoggingLevel.Detailed)
        {
            var middleware = new LoggingMiddleware(_logger, loggingLevel);
            _pipeline.AddMiddleware(middleware);
            return this;
        }

        /// <summary>
        /// Adds retry middleware to the pipeline.
        /// </summary>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="initialDelay">The initial delay before the first retry.</param>
        /// <param name="backoffMultiplier">The multiplier for exponential backoff.</param>
        /// <param name="useJitter">Whether to add random jitter to retry delays.</param>
        /// <returns>The builder for chaining.</returns>
        public EventPipelineBuilder AddRetry(
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            double backoffMultiplier = 2.0,
            bool useJitter = true)
        {
            var middleware = new RetryMiddleware(
                _logger,
                maxRetries,
                initialDelay,
                backoffMultiplier,
                useJitter);
            
            _pipeline.AddMiddleware(middleware);
            return this;
        }

        /// <summary>
        /// Adds circuit breaker middleware to the pipeline.
        /// </summary>
        /// <param name="failureThreshold">The number of consecutive failures before opening the circuit.</param>
        /// <param name="resetTimeout">The time to wait before attempting to close the circuit.</param>
        /// <returns>The builder for chaining.</returns>
        public EventPipelineBuilder AddCircuitBreaker(
            int failureThreshold = 5,
            TimeSpan? resetTimeout = null)
        {
            var middleware = new CircuitBreakerMiddleware(
                _logger,
                failureThreshold,
                resetTimeout);
            
            _pipeline.AddMiddleware(middleware);
            return this;
        }

        /// <summary>
        /// Adds dead letter queue middleware to the pipeline.
        /// </summary>
        /// <param name="queueCapacity">The maximum capacity of the queue.</param>
        /// <param name="retentionPeriod">The period to retain dead letter entries.</param>
        /// <returns>The builder for chaining.</returns>
        public EventPipelineBuilder AddDeadLetterQueue(
            int queueCapacity = 1000,
            TimeSpan? retentionPeriod = null)
        {
            var middleware = new DeadLetterQueueMiddleware(
                _logger,
                queueCapacity,
                retentionPeriod);
            
            _pipeline.AddMiddleware(middleware);
            return this;
        }

        /// <summary>
        /// Configures a standard pipeline with sensible defaults.
        /// </summary>
        /// <returns>The builder for chaining.</returns>
        public EventPipelineBuilder UseStandardPipeline()
        {
            return AddLogging()
                .AddCircuitBreaker()
                .AddRetry()
                .AddDeadLetterQueue();
        }

        /// <summary>
        /// Builds the pipeline.
        /// </summary>
        /// <returns>The configured pipeline.</returns>
        public IEventProcessingPipeline Build()
        {
            return _pipeline;
        }
    }
}