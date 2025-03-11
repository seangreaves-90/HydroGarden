using FluentAssertions;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.Tests.ErrorHandling.Mocks;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Components
{
    /// <summary>
    /// Unit tests for the ErrorContextBuilder class.
    /// </summary>
    public class ErrorContextBuilderTests
    {
        [Fact]
        public void Create_ShouldReturnNewInstance()
        {
            // Arrange & Act
            var builder = ErrorContextBuilder.Create();

            // Assert
            builder.Should().NotBeNull();
            builder.Should().BeOfType<ErrorContextBuilder>();
        }

        [Fact]
        public void WithSource_WithGenericObject_ShouldAddSourceType()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();
            var source = new object();

            // Act
            var result = builder.WithSource(source);
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder); // Fluent interface returns same instance
            context.Should().ContainKey("SourceType");
            context["SourceType"].Should().Be(typeof(object).FullName);
        }

        [Fact]
        public void WithSource_WithComponent_ShouldAddComponentDetails()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();
            var component = new MockComponent("Test Component");

            // Act
            var result = builder.WithSource(component);
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("SourceType");
            context.Should().ContainKey("ComponentId");
            context.Should().ContainKey("ComponentName");
            context.Should().ContainKey("ComponentState");
            context["ComponentId"].Should().Be(component.Id);
            context["ComponentName"].Should().Be(component.Name);
            context["ComponentState"].Should().Be(component.State.ToString());
        }

        [Fact]
        public void WithLocation_ShouldAddCallerInfo()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();

            // Act
            var result = builder.WithLocation();
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("CallSite");
            // Can't check exact value as it depends on the caller, but can verify it's not empty
            context["CallSite"].ToString().Should().NotBeNullOrEmpty();
            context["CallSite"].ToString().Should().Contain("ErrorContextBuilderTests");
        }

        [Fact]
        public void WithOperation_ShouldAddOperationDetails()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();
            var parameters = new { Param1 = "Value1", Param2 = 42 };

            // Act
            var result = builder.WithOperation("TestOperation", parameters);
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("Operation");
            context.Should().ContainKey("OperationParameters");
            context["Operation"].Should().Be("TestOperation");
            context["OperationParameters"].Should().Be(parameters);
        }

        [Fact]
        public void WithOperation_WithoutParameters_ShouldAddOnlyOperationName()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();

            // Act
            var result = builder.WithOperation("TestOperation");
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("Operation");
            context["Operation"].Should().Be("TestOperation");
            context.Should().NotContainKey("OperationParameters");
        }

        [Fact]
        public void WithProperty_ShouldAddCustomProperty()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();

            // Act
            var result = builder.WithProperty("CustomKey", "CustomValue");
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("CustomKey");
            context["CustomKey"].Should().Be("CustomValue");
        }

        [Fact]
        public void WithProperties_ShouldAddMultipleProperties()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();
            var properties = new Dictionary<string, object>
            {
                ["Key1"] = "Value1",
                ["Key2"] = 42,
                ["Key3"] = true
            };

            // Act
            var result = builder.WithProperties(properties);
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("Key1");
            context.Should().ContainKey("Key2");
            context.Should().ContainKey("Key3");
            context["Key1"].Should().Be("Value1");
            context["Key2"].Should().Be(42);
            context["Key3"].Should().Be(true);
        }

        [Fact]
        public void WithException_ShouldAddExceptionDetails()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();
            var innerException = new ArgumentException("Inner exception message");
            var exception = new InvalidOperationException("Test exception message", innerException);

            // Act
            var result = builder.WithException(exception);
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("ExceptionType");
            context.Should().ContainKey("ExceptionMessage");
            context.Should().ContainKey("InnerExceptionType");
            context.Should().ContainKey("InnerExceptionMessage");
            context["ExceptionType"].Should().Be("InvalidOperationException");
            context["ExceptionMessage"].Should().Be("Test exception message");
            context["InnerExceptionType"].Should().Be("ArgumentException");
            context["InnerExceptionMessage"].Should().Be("Inner exception message");

            // Stack trace hash should be present if the exception has a stack trace
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                context.Should().ContainKey("StackTraceHash");
                context["StackTraceHash"].Should().NotBeNull();
            }
        }

        [Fact]
        public void WithException_WithoutInnerException_ShouldAddOnlyMainExceptionDetails()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();
            var exception = new InvalidOperationException("Test exception message");

            // Act
            var result = builder.WithException(exception);
            var context = result.Build();

            // Assert
            result.Should().BeSameAs(builder);
            context.Should().ContainKey("ExceptionType");
            context.Should().ContainKey("ExceptionMessage");
            context.Should().NotContainKey("InnerExceptionType");
            context.Should().NotContainKey("InnerExceptionMessage");
            context["ExceptionType"].Should().Be("InvalidOperationException");
            context["ExceptionMessage"].Should().Be("Test exception message");

            // Stack trace hash should be present if the exception has a stack trace
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                context.Should().ContainKey("StackTraceHash");
                context["StackTraceHash"].Should().NotBeNull();
            }
        }

        [Fact]
        public void Build_ShouldAddContextCreatedAtTimestamp()
        {
            // Arrange
            var builder = ErrorContextBuilder.Create();

            // Act
            var context = builder.Build();

            // Assert
            context.Should().ContainKey("ContextCreatedAt");
            var timestamp = context["ContextCreatedAt"].ToString();
            timestamp.Should().NotBeNullOrEmpty();
            
            // Validate it's a proper ISO-8601 timestamp that can be parsed
            var parsedTime = DateTimeOffset.Parse(timestamp);
            parsedTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void ChainedMethods_ShouldCombineAllContext()
        {
            // Arrange
            var component = new MockComponent("Test Component");
            var exception = new InvalidOperationException("Test exception");

            // Act
            var context = ErrorContextBuilder.Create()
                .WithSource(component)
                .WithLocation()
                .WithOperation("TestOperation", new { Param = "Value" })
                .WithProperty("CustomKey", "CustomValue")
                .WithException(exception)
                .Build();

            // Assert
            context.Should().ContainKey("SourceType");
            context.Should().ContainKey("ComponentId");
            context.Should().ContainKey("CallSite");
            context.Should().ContainKey("Operation");
            context.Should().ContainKey("OperationParameters");
            context.Should().ContainKey("CustomKey");
            context.Should().ContainKey("ExceptionType");
            context.Should().ContainKey("ContextCreatedAt");
        }
    }
}
