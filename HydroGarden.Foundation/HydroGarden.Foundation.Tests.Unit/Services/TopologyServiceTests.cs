using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Core.Services;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Services
{
    public class TopologyServiceTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IStore> _mockStore;
        private readonly Mock<IPersistenceService> _mockPersistenceService;
        private readonly TopologyService _topologyService;
        private readonly Guid _topologyStoreId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        public TopologyServiceTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockStore = new Mock<IStore>();
            _mockPersistenceService = new Mock<IPersistenceService>();

            // Setup mock store to return empty data initially
            _mockStore.Setup(s => s.LoadAsync(_topologyStoreId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, object>?)null);

            _topologyService = new TopologyService(_mockLogger.Object, _mockStore.Object, _mockPersistenceService.Object);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLoadConnectionsFromStore()
        {
            // Arrange
            var connection1 = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "Test",
                IsEnabled = true
            };

            var connections = new List<ComponentConnection> { connection1 };

            var connectionsData = new Dictionary<string, object>
            {
                ["Connections"] = connections
            };

            _mockStore.Setup(s => s.LoadAsync(_topologyStoreId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connectionsData);

            // Act
            await _topologyService.InitializeAsync();
            var sourceConnections = await _topologyService.GetConnectionsForSourceAsync(connection1.SourceId);
            var targetConnections = await _topologyService.GetConnectionsForTargetAsync(connection1.TargetId);

            // Assert
            sourceConnections.Should().HaveCount(1);
            sourceConnections[0].ConnectionId.Should().Be(connection1.ConnectionId);

            targetConnections.Should().HaveCount(1);
            targetConnections[0].ConnectionId.Should().Be(connection1.ConnectionId);
        }

        [Fact]
        public async Task CreateConnectionAsync_ShouldAddConnectionAndSaveToStore()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "Test",
                IsEnabled = true
            };

            // Act
            var createdConnection = await _topologyService.CreateConnectionAsync(connection);
            var sourceConnections = await _topologyService.GetConnectionsForSourceAsync(sourceId);
            var targetConnections = await _topologyService.GetConnectionsForTargetAsync(targetId);

            // Assert
            createdConnection.Should().NotBeNull();
            createdConnection.ConnectionId.Should().NotBe(Guid.Empty);

            sourceConnections.Should().HaveCount(1);
            sourceConnections[0].ConnectionId.Should().Be(createdConnection.ConnectionId);

            targetConnections.Should().HaveCount(1);
            targetConnections[0].ConnectionId.Should().Be(createdConnection.ConnectionId);

            _mockStore.Verify(s => s.SaveAsync(
                _topologyStoreId,
                It.Is<IDictionary<string, object>>(d =>
                    d.ContainsKey("Connections") &&
                    ((List<ComponentConnection>)d["Connections"]).Count == 1),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateConnectionAsync_WithExistingId_ShouldThrowException()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "Test",
                IsEnabled = true
            };

            // Create the first connection
            await _topologyService.CreateConnectionAsync(connection);

            // Try to create another connection with the same ID
            var connection2 = new ComponentConnection
            {
                ConnectionId = connection.ConnectionId, // Same ID
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "Test2",
                IsEnabled = true
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _topologyService.CreateConnectionAsync(connection2));
        }

        [Fact]
        public async Task UpdateConnectionAsync_ShouldUpdateConnectionAndSaveToStore()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "Test",
                IsEnabled = true
            };

            var createdConnection = await _topologyService.CreateConnectionAsync(connection);

            // Modify connection
            var updatedConnection = new ComponentConnection
            {
                ConnectionId = createdConnection.ConnectionId,
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "Updated",
                IsEnabled = false
            };

            // Act
            var updateResult = await _topologyService.UpdateConnectionAsync(updatedConnection);
            var sourceConnections = await _topologyService.GetConnectionsForSourceAsync(sourceId);

            // Assert
            updateResult.Should().BeTrue();
            sourceConnections.Should().BeEmpty(); // Connection is disabled now

            _mockStore.Verify(s => s.SaveAsync(
                _topologyStoreId,
                It.Is<IDictionary<string, object>>(d =>
                    d.ContainsKey("Connections") &&
                    ((List<ComponentConnection>)d["Connections"]).Count == 1),
                It.IsAny<CancellationToken>()),
                Times.Exactly(2)); // Once for create, once for update
        }

        [Fact]
        public async Task DeleteConnectionAsync_ShouldRemoveConnectionAndSaveToStore()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "Test",
                IsEnabled = true
            };

            var createdConnection = await _topologyService.CreateConnectionAsync(connection);

            // Act
            var deleteResult = await _topologyService.DeleteConnectionAsync(createdConnection.ConnectionId);
            var sourceConnections = await _topologyService.GetConnectionsForSourceAsync(sourceId);
            var targetConnections = await _topologyService.GetConnectionsForTargetAsync(targetId);

            // Assert
            deleteResult.Should().BeTrue();
            sourceConnections.Should().BeEmpty();
            targetConnections.Should().BeEmpty();

            _mockStore.Verify(s => s.SaveAsync(
                _topologyStoreId,
                It.Is<IDictionary<string, object>>(d =>
                    d.ContainsKey("Connections") &&
                    ((List<ComponentConnection>)d["Connections"]).Count == 0),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EvaluateConnectionConditionAsync_NoCondition_ShouldReturnTrue()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "Test",
                IsEnabled = true,
                Condition = null // No condition
            };

            // Act
            var result = await _topologyService.EvaluateConnectionConditionAsync(connection);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateConnectionConditionAsync_WithSimpleCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "Test",
                IsEnabled = true,
                Condition = "source.Temperature > 25" // Condition referencing source device
            };

            // Set up persistence service to return a property value (Temperature = 30)
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    sourceId, "Temperature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(30);

            // Act
            var result = await _topologyService.EvaluateConnectionConditionAsync(connection);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateConnectionConditionAsync_WithFalseCondition_ShouldReturnFalse()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "Test",
                IsEnabled = true,
                Condition = "source.Temperature > 25" // Condition referencing source device
            };

            // Set up persistence service to return a property value (Temperature = 20)
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    sourceId, "Temperature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(20);

            // Act
            var result = await _topologyService.EvaluateConnectionConditionAsync(connection);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task EvaluateConnectionConditionAsync_WithTargetCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "Test",
                IsEnabled = true,
                Condition = "target.IsActive == true" // Condition referencing target device
            };

            // Set up persistence service to return a property value (IsActive = true)
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    targetId, "IsActive", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _topologyService.EvaluateConnectionConditionAsync(connection);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EvaluateConnectionConditionAsync_WithInvalidCondition_ShouldReturnFalse()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var connection = new ComponentConnection
            {
                ConnectionId = connectionId,
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "Test",
                IsEnabled = true,
                Condition = "InvalidConditionFormat" // Invalid format - no operator
            };

            // Setup a clear failure for specific format - must be placed before invocation
            // This will ensure ConditionEvaluator throws ArgumentException
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid condition format"));

            // Act
            var result = await _topologyService.EvaluateConnectionConditionAsync(connection);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(
                It.IsAny<Exception>(),
                It.Is<string>(s => s.Contains("Error evaluating condition"))),
                Times.Once);
        }

        [Fact]
        public async Task GetConnectionsForSource_WithMultipleConnections_ShouldFilterBySource()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId1 = Guid.NewGuid();
            var targetId2 = Guid.NewGuid();

            // Create two connections from the same source
            var connection1 = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId1,
                ConnectionType = "Test1",
                IsEnabled = true
            };

            var connection2 = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId2,
                ConnectionType = "Test2",
                IsEnabled = true
            };

            await _topologyService.CreateConnectionAsync(connection1);
            await _topologyService.CreateConnectionAsync(connection2);

            // Act
            var sourceConnections = await _topologyService.GetConnectionsForSourceAsync(sourceId);

            // Assert
            sourceConnections.Should().HaveCount(2);
            sourceConnections.Should().Contain(c => c.TargetId == targetId1);
            sourceConnections.Should().Contain(c => c.TargetId == targetId2);
        }

        [Fact]
        public async Task GetConnectionsForTarget_WithMultipleConnections_ShouldFilterByTarget()
        {
            // Arrange
            var sourceId1 = Guid.NewGuid();
            var sourceId2 = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            // Create two connections to the same target
            var connection1 = new ComponentConnection
            {
                SourceId = sourceId1,
                TargetId = targetId,
                ConnectionType = "Test1",
                IsEnabled = true
            };

            var connection2 = new ComponentConnection
            {
                SourceId = sourceId2,
                TargetId = targetId,
                ConnectionType = "Test2",
                IsEnabled = true
            };

            await _topologyService.CreateConnectionAsync(connection1);
            await _topologyService.CreateConnectionAsync(connection2);

            // Act
            var targetConnections = await _topologyService.GetConnectionsForTargetAsync(targetId);

            // Assert
            targetConnections.Should().HaveCount(2);
            targetConnections.Should().Contain(c => c.SourceId == sourceId1);
            targetConnections.Should().Contain(c => c.SourceId == sourceId2);
        }
    }
}