using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Logger.Abstractions;
using Moq;

namespace HydroGarden.Foundation.Tests.Unit.EventBus
{
    public abstract class EventBusBaseTests
    {
        protected readonly Mock<ILogger> MockLogger;
        protected readonly Mock<IEventStore> MockStore;
        protected readonly Mock<IEventRetryPolicy> MockRetryPolicy;
        protected readonly Mock<IEventTransformer> MockTransformer;
        protected readonly Mock<ITopologyService> MockTopologyService;
        protected readonly Mock<IErrorMonitor> MockErrorMonitor;

        protected EventBusBaseTests()
        {
            MockLogger = new Mock<ILogger>();
            MockStore = new Mock<IEventStore>();
            MockRetryPolicy = new Mock<IEventRetryPolicy>();
            MockTransformer = new Mock<IEventTransformer>();
            MockTopologyService = new Mock<ITopologyService>();
            MockErrorMonitor = new Mock<IErrorMonitor>();

            MockTransformer.Setup(t => t.Transform(It.IsAny<IEvent>()))
                .Returns<IEvent>(e => e);
        }

        protected Common.Events.EventBus CreateTestEventBus()
        {
            var eventBus = new Common.Events.EventBus(
                MockLogger.Object);
            eventBus.SetTopologyService(MockTopologyService.Object);
            return eventBus;
        }
    }
}