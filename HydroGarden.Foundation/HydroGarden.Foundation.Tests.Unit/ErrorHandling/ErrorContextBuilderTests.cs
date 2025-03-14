using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.ErrorHandling
{
    public class ErrorContextBuilderTests
    {
        [Fact]
        public void Create_ShouldReturnNewBuilderInstance()
        {
            // Act
            var builder = ErrorContextBuilder.Create();

            // Assert
            builder.Should().NotBeNull();
            builder.Should().BeOfType<ErrorContextBuilder>();
        }

        [Fact]
        public void WithDevice_ShouldAddDeviceInformation()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var deviceName = "Test Device";

            // Act
            var context = ErrorContextBuilder.Create()
                .WithDevice(deviceId, deviceName)
                .Build();

            // Assert
            context.Should().ContainKey("DeviceId");
            context["DeviceId"].Should().Be(deviceId);
            context.Should().ContainKey("DeviceName");
            context["DeviceName"].Should().Be(deviceName);
        }

        [Fact]
        public void WithDevice_NullDeviceName_ShouldAddOnlyDeviceId()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            // Act
            var context = ErrorContextBuilder.Create()
                .WithDevice(deviceId)
                .Build();

            // Assert
            context.Should().ContainKey("DeviceId");
            context["DeviceId"].Should().Be(deviceId);
            context.Should().NotContainKey("DeviceName");
        }

        [Fact]
        public void WithSource_ShouldAddSourceInformation()
        {
            // Arrange
            var source = new object();

            // Act
            var context = ErrorContextBuilder.Create()
                .WithSource(source)
                .Build();

            // Assert
            context.Should().ContainKey("SourceType");
            context["SourceType"].Should().Be(typeof(object).FullName);
        }

        [Fact]
        public void WithSource_ForComponent_ShouldAddComponentInformation()
        {
            // Arrange
            var componentId = Guid.NewGuid();
            var componentName = "Test Component";
            var componentState = ComponentState.Running;
            
            var mockComponent = new Mock<IComponent>();
            mockComponent.SetupGet(c => c.Id).Returns(componentId);
            mockComponent.SetupGet(c => c.Name).Returns(componentName);
            mockComponent.SetupGet(c => c.State).Returns(componentState);

            // Act
            var context = ErrorContextBuilder.Create()
                .WithSource(mockComponent.Object)
                .Build();

            // Assert
            context.Should().ContainKey("SourceType");
            context.Should().ContainKey("ComponentId");
            context.Should().ContainKey("ComponentName");
            context.Should().ContainKey("ComponentState");
            context["ComponentId"].Should().Be(componentId);
            context["ComponentName"].Should().Be(componentName);
            context["ComponentState"].Should().Be(componentState.ToString());
        }

        [Fact]
        public void WithLocation_ShouldAddLocationInformation()
        {
            // Act
            var context = ErrorContextBuilder.Create()
                .WithLocation("TestMethod", "TestFile.cs", 42)
                .Build();

            // Assert
            context.Should().ContainKey("CallSite");
            context["CallSite"].Should().Be("TestFile.cs:TestMethod(42)");
        }

        [Fact]
        public void WithOperation_ShouldAddOperationInformation()
        {
            // Arrange
            var operationName = "TestOperation";

            // Act
            var context = ErrorContextBuilder.Create()
                .WithOperation(operationName)
                .Build();

            // Assert
            context.Should().ContainKey("Operation");
            context["Operation"].Should().Be(operationName);
        }

        [Fact]
        public void WithOperation_WithParameters_ShouldAddParameterInformation()
        {
            // Arrange
            var operationName = "TestOperation";
            var parameters = new { Id = 42, Name = "Test" };

            // Act
            var context = ErrorContextBuilder.Create()
                .WithOperation(operationName, parameters)
                .Build();

            // Assert
            context.Should().ContainKey("Operation");
            context["Operation"].Should().Be(operationName);
            context.Should().ContainKey("OperationParameterType");
            context["OperationParameterType"].Should().Be(parameters.GetType().Name);
        }

        [Fact]
        public void WithOperation_WithDictionary_ShouldAddDictionaryInformation()
        {
            // Arrange
            var operationName = "TestOperation";
            var parameters = new Dictionary<string, object>
            {
                { "Id", 42 },
                { "Name", "Test" }
            };

            // Act
            var context = ErrorContextBuilder.Create()
                .WithOperation(operationName, parameters)
                .Build();

            // Assert
            context.Should().ContainKey("Operation");
            context["Operation"].Should().Be(operationName);
            context.Should().ContainKey("OperationParameterCount");
            context["OperationParameterCount"].Should().Be(2);
            context.Should().ContainKey("OperationParameterKeys");
            context["OperationParameterKeys"].ToString().Should().Contain("Id");
            context["OperationParameterKeys"].ToString().Should().Contain("Name");
        }

        [Fact]
        public void WithErrorClassification_ShouldAddClassificationInformation()
        {
            // Arrange
            var errorCode = "TEST_ERROR";
            var severity = ErrorSeverity.Critical;
            var source = ErrorSource.Device;
            var category = ErrorCategory.Device;

            // Act
            var context = ErrorContextBuilder.Create()
                .WithErrorClassification(errorCode, severity, source, category)
                .Build();

            // Assert
            context.Should().ContainKey("ErrorCode");
            context.Should().ContainKey("ErrorSeverity");
            context.Should().ContainKey("ErrorSource");
            context.Should().ContainKey("ErrorCategory");
            context["ErrorCode"].Should().Be(errorCode);
            context["ErrorSeverity"].Should().Be(severity.ToString());
            context["ErrorSource"].Should().Be(source.ToString());
            context["ErrorCategory"].Should().Be(category.ToString());
        }

        [Fact]
        public void WithProperty_ShouldAddCustomProperty()
        {
            // Arrange
            var key = "CustomKey";
            var value = "CustomValue";

            // Act
            var context = ErrorContextBuilder.Create()
                .WithProperty(key, value)
                .Build();

            // Assert
            context.Should().ContainKey(key);
            context[key].Should().Be(value);
        }

        [Fact]
        public void WithProperties_ShouldAddMultipleProperties()
        {
            // Arrange
            var properties = new Dictionary<string, object>
            {
                { "Key1", "Value1" },
                { "Key2", 42 },
                { "Key3", true }
            };

            // Act
            var context = ErrorContextBuilder.Create()
                .WithProperties(properties)
                .Build();

            // Assert
            context.Should().ContainKey("Key1");
            context.Should().ContainKey("Key2");
            context.Should().ContainKey("Key3");
            context["Key1"].Should().Be("Value1");
            context["Key2"].Should().Be(42);
            context["Key3"].Should().Be(true);
        }

        [Fact]
        public void WithException_ShouldAddExceptionInformation()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception",
                new ArgumentException("Inner test exception"));

            // Act
            var context = ErrorContextBuilder.Create()
                .WithException(exception)
                .Build();

            // Assert
            context.Should().ContainKey("ExceptionType");
            context.Should().ContainKey("ExceptionMessage");
            context.Should().ContainKey("InnerExceptionType1");
            context.Should().ContainKey("InnerExceptionMessage1");
            context.Should().ContainKey("StackTraceHash");
            context.Should().ContainKey("HResult");
            
            context["ExceptionType"].Should().Be("InvalidOperationException");
            context["ExceptionMessage"].Should().Be("Test exception");
            context["InnerExceptionType1"].Should().Be("ArgumentException");
            context["InnerExceptionMessage1"].Should().Be("Inner test exception");
        }

        [Fact]
        public void WithCorrelation_ShouldAddCorrelationId()
        {
            // Arrange
            var correlationId = Guid.NewGuid();

            // Act
            var context = ErrorContextBuilder.Create()
                .WithCorrelation(correlationId)
                .Build();

            // Assert
            context.Should().ContainKey("CorrelationId");
            context["CorrelationId"].Should().Be(correlationId);
        }

        [Fact]
        public void Build_ShouldAddTimestampAndReturnDictionary()
        {
            // Act
            var context = ErrorContextBuilder.Create().Build();

            // Assert
            context.Should().ContainKey("ContextCreatedAt");
            // Parse the timestamp and ensure it's within 1 second of now, regardless of timezone
            var timestamp = DateTimeOffset.Parse(context["ContextCreatedAt"].ToString());
            timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void MultipleOperations_ShouldBuildCumulativeContext()
        {
            // Act
            var context = ErrorContextBuilder.Create()
                .WithDevice(Guid.NewGuid(), "Test Device")
                .WithLocation()
                .WithOperation("TestOperation")
                .WithProperty("CustomKey", "CustomValue")
                .WithErrorClassification("TEST_ERROR", ErrorSeverity.Error, ErrorSource.Device, ErrorCategory.Device)
                .Build();

            // Assert
            context.Should().ContainKey("DeviceId");
            context.Should().ContainKey("DeviceName");
            context.Should().ContainKey("CallSite");
            context.Should().ContainKey("Operation");
            context.Should().ContainKey("CustomKey");
            context.Should().ContainKey("ErrorCode");
            context.Should().ContainKey("ErrorSeverity");
            context.Should().ContainKey("ErrorSource");
            context.Should().ContainKey("ErrorCategory");
            context.Should().ContainKey("ContextCreatedAt");
        }
    }
}