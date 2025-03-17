//using FluentAssertions;
//using HydroGarden.Foundation.Abstractions.Interfaces.Events;
//using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
//using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
//using HydroGarden.Foundation.Abstractions.Interfaces.Services;
//using HydroGarden.Logger.Abstractions;
//using Moq;
//using Xunit;

//namespace HydroGarden.Foundation.Tests.Unit.EventBus
//{
//    public class EventBusInitializationTests : EventBusBaseTests
//    {
//        [Fact]
//        public void EventBus_ShouldInitializeWithAllDependencies()
//        {
//            // Arrange & Act
//            using var eventBus = new Common.Events.EventBus(
//                MockLogger.Object,
//                MockEventRouter.Object,
//                MockStore.Object,
//                MockTransformer.Object);

//            // Assert
//            eventBus.Should().NotBeNull();
//            eventBus.GetTopologyService().Should().BeNull();
//        }

//        [Fact]
//        public void EventBus_ShouldThrowWithNullLogger()
//        {
//            // Arrange & Act & Assert
//            Action act = () => new Common.Events.EventBus(
//                null!,
//                MockEventRouter.Object,
//                MockStore.Object,
//                MockTransformer.Object);

//            act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("logger");
//        }

//        [Fact]
//        public void EventBus_ShouldThrowWithNullRouter()
//        {
//            // Arrange & Act & Assert
//            Action act = () => new Common.Events.EventBus(
//                MockLogger.Object,
//                null!,
//                MockStore.Object,
//                MockTransformer.Object);

//            act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("router");
//        }

//        [Fact]
//        public void EventBus_ShouldAllowNullOptionalDependencies()
//        {
//            // Arrange & Act
//            using var eventBus = new Common.Events.EventBus(
//                MockLogger.Object,
//                MockEventRouter.Object);

//            // Assert
//            eventBus.Should().NotBeNull();
//            eventBus.GetTopologyService().Should().BeNull();
//        }
//    }

//}//using FluentAssertions;
//using HydroGarden.Foundation.Abstractions.Interfaces.Events;
//using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
//using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
//using HydroGarden.Foundation.Abstractions.Interfaces.Services;
//using HydroGarden.Logger.Abstractions;
//using Moq;
//using Xunit;

//namespace HydroGarden.Foundation.Tests.Unit.EventBus
//{
//    public class EventBusInitializationTests : EventBusBaseTests
//    {
//        [Fact]
//        public void EventBus_ShouldInitializeWithAllDependencies()
//        {
//            // Arrange & Act
//            using var eventBus = new Common.Events.EventBus(
//                MockLogger.Object,
//                MockEventRouter.Object,
//                MockStore.Object,
//                MockTransformer.Object);

//            // Assert
//            eventBus.Should().NotBeNull();
//            eventBus.GetTopologyService().Should().BeNull();
//        }

//        [Fact]
//        public void EventBus_ShouldThrowWithNullLogger()
//        {
//            // Arrange & Act & Assert
//            Action act = () => new Common.Events.EventBus(
//                null!,
//                MockEventRouter.Object,
//                MockStore.Object,
//                MockTransformer.Object);

//            act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("logger");
//        }

//        [Fact]
//        public void EventBus_ShouldThrowWithNullRouter()
//        {
//            // Arrange & Act & Assert
//            Action act = () => new Common.Events.EventBus(
//                MockLogger.Object,
//                null!,
//                MockStore.Object,
//                MockTransformer.Object);

//            act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("router");
//        }

//        [Fact]
//        public void EventBus_ShouldAllowNullOptionalDependencies()
//        {
//            // Arrange & Act
//            using var eventBus = new Common.Events.EventBus(
//                MockLogger.Object,
//                MockEventRouter.Object);

//            // Assert
//            eventBus.Should().NotBeNull();
//            eventBus.GetTopologyService().Should().BeNull();
//        }
//    }

//}