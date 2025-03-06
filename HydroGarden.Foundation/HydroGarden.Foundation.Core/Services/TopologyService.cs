using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Implementation of the topology service for managing device connections
    /// </summary>
    public class TopologyService : ITopologyService
    {
        private readonly ILogger _logger;
        private readonly IStore _store;
        private readonly IPersistenceService _persistenceService;
        private readonly ConcurrentDictionary<Guid, ComponentConnection> _connections = new();
        private readonly ConcurrentDictionary<Guid, List<Guid>> _sourceToConnectionMap = new();
        private readonly ConcurrentDictionary<Guid, List<Guid>> _targetToConnectionMap = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ConditionEvaluator _conditionEvaluator;
        private bool _isInitialized = false;
        private bool _isDisposed = false;

        /// <summary>
        /// Creates a new topology service instance
        /// </summary>
        /// <param name="logger">Logger for recording events</param>
        /// <param name="store">Store for persisting connections</param>
        public TopologyService(ILogger logger, IStore store, IPersistenceService persistenceService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _conditionEvaluator = new ConditionEvaluator(_persistenceService);
        }

        /// <summary>
        /// Initializes the topology service by loading connections from storage
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized)
                return;

            await _lock.WaitAsync(ct);

            try
            {
                _logger.Log("Initializing topology service");

                // Use a fixed GUID for topology storage
                var topologyStoreId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                // Load connections from storage
                var connectionsData = await _store.LoadAsync(topologyStoreId, ct);
                if (connectionsData != null && connectionsData.TryGetValue("Connections", out var rawConnections) &&
                    rawConnections is List<ComponentConnection> connections)
                {
                    foreach (var connection in connections)
                    {
                        AddConnectionToCache(connection);
                    }

                    _logger.Log($"Loaded {connections.Count} connections from storage");
                }
                else
                {
                    _logger.Log("No connections found in storage, starting with empty topology");
                }

                _isInitialized = true;
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, "Error initializing topology service");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<IComponentConnection>> GetConnectionsForSourceAsync(Guid sourceId, CancellationToken ct = default)
        {
            if (!_isInitialized)
                await InitializeAsync(ct);

            if (!_sourceToConnectionMap.TryGetValue(sourceId, out var connectionIds))
            {
                return Array.Empty<IComponentConnection>();
            }

            var result = new List<IComponentConnection>();
            foreach (var connectionId in connectionIds)
            {
                if (_connections.TryGetValue(connectionId, out var connection) &&
                    connection.IsEnabled)
                {
                    result.Add(connection);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<IComponentConnection>> GetConnectionsForTargetAsync(Guid targetId, CancellationToken ct = default)
        {
            if (!_isInitialized)
                await InitializeAsync(ct);

            if (!_targetToConnectionMap.TryGetValue(targetId, out var connectionIds))
            {
                return Array.Empty<IComponentConnection>();
            }

            var result = new List<IComponentConnection>();
            foreach (var connectionId in connectionIds)
            {
                if (_connections.TryGetValue(connectionId, out var connection) &&
                    connection.IsEnabled)
                {
                    result.Add(connection);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<IComponentConnection> CreateConnectionAsync(IComponentConnection connection, CancellationToken ct = default)
        {
            if (!_isInitialized)
                await InitializeAsync(ct);

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (connection.SourceId == Guid.Empty)
                throw new ArgumentException("Source ID must be specified", nameof(connection));

            if (connection.TargetId == Guid.Empty)
                throw new ArgumentException("Target ID must be specified", nameof(connection));

            await _lock.WaitAsync(ct);

            try
            {
                // Convert to our internal type if necessary
                ComponentConnection internalConnection;
                if (connection is ComponentConnection connImpl)
                {
                    internalConnection = connImpl;
                }
                else
                {
                    internalConnection = new ComponentConnection
                    {
                        ConnectionId = connection.ConnectionId == Guid.Empty ? Guid.NewGuid() : connection.ConnectionId,
                        SourceId = connection.SourceId,
                        TargetId = connection.TargetId,
                        ConnectionType = connection.ConnectionType,
                        IsEnabled = connection.IsEnabled,
                        Condition = connection.Condition,
                        Metadata = connection.Metadata != null ? new Dictionary<string, object>(connection.Metadata) : null
                    };
                }

                // Generate a new ID if not provided
                if (internalConnection.ConnectionId == Guid.Empty)
                {
                    internalConnection.ConnectionId = Guid.NewGuid();
                }

                // Check if connection already exists
                if (_connections.ContainsKey(internalConnection.ConnectionId))
                {
                    throw new InvalidOperationException($"Connection with ID {internalConnection.ConnectionId} already exists");
                }

                // Add to cache
                AddConnectionToCache(internalConnection);

                // Save to storage
                await SaveConnectionsToStorageAsync(ct);

                _logger.Log($"Created connection {internalConnection.ConnectionId} from {internalConnection.SourceId} to {internalConnection.TargetId}");
                return internalConnection;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateConnectionAsync(IComponentConnection connection, CancellationToken ct = default)
        {
            if (!_isInitialized)
                await InitializeAsync(ct);

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (connection.ConnectionId == Guid.Empty)
                throw new ArgumentException("Connection ID must be specified", nameof(connection));

            await _lock.WaitAsync(ct);

            try
            {
                // Convert to our internal type if necessary
                ComponentConnection internalConnection;
                if (connection is ComponentConnection connImpl)
                {
                    internalConnection = connImpl;
                }
                else
                {
                    internalConnection = new ComponentConnection
                    {
                        ConnectionId = connection.ConnectionId,
                        SourceId = connection.SourceId,
                        TargetId = connection.TargetId,
                        ConnectionType = connection.ConnectionType,
                        IsEnabled = connection.IsEnabled,
                        Condition = connection.Condition,
                        Metadata = connection.Metadata != null ? new Dictionary<string, object>(connection.Metadata) : null
                    };
                }

                // Remove from cache
                if (!_connections.TryGetValue(connection.ConnectionId, out var existingConnection))
                {
                    return false;
                }

                RemoveConnectionFromCache(existingConnection);

                // Add updated connection to cache
                AddConnectionToCache(internalConnection);

                // Save to storage
                await SaveConnectionsToStorageAsync(ct);

                _logger.Log($"Updated connection {internalConnection.ConnectionId} from {internalConnection.SourceId} to {internalConnection.TargetId}");
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteConnectionAsync(Guid connectionId, CancellationToken ct = default)
        {
            if (!_isInitialized)
                await InitializeAsync(ct);

            if (connectionId == Guid.Empty)
                throw new ArgumentException("Connection ID must be specified", nameof(connectionId));

            await _lock.WaitAsync(ct);

            try
            {
                // Remove from cache
                if (!_connections.TryRemove(connectionId, out var connection))
                {
                    return false;
                }

                RemoveConnectionFromCache(connection);

                // Save to storage
                await SaveConnectionsToStorageAsync(ct);

                _logger.Log($"Deleted connection {connectionId} from {connection.SourceId} to {connection.TargetId}");
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> EvaluateConnectionConditionAsync(IComponentConnection connection, CancellationToken ct = default)
        {
            if (!_isInitialized)
                await InitializeAsync(ct);

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (string.IsNullOrWhiteSpace(connection.Condition))
                return true; // No condition means it passes

            try
            {
                return await _conditionEvaluator.EvaluateAsync(
                    connection.SourceId,
                    connection.TargetId,
                    connection.Condition,
                    ct);
            }
            catch (Exception? ex)
            {
                // Make sure to log the exception
                _logger.Log(ex, $"Error evaluating condition for connection {connection.ConnectionId}");
                return false; // Fail closed on errors
            }
        }

        private void AddConnectionToCache(ComponentConnection connection)
        {
            _connections[connection.ConnectionId] = connection;

            // Update source mapping
            if (!_sourceToConnectionMap.TryGetValue(connection.SourceId, out var sourceConnections))
            {
                sourceConnections = new List<Guid>();
                _sourceToConnectionMap[connection.SourceId] = sourceConnections;
            }

            if (!sourceConnections.Contains(connection.ConnectionId))
            {
                sourceConnections.Add(connection.ConnectionId);
            }

            // Update target mapping
            if (!_targetToConnectionMap.TryGetValue(connection.TargetId, out var targetConnections))
            {
                targetConnections = new List<Guid>();
                _targetToConnectionMap[connection.TargetId] = targetConnections;
            }

            if (!targetConnections.Contains(connection.ConnectionId))
            {
                targetConnections.Add(connection.ConnectionId);
            }
        }

        private void RemoveConnectionFromCache(ComponentConnection connection)
        {
            // Remove from source mapping
            if (_sourceToConnectionMap.TryGetValue(connection.SourceId, out var sourceConnections))
            {
                sourceConnections.Remove(connection.ConnectionId);
                if (sourceConnections.Count == 0)
                {
                    _sourceToConnectionMap.TryRemove(connection.SourceId, out _);
                }
            }

            // Remove from target mapping
            if (_targetToConnectionMap.TryGetValue(connection.TargetId, out var targetConnections))
            {
                targetConnections.Remove(connection.ConnectionId);
                if (targetConnections.Count == 0)
                {
                    _targetToConnectionMap.TryRemove(connection.TargetId, out _);
                }
            }

            // Remove from connections
            _connections.TryRemove(connection.ConnectionId, out _);
        }

        private async Task SaveConnectionsToStorageAsync(CancellationToken ct)
        {
            try
            {
                // Get all connections
                var connections = _connections.Values.ToList();

                // Save to storage using a fixed GUID for topology data
                var topologyStoreId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var properties = new Dictionary<string, object>
                {
                    ["Connections"] = connections
                };

                await _store.SaveAsync(topologyStoreId, properties, ct);

                _logger.Log($"Saved {connections.Count} connections to storage");
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, "Error saving connections to storage");
                throw;
            }
        }
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            await Task.Run(() => _lock.Dispose());
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}