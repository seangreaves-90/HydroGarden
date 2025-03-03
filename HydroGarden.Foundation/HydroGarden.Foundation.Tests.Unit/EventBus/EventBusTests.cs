using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using Moq;

namespace HydroGarden.Foundation.Tests.Unit.Events
{
    /// <summary>
    /// Base test class for the EventBus that contains the testing infrastructure.
    /// </summary>
    public abstract class EventBusBaseTests
    {
        protected readonly Mock<IHydroGardenLogger> MockLogger;
        protected readonly Mock<IEventStore> MockStore;
        protected readonly Mock<IEventRetryPolicy> MockRetryPolicy;
        protected readonly Mock<IEventTransformer> MockTransformer;
        protected readonly Mock<ITopologyService> MockTopologyService;

        protected EventBusBaseTests()
        {
            MockLogger = new Mock<IHydroGardenLogger>();
            MockStore = new Mock<IEventStore>();
            MockRetryPolicy = new Mock<IEventRetryPolicy>();
            MockTransformer = new Mock<IEventTransformer>();
            MockTopologyService = new Mock<ITopologyService>();

            // Set up transformer to return the same event by default (identity transform)
            MockTransformer.Setup(t => t.Transform(It.IsAny<IHydroGardenEvent>()))
                .Returns<IHydroGardenEvent>(e => e);
        }

        /// <summary>
        /// Creates a real EventBus with mocked dependencies for testing.
        /// </summary>
        protected EventBus CreateTestEventBus()
        {
            var eventBus = new EventBus(
                MockLogger.Object,
                MockStore.Object,
                MockRetryPolicy.Object,
                MockTransformer.Object,
                1); // Use 1 for concurrency to make tests more predictable

            // Set the topology service explicitly
            eventBus.SetTopologyService(MockTopologyService.Object);

            return eventBus;
        }
    }
}