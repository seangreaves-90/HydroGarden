using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
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
        private readonly Mock<IPersistenceService> _mockPersistenceService;
        private readonly Mock<IPersistenceTransaction> _mockTransaction;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        private readonly TopologyService _service;

        public TopologyServiceTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockPersistenceService = new Mock<IPersistenceService>();
            _mockTransaction = new Mock<IPersistenceTransaction>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();

            _mockPersistenceService.Setup(p => p.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _service = new TopologyService(_mockLogger.Object, _mockPersistenceService.Object);
        }

        [Fact]
        public async Task InitializeAsync_Should_LoadConnectionsFromPersistenceService()
        {
            // Arrange
            var connections = new List<IComponentConnection>
            {
                new ComponentConnection
                {
                    ConnectionId = Guid.NewGuid(),
                    SourceId = Guid.NewGuid(),
                    TargetId = Guid.NewGuid(),
                    ConnectionType = "TestType",
                    IsEnabled = true
                }
            };

            _mockPersistenceService.Setup(p => p.GetAllConnectionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connections);

            // Act
            await _service.InitializeAsync();

            // Assert
            _mockPersistenceService.Verify(p => p.GetAllConnectionsAsync(It.IsAny<CancellationToken>()), Times.Once);
            
            // Verify connections were loaded correctly
            var sourceConnections = await _service.GetConnectionsForSourceAsync(connections[0].SourceId);
            Assert.Single(sourceConnections);
            Assert.Equal(connections[0].ConnectionId, sourceConnections[0].ConnectionId);
        }

        [Fact]
        public async Task CreateConnectionAsync_Should_StoreConnectionInPersistenceService()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // Act
            var result = await _service.CreateConnectionAsync(connection);

            // Assert
            _mockPersistenceService.Verify(p => p.StoreConnectionAsync(
                It.Is<IComponentConnection>(c => c.ConnectionId == connection.ConnectionId),
                It.IsAny<CancellationToken>()),
                Times.Once);
                
            Assert.Equal(connection.ConnectionId, result.ConnectionId);
        }

        [Fact]
        public async Task UpdateConnectionAsync_Should_StoreUpdatedConnectionInPersistenceService()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // First create the connection
            await _service.CreateConnectionAsync(connection);
            
            // Modify the connection
            connection.IsEnabled = false;
            connection.ConnectionType = "UpdatedType";

            // Act
            var result = await _service.UpdateConnectionAsync(connection);

            // Assert
            Assert.True(result);
            _mockPersistenceService.Verify(p => p.StoreConnectionAsync(
                It.Is<IComponentConnection>(c => 
                    c.ConnectionId == connection.ConnectionId && 
                    c.ConnectionType == "UpdatedType" && 
                    c.IsEnabled == false),
                It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task UpdateConnectionAsync_Should_ReturnFalse_WhenConnectionDoesNotExist()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // Act
            var result = await _service.UpdateConnectionAsync(connection);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteConnectionAsync_Should_DeleteConnectionFromPersistenceService()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // First create the connection
            await _service.CreateConnectionAsync(connection);
            
            _mockPersistenceService.Setup(p => p.DeleteConnectionAsync(
                    It.Is<Guid>(id => id == connection.ConnectionId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteConnectionAsync(connection.ConnectionId);

            // Assert
            Assert.True(result);
            _mockPersistenceService.Verify(p => p.DeleteConnectionAsync(
                It.Is<Guid>(id => id == connection.ConnectionId),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetConnectionsForSourceAsync_Should_ReturnConnectionsWithMatchingSource()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            
            var connection1 = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "TestType1",
                IsEnabled = true
            };
            
            var connection2 = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = sourceId,
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType2",
                IsEnabled = true
            };
            
            var connection3 = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = targetId,
                ConnectionType = "TestType3",
                IsEnabled = true
            };

            // Create the connections
            await _service.CreateConnectionAsync(connection1);
            await _service.CreateConnectionAsync(connection2);
            await _service.CreateConnectionAsync(connection3);

            // Act
            var result = await _service.GetConnectionsForSourceAsync(sourceId);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.ConnectionId == connection1.ConnectionId);
            Assert.Contains(result, c => c.ConnectionId == connection2.ConnectionId);
        }

        [Fact]
        public async Task GetConnectionsForTargetAsync_Should_ReturnConnectionsWithMatchingTarget()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            
            var connection1 = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "TestType1",
                IsEnabled = true
            };
            
            var connection2 = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = targetId,
                ConnectionType = "TestType2",
                IsEnabled = true
            };
            
            var connection3 = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = sourceId,
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType3",
                IsEnabled = true
            };

            // Create the connections
            await _service.CreateConnectionAsync(connection1);
            await _service.CreateConnectionAsync(connection2);
            await _service.CreateConnectionAsync(connection3);

            // Act
            var result = await _service.GetConnectionsForTargetAsync(targetId);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.ConnectionId == connection1.ConnectionId);
            Assert.Contains(result, c => c.ConnectionId == connection2.ConnectionId);
        }

        [Fact]
        public async Task EvaluateConnectionConditionAsync_Should_ReturnTrue_WhenNoConditionSpecified()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true,
                Condition = null
            };

            // Act
            var result = await _service.EvaluateConnectionConditionAsync(connection);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateConnectionConditionAsync_Should_EvaluateCondition_WhenConditionSpecified()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "TestType",
                IsEnabled = true,
                Condition = "source.Temperature > 20"
            };

            // Mock the condition evaluator to return true
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<double>(
                    It.Is<Guid>(id => id == sourceId),
                    It.Is<string>(name => name == "Temperature"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(25.0);

            // Act
            var result = await _service.EvaluateConnectionConditionAsync(connection);

            // Assert
            Assert.True(result);
            _mockPersistenceService.Verify(p => p.GetPropertyAsync<double>(
                It.Is<Guid>(id => id == sourceId),
                It.Is<string>(name => name == "Temperature"),
                It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ErrorHandling_Should_LogAndReturnFalse_WhenConditionEvaluationThrows()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true,
                Condition = "source.Temperature > 20"
            };

            // Mock the persistence service to throw
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<object>(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            var result = await _service.EvaluateConnectionConditionAsync(connection);

            // Assert
            Assert.False(result);
            _mockLogger.Verify(l => l.Log(
                It.IsAny<Exception>(),
                It.IsAny<string>()),
                Times.AtLeastOnce);
        }
    }
}