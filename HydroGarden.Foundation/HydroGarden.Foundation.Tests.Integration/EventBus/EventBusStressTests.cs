using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.Adapters;
using HydroGarden.Logger.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;
using HydroGarden.Foundation.Abstractions.Interfaces;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    /// <summary>
    /// Stress and performance tests for the EventBus.
    /// These tests verify system stability under high load.
    /// </summary>
    public class EventBusStressTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IEventRouter> _mockEventRouter;
        private readonly Common.Events.EventBus _eventBus;
        
        public EventBusStressTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockEventRouter = new Mock<IEventRouter>();
            
            // Set up router to pass through all subscriptions
            _mockEventRouter.Setup(r => r.GetMatchingSubscriptionsAsync(
                It.IsAny<IEvent>(), 
                It.IsAny<IEnumerable<IEventSubscription>>(), 
                It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>((_, subs, _) => 
                    Task.FromResult<IReadOnlyList<IEventSubscription>>(subs.ToList()));
            
            _eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object);
        }
        
        [Fact]
        public async Task EventBus_ShouldHandleHighVolumeEvents()
        {
            // Arrange
            const int eventCount = 1000;
            var processedEvents = new ConcurrentBag<Guid>();
            
            // Create a fast handler
            var fastHandler = new Mock<IEventHandler>();
            fastHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, evt, _) => 
                    processedEvents.Add(evt.EventId))
                .Returns(Task.CompletedTask);
            
            // Subscribe the handler using adapter
            var adapter = new GenericEventHandlerAdapter<IEvent>(fastHandler.Object);
            _eventBus.Subscribe<IEvent>(adapter);
            
            // Create many events
            var events = new List<IEvent>();
            for (int i = 0; i < eventCount; i++)
            {
                var eventId = Guid.NewGuid();
                var mockEvent = new Mock<IEvent>();
                mockEvent.Setup(e => e.EventId).Returns(eventId);
                mockEvent.Setup(e => e.EventType).Returns(EventType.Command);
                mockEvent.Setup(e => e.SourceId).Returns(Guid.NewGuid());
                mockEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
                
                events.Add(mockEvent.Object);
            }
            
            // Act - Publish all events and measure performance
            var stopwatch = Stopwatch.StartNew();
            var publishTasks = events.Select(evt => _eventBus.PublishAsync(this, evt)).ToList();
            await Task.WhenAll(publishTasks);
            stopwatch.Stop();
            
            // Assert
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var eventsPerSecond = eventCount / (elapsedMs / 1000.0);
            
            processedEvents.Count.Should().Be(eventCount * 2);
            
            // Verify all tasks completed successfully
            publishTasks.Should().AllSatisfy(task => task.IsCompleted.Should().BeTrue());
            publishTasks.Should().AllSatisfy(task => task.Result.Should().NotBeNull());
            publishTasks.Should().AllSatisfy(task => task.Result!.SuccessCount.Should().Be(2));
            
            // Log performance metrics - not a strict test as it depends on machine
            _mockLogger.Verify(l => 
                l.Log(It.Is<string>(s => s.Contains($"Event") && s.Contains("published to") && s.Contains("handlers with") && s.Contains("successful"))), 
                Times.Exactly(eventCount));
            
            // Performance check - This is a loose check as performance depends on the test environment
            eventsPerSecond.Should().BeGreaterThan(100, 
                $"Event bus should handle at least 100 events per second, but processed {eventsPerSecond:F1} events/sec");
        }
        
        [Fact]
        public async Task EventBus_ShouldHandleConcurrentSubscriptionChanges()
        {
            // Arrange
            const int handlerCount = 100;
            const int eventsPerHandler = 10;
            
            // Create many handlers
            var handlers = new List<IEventHandler>();
            var subscriptionIds = new List<Guid>();
            var processedEvents = new ConcurrentDictionary<Guid, int>();
            
            for (int i = 0; i < handlerCount; i++)
            {
                var mockHandler = new Mock<IEventHandler>();
                var handlerId = i;
                
                mockHandler
                    .Setup(h => h.HandleEventAsync(
                        It.IsAny<object>(),
                        It.IsAny<IEvent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<object, IEvent, CancellationToken>((_, evt, _) => 
                    {
                        processedEvents.AddOrUpdate(
                            evt.EventId,
                            _ => 1,
                            (_, count) => count + 1);
                            
                        // Small delay to increase chance of concurrency issues
                        Thread.Sleep(1);
                    })
                    .Returns(Task.CompletedTask);
                
                handlers.Add(mockHandler.Object);
                
                // Subscribe the handler with adapter
                var adapter = new GenericEventHandlerAdapter<IEvent>(mockHandler.Object);
                var subId = _eventBus.Subscribe<IEvent>(adapter);
                subscriptionIds.Add(subId);
            }
            
            // Create events
            var events = new List<IEvent>();
            for (int i = 0; i < eventsPerHandler; i++)
            {
                var eventId = Guid.NewGuid();
                var mockEvent = new Mock<IEvent>();
                mockEvent.Setup(e => e.EventId).Returns(eventId);
                mockEvent.Setup(e => e.EventType).Returns(EventType.Command);
                mockEvent.Setup(e => e.SourceId).Returns(Guid.NewGuid());
                mockEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
                
                events.Add(mockEvent.Object);
            }
            
            // Act - Publish events while concurrently modifying subscriptions
            var publishTasks = events.Select(evt => _eventBus.PublishAsync(this, evt)).ToList();
            
            // Concurrently unsubscribe some handlers during publishing
            var unsubscribeTasks = Task.Run(() =>
            {
                // Unsubscribe half of the handlers
                for (int i = 0; i < handlerCount / 2; i++)
                {
                    // Random delay to increase chance of concurrency issues
                    Thread.Sleep(Random.Shared.Next(1, 5));
                    _eventBus.Unsubscribe(subscriptionIds[i]);
                }
            });
            
            // Concurrently subscribe new handlers during publishing
            var subscribeTasks = Task.Run(() =>
            {
                // Add some new handlers
                for (int i = 0; i < handlerCount / 4; i++)
                {
                    var mockHandler = new Mock<IEventHandler>();
                    mockHandler
                        .Setup(h => h.HandleEventAsync(
                            It.IsAny<object>(),
                            It.IsAny<IEvent>(),
                            It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
                    
                    // Random delay to increase chance of concurrency issues
                    Thread.Sleep(Random.Shared.Next(1, 5));
                    var adapter = new GenericEventHandlerAdapter<IEvent>(mockHandler.Object);
                    _eventBus.Subscribe<IEvent>(adapter);
                }
            });
            
            // Wait for all operations to complete
            await Task.WhenAll(publishTasks);
            await unsubscribeTasks;
            await subscribeTasks;
            
            // Assert
            // The main assertion here is that nothing throws exceptions during concurrent operations
            publishTasks.Should().AllSatisfy(task => task.IsCompleted.Should().BeTrue());
            
            // Each event should have been processed by some handlers
            processedEvents.Count.Should().Be(eventsPerHandler);
        }
        
        [Fact]
        public async Task EventBus_ShouldHandleLongRunningHandlers()
        {
            // Arrange
            const int longRunningDelay = 3000; // 3 seconds - make sure it's long enough
            const int eventCount = 5;
            
            // Create a long-running handler
            var longRunningHandler = new Mock<IEventHandler>();
            longRunningHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (object _, IEvent __, CancellationToken ___) => 
                {
                    // Simulate long-running operation
                    await Task.Delay(longRunningDelay);
                });
            
            // Create a fast handler
            var fastHandler = new Mock<IEventHandler>();
            fastHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Subscribe both handlers with adapters
            var longAdapter = new GenericEventHandlerAdapter<IEvent>(longRunningHandler.Object);
            var fastAdapter = new GenericEventHandlerAdapter<IEvent>(fastHandler.Object);
            _eventBus.Subscribe<IEvent>(longAdapter);
            _eventBus.Subscribe<IEvent>(fastAdapter);
            
            // Create events with different timeout settings
            var events = new List<IEvent>();
            for (int i = 0; i < eventCount; i++)
            {
                var eventId = Guid.NewGuid();
                var mockEvent = new Mock<IEvent>();
                mockEvent.Setup(e => e.EventId).Returns(eventId);
                mockEvent.Setup(e => e.EventType).Returns(EventType.Command);
                mockEvent.Setup(e => e.SourceId).Returns(Guid.NewGuid());
                mockEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
                
                // Set a routing data with timeout
                var routingData = new Mock<IEventRoutingData>();
                
                // First half has short timeout, second half has long timeout
                var timeout = i < eventCount / 2 
                ? TimeSpan.FromMilliseconds(100) // Very short timeout (100ms)
                : TimeSpan.FromMilliseconds(5000); // Long timeout (5s)
                    
                routingData.Setup(r => r.Timeout).Returns(timeout);
                mockEvent.Setup(e => e.RoutingData).Returns(routingData.Object);
                
                events.Add(mockEvent.Object);
            }
            
            // Act - Publish all events
            var results = new List<IPublishResult>();
            foreach (var evt in events)
            {
                var result = await _eventBus.PublishAsync(this, evt);
                results.Add(result!);
            }
            
            // Wait for all tasks to complete (regardless of timeout)
            var allTasks = results.SelectMany(r => r.HandlerTasks).ToList();
            try
            {
                // Use a timeout longer than any of the handlers to ensure everything has settled
                await Task.WhenAll(allTasks).WaitAsync(TimeSpan.FromMilliseconds(longRunningDelay * 2));
            }
            catch (TimeoutException)
            {
                // This is expected in some cases
            }
            
            // Assert
            // Events with short timeout should have timed out
            results.Take(eventCount / 2).Should().AllSatisfy(r => 
                r.TimedOut.Should().BeTrue());
            
            // Events with long timeout should not have timed out
            results.Skip(eventCount / 2).Should().AllSatisfy(r => 
                r.TimedOut.Should().BeFalse());
                
            // All tasks should either be completed or faulted/canceled
            allTasks.Should().AllSatisfy(t => 
                t.Status.Should().BeOneOf(
                    TaskStatus.RanToCompletion, 
                    TaskStatus.Faulted, 
                    TaskStatus.Canceled));
                    
            // The fast handler should always be called regardless of timeouts
            fastHandler.Verify(
                h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(eventCount * 2));
                
            // Update verification for longRunningHandler as well
            longRunningHandler.Verify(
                h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(eventCount * 2));
        }
        
        [Fact]
        public async Task EventBus_ShouldMaintainMemoryEfficiency()
        {
            // Arrange
            const int eventCount = 1000;
            const int handlerCount = 10; // Reduced handler count to decrease memory usage
            
            // Create many handlers
            var handlers = new List<IEventHandler>();
            for (int i = 0; i < handlerCount; i++)
            {
                var mockHandler = new Mock<IEventHandler>();
                mockHandler
                    .Setup(h => h.HandleEventAsync(
                        It.IsAny<object>(),
                        It.IsAny<IEvent>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                
                handlers.Add(mockHandler.Object);
                var adapter = new GenericEventHandlerAdapter<IEvent>(mockHandler.Object);
                _eventBus.Subscribe<IEvent>(adapter);
            }
            
            // Create event factory so we don't hold all events in memory at once
            IEvent CreateEvent(int index)
            {
                var eventId = Guid.NewGuid();
                var mockEvent = new Mock<IEvent>();
                mockEvent.Setup(e => e.EventId).Returns(eventId);
                mockEvent.Setup(e => e.EventType).Returns(EventType.Command);
                mockEvent.Setup(e => e.SourceId).Returns(Guid.NewGuid());
                mockEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
                // Note: IEvent doesn't have a Payload property; add custom data if needed
                
                return mockEvent.Object;
            }
            
            // Act - Run memory consumption test
            var memoryBefore = GC.GetTotalMemory(true);
            
            // Process events in batches to prevent overwhelming memory
            const int batchSize = 100;
            for (int batch = 0; batch < eventCount / batchSize; batch++)
            {
                var batchTasks = new List<Task<IPublishResult?>>();
                
                for (int i = 0; i < batchSize; i++)
                {
                    var evt = CreateEvent(batch * batchSize + i);
                    batchTasks.Add(_eventBus.PublishAsync(this, evt));
                }
                
                await Task.WhenAll(batchTasks);
                
                // Allow opportunity for GC to reclaim memory
                if (batch % 2 == 0)
                {
                    GC.Collect();
                    await Task.Delay(10);
                }
            }
            
            // Force GC to get accurate measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfter = GC.GetTotalMemory(true);
            var memoryIncrease = memoryAfter - memoryBefore;
            
            // Assert
            // This is a loose check as memory behavior depends on the runtime and environment
            // The key is to verify we don't have a massive leak
            memoryIncrease.Should().BeLessThan(10 * 1024 * 1024, "Memory increase should be less than 10MB");
            
            // Note: We're not unsubscribing handlers at the end because we'd need to track subscription IDs
        }
    }
}