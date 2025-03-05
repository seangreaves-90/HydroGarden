using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Core.Services;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Services
{
    public class ConditionEvaluatorTests
    {
        private readonly Mock<IPersistenceService> _mockPersistenceService;
        private readonly ConditionEvaluator _conditionEvaluator;
        private readonly Guid _sourceId = Guid.NewGuid();
        private readonly Guid _targetId = Guid.NewGuid();

        public ConditionEvaluatorTests()
        {
            _mockPersistenceService = new Mock<IPersistenceService>();
            _conditionEvaluator = new ConditionEvaluator(_mockPersistenceService.Object);
        }

        [Fact]
        public async Task EvaluateAsync_WithEmptyCondition_ShouldReturnTrue()
        {
            // Arrange
            string condition = "";

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithNullCondition_ShouldReturnTrue()
        {
            // Arrange
            string? condition = null;

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition!);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithSourceProperty_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "Temperature > 25";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Temperature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(30);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithExplicitSourcePrefix_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "source.Temperature > 25";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Temperature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(30);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithTargetPrefix_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "target.IsActive == true";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _targetId, "IsActive", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithFalseCondition_ShouldReturnFalse()
        {
            // Arrange
            string condition = "Temperature > 25";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Temperature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(20);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task EvaluateAsync_WithInvalidPropertyFormat_ShouldReturnFalse()
        {
            // Arrange
            string condition = "InvalidFormatNoOperator";

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task EvaluateAsync_WithNonExistentProperty_ShouldReturnFalse()
        {
            // Arrange
            string condition = "NonExistentProperty > 25";

            // Set up persistence service to return null
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "NonExistentProperty", It.IsAny<CancellationToken>()))
                .ReturnsAsync((object?)null);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task EvaluateAsync_WithStringEqualityCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "Status == \"Ready\"";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Status", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Ready");

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithLessThanCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "Counter < 10";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Counter", It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithGreaterThanOrEqualCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "Counter >= 10";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Counter", It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithLessThanOrEqualCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "Counter <= 10";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Counter", It.IsAny<CancellationToken>()))
                .ReturnsAsync(11);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task EvaluateAsync_WithNotEqualCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "Status != \"Ready\"";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Status", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Busy");

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithBooleanCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "IsEnabled == true";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "IsEnabled", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateAsync_WithDoubleCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            string condition = "Temperature > 25.5";

            // Set up persistence service to return property value
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    _sourceId, "Temperature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(25.6);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(_sourceId, _targetId, condition);

            // Assert
            result.Should().BeTrue();
        }
    }
}