using System.Text.Json;
using System.Text.Json.Serialization;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Core.Serialization;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Core.Stores
{
    /// <summary>
    /// JSON-based implementation of a store for component data and topology
    /// </summary>
    public class JsonStore : IStore
    {
        // Directory structure constants
        private const string COMPONENTS_DIR = "components";
        private const string TOPOLOGY_DIR = "topology";
        private const string STATE_DIR = "state";
        private const string EVENTS_DIR = "events";
        private const string BACKUP_DIR = "backup";

        // File naming constants
        private const string CONNECTIONS_FILE = "connections.json";
        private const string METADATA_FILE = "metadata.json";
        private const string SYSTEM_STATE_FILE = "system.json";

        private readonly string _basePath;
        private readonly string _componentsPath;
        private readonly string _topologyPath;
        private readonly string _statePath;
        private readonly string _eventsPath;
        private readonly string _backupPath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _serializerOptions;
private readonly ILogger _logger;
private long _lastBackupTimestamp;
private readonly TimeSpan _backupInterval;

        /// <summary>
        /// Creates a new instance of the JsonStore
        /// </summary>
        /// <param name="basePath">The base directory path for storage</param>
        /// <param name="logger">Logger for operations</param>
        public JsonStore(string basePath, ILogger? logger, TimeSpan? backupInterval = null)
        {
            // Setup paths
            _basePath = Path.GetFullPath(basePath);
            _componentsPath = Path.Combine(_basePath, COMPONENTS_DIR);
            _topologyPath = Path.Combine(_basePath, TOPOLOGY_DIR);
            _statePath = Path.Combine(_basePath, STATE_DIR);
            _eventsPath = Path.Combine(_basePath, EVENTS_DIR);
            _backupPath = Path.Combine(_basePath, BACKUP_DIR);
            
            // Initialize backup tracking
            _lastBackupTimestamp = DateTime.UtcNow.Ticks;
            _backupInterval = backupInterval ?? TimeSpan.FromHours(24); // Default: daily backup

            // Create directory structure
            Directory.CreateDirectory(_componentsPath);
            Directory.CreateDirectory(_topologyPath);
            Directory.CreateDirectory(_statePath);
            Directory.CreateDirectory(_eventsPath);
            Directory.CreateDirectory(_backupPath);

            // Configure serialization
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new PropertyMetadataConverter(),
                    new NumericJsonConverter() // Add the numeric converter
                }
            };

            _logger = logger ?? new Logger.Logging.Logger();
            _logger.Log($"JsonStore initialized with base path: {_basePath}");
        }

        /// <inheritdoc />
        public async Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                return new JsonStoreTransaction(this, _lock);
            }
            catch (Exception)
            {
                _lock.Release();
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            // Check for cancellation before proceeding
            ct.ThrowIfCancellationRequested();
            
            string filePath = GetComponentFilePath(id);
            if (!File.Exists(filePath))
            {
                // Special handling for topology ID
                if (id == Guid.Parse("00000000-0000-0000-0000-000000000001"))
                {
                    // Attempt to load from topology directory
                    return await LoadTopologyAsync(ct);
                }
                _logger.Log($"No file found for component {id}");
                return null;
            }

            try
            {
                // Use a file stream for more efficient I/O
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var component = await JsonSerializer.DeserializeAsync<ComponentStore>(fileStream, _serializerOptions, ct);
                
                if (component?.Properties != null)
                {
                    // Ensure numeric values are properly typed, especially FlowRate
                    var normalizedProperties = new Dictionary<string, object>();
                    foreach (var kvp in component.Properties)
                    {
                        // Specifically ensure FlowRate is always a double
                        if (kvp.Key == "FlowRate")
                        {
                            // Convert integer to double if needed
                            if (kvp.Value is int intValue)
                            {
                                normalizedProperties[kvp.Key] = (double)intValue;
                            }
                            else if (kvp.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
                            {
                                // JsonElement needs special handling
                                if (jsonElement.TryGetDouble(out double doubleValue))
                                {
                                    normalizedProperties[kvp.Key] = doubleValue;
                                }
                                else
                                {
                                    normalizedProperties[kvp.Key] = kvp.Value;
                                }
                            }
                            else
                            {
                                normalizedProperties[kvp.Key] = kvp.Value;
                            }
                        }
                        else
                        {
                            normalizedProperties[kvp.Key] = kvp.Value;
                        }
                    }
                    return normalizedProperties;
                }
                
                return component?.Properties;
            }
            catch (JsonException? ex)
            {
                _logger.Log(ex, $"Error parsing JSON for component {id}");
                return null;
            }
            catch (IOException? ex)
            {
                _logger.Log(ex, $"Error reading file for component {id}");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default)
        {
            string filePath = GetComponentFilePath(id);
            if (!File.Exists(filePath))
                return null;

            try
            {
                string json = await File.ReadAllTextAsync(filePath, ct);
                var component = JsonSerializer.Deserialize<ComponentStore>(json, _serializerOptions);
                return component?.Metadata;
            }
            catch (JsonException? ex)
            {
                _logger.Log(ex, $"Error parsing JSON metadata for component {id}");
                return null;
            }
            catch (IOException? ex)
            {
                _logger.Log(ex, $"Error reading metadata file for component {id}");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default)
        {
            // Special handling for topology ID
            if (id == Guid.Parse("00000000-0000-0000-0000-000000000001"))
            {
                await SaveTopologyAsync(properties, ct);
                return;
            }

            await SaveWithMetadataAsync(id, properties, null, ct);
        }

        /// <inheritdoc />
        public async Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default)
        {
            // Check for cancellation before proceeding
            ct.ThrowIfCancellationRequested();
            
            string filePath = GetComponentFilePath(id);
            string tempFile = $"{filePath}.tmp";
            string backupFile = $"{filePath}.bak";
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _componentsPath);

            // Create a normalized copy of properties with consistent types
            var normalizedProperties = new Dictionary<string, object>();
            foreach (var kvp in properties)
            {
                // Ensure FlowRate is always a double
                if (kvp.Key == "FlowRate" && kvp.Value is int intValue)
                {
                    normalizedProperties[kvp.Key] = (double)intValue;
                }
                else
                {
                    normalizedProperties[kvp.Key] = kvp.Value;
                }
            }

            var component = new ComponentStore
            {
                Id = id,
                Properties = normalizedProperties,
                Metadata = metadata != null 
                    ? new Dictionary<string, IPropertyMetadata>(metadata) 
                    : new Dictionary<string, IPropertyMetadata>()
            };

            try
            {
                // Back up existing file if it exists
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupFile, true);
                }
                
                // Use FileStream for more efficient I/O and async operations
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await JsonSerializer.SerializeAsync(fileStream, component, _serializerOptions, ct);
                    await fileStream.FlushAsync(ct);
                }
                
                // Use atomic file move operation for data integrity
                File.Move(tempFile, filePath, true);
                
                // Remove backup file if everything succeeded
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }
                
                // Consider creating a backup if enough time has elapsed
                await CheckAndCreateBackupIfNeededAsync(ct);
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, $"Error saving component {id} to file");
                
                // Clean up temporary file if it exists
                if (File.Exists(tempFile)) 
                {
                    try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }
                }
                
                // Try to restore from backup if available
                if (File.Exists(backupFile) && !File.Exists(filePath))
                {
                    try 
                    { 
                        _logger.Log($"Attempting to restore {id} from backup");
                        File.Move(backupFile, filePath, false); 
                    } 
                    catch (Exception restoreEx) 
                    { 
                        _logger.Log(restoreEx, $"Failed to restore {id} from backup"); 
                    }
                }
                
                throw;
            }
        }

        /// <summary>
        /// Gets the file path for a component
        /// </summary>
        private string GetComponentFilePath(Guid id)
        {
            return Path.Combine(_componentsPath, $"{id}.json");
        }

        /// <summary>
        /// Gets the file path for topology connections
        /// </summary>
        private string GetConnectionsFilePath()
        {
            return Path.Combine(_topologyPath, CONNECTIONS_FILE);
        }

        /// <summary>
        /// Gets the file path for topology metadata
        /// </summary>
        private string GetTopologyMetadataFilePath()
        {
            return Path.Combine(_topologyPath, METADATA_FILE);
        }

        /// <summary>
        /// Gets the file path for system state
        /// </summary>
        private string GetSystemStateFilePath()
        {
            return Path.Combine(_statePath, SYSTEM_STATE_FILE);
        }

        /// <summary>
        /// Creates a backup of the current data
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A task representing the backup operation</returns>
        public async Task CreateBackupAsync(CancellationToken ct = default)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupDir = Path.Combine(_backupPath, timestamp);
            
            Directory.CreateDirectory(backupDir);
            var backupComponentsDir = Path.Combine(backupDir, COMPONENTS_DIR);
            var backupTopologyDir = Path.Combine(backupDir, TOPOLOGY_DIR);
            var backupStateDir = Path.Combine(backupDir, STATE_DIR);
            
            Directory.CreateDirectory(backupComponentsDir);
            Directory.CreateDirectory(backupTopologyDir);
            Directory.CreateDirectory(backupStateDir);

            // Copy components
            foreach (var file in Directory.GetFiles(_componentsPath))
            {
                var destFile = Path.Combine(backupComponentsDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy topology
            foreach (var file in Directory.GetFiles(_topologyPath))
            {
                var destFile = Path.Combine(backupTopologyDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy state
            foreach (var file in Directory.GetFiles(_statePath))
            {
                var destFile = Path.Combine(backupStateDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            _logger.Log($"Created backup at {backupDir}");
            
            // Update last backup timestamp
            _lastBackupTimestamp = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Checks if a backup is needed based on the elapsed time and creates one if necessary
        /// </summary>
        private async Task CheckAndCreateBackupIfNeededAsync(CancellationToken ct)
        {
            var currentTime = DateTime.UtcNow.Ticks;
            var elapsedTicks = currentTime - _lastBackupTimestamp;
            var elapsedTime = new TimeSpan(elapsedTicks);
            
            if (elapsedTime >= _backupInterval)
            {
                await CreateBackupAsync(ct);
            }
        }
        
        /// <summary>
        /// Loads topology data from the dedicated topology directory
        /// </summary>
        private async Task<IDictionary<string, object>?> LoadTopologyAsync(CancellationToken ct)
        {
            string connectionsFilePath = GetConnectionsFilePath();
            string metadataFilePath = GetTopologyMetadataFilePath();
            
            if (!File.Exists(connectionsFilePath))
            {
                _logger.Log("Topology connections file not found");
                return null;
            }

            try
            {
                var result = new Dictionary<string, object>();
                
                // Load connections using FileStream for efficiency
                using var fileStream = new FileStream(connectionsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var connectionsData = await JsonSerializer.DeserializeAsync<TopologyStore>(fileStream, _serializerOptions, ct);
                if (connectionsData != null)
                {
                    result["Connections"] = connectionsData.Connections;
                    result["Version"] = connectionsData.Version;
                    result["LastUpdated"] = connectionsData.LastUpdated;
                }

                // Load additional metadata if exists
                if (File.Exists(metadataFilePath))
                {
                    using var metadataStream = new FileStream(metadataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    var metadataObj = await JsonSerializer.DeserializeAsync<JsonElement>(metadataStream, _serializerOptions, ct);
                    // Add any additional metadata as needed
                }

                return result;
            }
            catch (JsonException? ex)
            {
                _logger.Log(ex, "Error parsing topology JSON");
                return null;
            }
            catch (IOException? ex)
            {
                _logger.Log(ex, "Error reading topology files");
                return null;
            }
        }

        /// <summary>
        /// Saves topology data to the dedicated topology directory
        /// </summary>
        private async Task SaveTopologyAsync(IDictionary<string, object> properties, CancellationToken ct)
        {
            string connectionsFilePath = GetConnectionsFilePath();
            string tempFile = $"{connectionsFilePath}.tmp";
            string backupFile = $"{connectionsFilePath}.bak";
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(connectionsFilePath) ?? _topologyPath);
            
            try
            {
                var topologyStore = new TopologyStore();
                
                // Extract topology properties
                if (properties.TryGetValue("Connections", out var connectionsObj) && 
                    connectionsObj is IEnumerable<IComponentConnection> connections)
                {
                    topologyStore.Connections = connections.ToList();
                }

                if (properties.TryGetValue("Version", out var versionObj) && 
                    versionObj is long version)
                {
                    topologyStore.Version = version;
                }
                
                if (properties.TryGetValue("LastUpdated", out var lastUpdatedObj) && 
                    lastUpdatedObj is DateTime lastUpdated)
                {
                    topologyStore.LastUpdated = lastUpdated;
                }
                else
                {
                    topologyStore.LastUpdated = DateTime.UtcNow;
                }

                // Back up existing file if it exists
                if (File.Exists(connectionsFilePath))
                {
                    File.Copy(connectionsFilePath, backupFile, true);
                }
                
                // Save connections file using FileStream for efficiency
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await JsonSerializer.SerializeAsync(fileStream, topologyStore, _serializerOptions, ct);
                    await fileStream.FlushAsync(ct);
                }
                
                // Use atomic file move operation
                File.Move(tempFile, connectionsFilePath, true);
                
                // Remove backup file if everything succeeded
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }

                // Save any additional metadata if needed
                // ...
                
                _logger.Log("Saved topology data");
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, "Error saving topology data");
                
                // Clean up temporary file if it exists
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }
                }
                
                // Try to restore from backup if available
                if (File.Exists(backupFile) && !File.Exists(connectionsFilePath))
                {
                    try 
                    { 
                        _logger.Log("Attempting to restore topology from backup");
                        File.Move(backupFile, connectionsFilePath, false); 
                    }
                    catch (Exception restoreEx) 
                    { 
                        _logger.Log(restoreEx, "Failed to restore topology from backup"); 
                    }
                }
                
                throw;
            }
        }

        /// <summary>
        /// Component data storage structure
        /// </summary>
        public class ComponentStore
        {
            public Guid Id { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new();
            public Dictionary<string, IPropertyMetadata> Metadata { get; set; } = new();
        }

        /// <summary>
        /// Topology data storage structure
        /// </summary>
        public class TopologyStore
        {
            public List<IComponentConnection> Connections { get; set; } = new();
            public long Version { get; set; } = 1;
            public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        }
    }
}