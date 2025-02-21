using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Core.Devices;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Devices
{
    public class TestIoTDevice : IoTDeviceBase
    {
        public TestIoTDevice(
            Guid id,
            string displayName,
            IPropertyManager properties,
            ILogger logger)
            : base(id, displayName, "Test", properties, logger)
        {
        }

        public override Task ExecuteCoreAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    public class IoTDeviceTests
    {
        private readonly Mock<IPropertyManager> _propertyManagerMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly TestIoTDevice _device;

        public IoTDeviceTests()
        {
            _propertyManagerMock = new Mock<IPropertyManager>();
            _loggerMock = new Mock<ILogger>();
            _device = new TestIoTDevice(
                Guid.NewGuid(),
                "Test Device",
                _propertyManagerMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Initialize_ShouldRegisterCoreProperties()
        {
            // Arrange
            var savedProperties = new Dictionary<string, (string, bool)>();
            _propertyManagerMock
                .Setup(x => x.SetPropertyAsync<string>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<IPropertyValidator<IValidationResult, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, bool, IPropertyValidator<IValidationResult, string>, CancellationToken>(
                    (name, value, isReadOnly, validator, ct) => savedProperties[name] = (value, isReadOnly))
                .Returns(Task.CompletedTask);

            // Act
            await _device.InitializeAsync(CancellationToken.None);

            // Assert
            savedProperties.Should().ContainKey("Id");
            savedProperties.Should().ContainKey("DisplayName");
            savedProperties.Should().ContainKey("DeviceType");
            savedProperties["Id"].Item2.Should().BeTrue(); // Should be readonly
            savedProperties["DisplayName"].Item2.Should().BeTrue();
            savedProperties["DeviceType"].Item2.Should().BeTrue();
        }
    }
}