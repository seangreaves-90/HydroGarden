namespace HydroGarden.ErrorHandling.Core.Common
{
    /// <summary>
    /// Provides a detailed taxonomy for error categorization, enabling precise targeting of recovery strategies.
    /// This taxonomy goes beyond basic error codes to provide a structured classification system.
    /// </summary>
    public static class ErrorTaxonomy
    {
        /// <summary>
        /// Root causes of errors for diagnostic purposes.
        /// </summary>
        public enum RootCause
        {
            Unknown = 0,
            
            // Hardware-related causes
            HardwareFailure = 10,
            PowerIssue = 11,
            CalibrationProblem = 12,
            SensorMalfunction = 13,
            MemoryExhaustion = 14,
            
            // Software-related causes
            SoftwareBug = 20,
            ConfigurationError = 21,
            InvalidState = 22,
            ValidationFailure = 23,
            ResourceExhaustion = 24,
            
            // Communication-related causes
            NetworkFailure = 30,
            ConnectionTimeout = 31,
            ProtocolError = 32,
            SerializationError = 33,
            
            // External causes
            ExternalSystemFailure = 40,
            DependencyUnavailable = 41,
            ServiceUnavailable = 42,
            
            // Environmental causes
            EnvironmentalCondition = 50,
            TemperatureExcursion = 51,
            PowerFluctuation = 52,
            
            // User-related causes
            UserError = 60,
            InvalidInput = 61,
            PermissionDenied = 62,
            
            // Recovery-related causes
            RecoveryFailure = 70,
            RetryExhaustion = 71,
            CircuitBreakerOpen = 72
        }
        
        /// <summary>
        /// Impacts of errors on system operation.
        /// </summary>
        public enum SystemImpact
        {
            None = 0,
            Minimal = 1,          // Functionality not affected
            Partial = 2,          // Some functionality affected
            Significant = 3,      // Major functionality affected
            Complete = 4,         // System non-operational
            Cascading = 5         // Affects other systems
        }
        
        /// <summary>
        /// Recovery complexity levels.
        /// </summary>
        public enum RecoveryComplexity
        {
            Simple = 0,           // Automatic recovery likely to succeed
            Moderate = 1,         // Requires specific strategy but likely to succeed
            Complex = 2,          // Requires multiple strategies or steps
            VeryComplex = 3,      // Sophisticated recovery plan needed
            Manual = 4            // Manual intervention required
        }
        
        /// <summary>
        /// Data integrity statuses.
        /// </summary>
        public enum DataIntegrityStatus
        {
            Unknown = 0,
            Intact = 1,           // No data affected
            RecoverableCorruption = 2,  // Data corrupted but recoverable
            PartialLoss = 3,      // Some data lost but system can continue
            MajorLoss = 4,        // Significant data loss affecting operation
            CompleteLoss = 5      // Complete data loss
        }
        
        /// <summary>
        /// Time sensitivity levels for recovery.
        /// </summary>
        public enum TimeSensitivity
        {
            Low = 0,              // Recovery can be delayed
            Medium = 1,           // Should be addressed soon
            High = 2,             // Requires prompt attention
            Critical = 3,         // Requires immediate attention
            Realtime = 4          // Must be addressed in real-time
        }
        
        /// <summary>
        /// Operation types affected by the error.
        /// </summary>
        public enum AffectedOperation
        {
            Unknown = 0,
            Sensing = 1,          // Data acquisition
            Actuation = 2,        // Physical system control
            Monitoring = 3,       // System monitoring
            DataProcessing = 4,   // Data analysis
            UserInterface = 5,    // User interaction
            Communication = 6,    // System communication
            Storage = 7,          // Data persistence
            Authentication = 8,   // User authentication
            Authorization = 9,    // Access control
            Scheduling = 10,      // Scheduled operations
            Alerting = 11         // Notification system
        }
        
        /// <summary>
        /// Analyzes an error code to determine its root cause.
        /// </summary>
        /// <param name="errorCode">The error code to analyze.</param>
        /// <returns>The determined root cause.</returns>
        public static RootCause AnalyzeRootCause(string? errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
                return RootCause.Unknown;
                
            return errorCode switch
            {
                ErrorCodes.Device.HARDWARE_FAILURE => RootCause.HardwareFailure,
                ErrorCodes.Device.SENSOR_MALFUNCTION => RootCause.SensorMalfunction,
                ErrorCodes.Device.CALIBRATION_ERROR => RootCause.CalibrationProblem,
                ErrorCodes.Device.CONFIGURATION_INVALID => RootCause.ConfigurationError,
                ErrorCodes.Device.STATE_TRANSITION_FAILED => RootCause.InvalidState,
                ErrorCodes.Device.RESOURCE_EXHAUSTED => RootCause.ResourceExhaustion,
                
                ErrorCodes.Service.DEPENDENCY_UNAVAILABLE => RootCause.DependencyUnavailable,
                ErrorCodes.Service.OPERATION_TIMEOUT => RootCause.ConnectionTimeout,
                ErrorCodes.Service.CONFIGURATION_INVALID => RootCause.ConfigurationError,
                ErrorCodes.Service.RESOURCE_EXHAUSTED => RootCause.ResourceExhaustion,
                ErrorCodes.Service.CONCURRENT_ACCESS_CONFLICT => RootCause.InvalidState,
                
                ErrorCodes.Communication.CONNECTION_FAILED => RootCause.NetworkFailure,
                ErrorCodes.Communication.TIMEOUT => RootCause.ConnectionTimeout,
                ErrorCodes.Communication.PROTOCOL_ERROR => RootCause.ProtocolError,
                ErrorCodes.Communication.SERIALIZATION_ERROR => RootCause.SerializationError,
                
                ErrorCodes.Event.ROUTING_ERROR => RootCause.SoftwareBug,
                ErrorCodes.Event.PROCESSING_TIMEOUT => RootCause.ConnectionTimeout,
                ErrorCodes.Event.HANDLER_EXCEPTION => RootCause.SoftwareBug,
                
                ErrorCodes.Storage.DATA_CORRUPTION => RootCause.HardwareFailure,
                ErrorCodes.Storage.SERIALIZATION_ERROR => RootCause.SerializationError,
                
                ErrorCodes.Recovery.STRATEGY_FAILED => RootCause.RecoveryFailure,
                ErrorCodes.Recovery.ATTEMPT_LIMIT_REACHED => RootCause.RetryExhaustion,
                ErrorCodes.Recovery.CIRCUIT_OPEN => RootCause.CircuitBreakerOpen,
                
                _ => RootCause.Unknown
            };
        }
        
        /// <summary>
        /// Determines the system impact of an error based on its code and severity.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="severity">The error severity.</param>
        /// <returns>The system impact assessment.</returns>
        public static SystemImpact DetermineSystemImpact(string? errorCode, ErrorSeverity severity)
        {
            // First consider severity
            if (severity == ErrorSeverity.Catastrophic)
                return SystemImpact.Complete;
                
            if (severity == ErrorSeverity.Critical)
                return SystemImpact.Significant;
                
            // Then look at specific error codes
            if (string.IsNullOrEmpty(errorCode))
                return severity == ErrorSeverity.Warning ? SystemImpact.Minimal : SystemImpact.Partial;
                
            return errorCode switch
            {
                ErrorCodes.Device.HARDWARE_FAILURE => SystemImpact.Complete,
                ErrorCodes.Communication.CONNECTION_FAILED => SystemImpact.Significant,
                ErrorCodes.Storage.DATA_CORRUPTION => SystemImpact.Significant,
                ErrorCodes.Device.SENSOR_MALFUNCTION => SystemImpact.Partial,
                _ => severity == ErrorSeverity.Error ? SystemImpact.Partial : SystemImpact.Minimal
            };
        }
        
        /// <summary>
        /// Assesses the recovery complexity for an error.
        /// </summary>
        /// <param name="error">The application error.</param>
        /// <returns>The recovery complexity assessment.</returns>
        public static RecoveryComplexity AssessRecoveryComplexity(IApplicationError error)
        {
            // Non-recoverable errors require manual intervention
            if (error is ComponentError componentError && componentError.IsUnrecoverable)
                return RecoveryComplexity.Manual;
                
            // Consider the root cause
            var rootCause = AnalyzeRootCause(error.ErrorCode);
            
            return rootCause switch
            {
                RootCause.HardwareFailure => RecoveryComplexity.Manual,
                RootCause.ConfigurationError => RecoveryComplexity.Complex,
                RootCause.SensorMalfunction => RecoveryComplexity.Moderate,
                RootCause.NetworkFailure => RecoveryComplexity.Moderate,
                RootCause.ConnectionTimeout => RecoveryComplexity.Simple,
                RootCause.InvalidState => RecoveryComplexity.Moderate,
                RootCause.ResourceExhaustion => RecoveryComplexity.Moderate,
                RootCause.RetryExhaustion => RecoveryComplexity.Complex,
                _ => RecoveryComplexity.Moderate
            };
        }
        
        /// <summary>
        /// Assesses the time sensitivity for recovering from an error.
        /// </summary>
        /// <param name="error">The application error.</param>
        /// <returns>The time sensitivity assessment.</returns>
        public static TimeSensitivity AssessTimeSensitivity(IApplicationError error)
        {
            // Consider severity first
            if (error.Severity == ErrorSeverity.Catastrophic)
                return TimeSensitivity.Realtime;
                
            if (error.Severity == ErrorSeverity.Critical)
                return TimeSensitivity.Critical;
                
            // Look at affected operation
            var operation = DetermineAffectedOperation(error.ErrorCode);
            
            return operation switch
            {
                AffectedOperation.Sensing => TimeSensitivity.High,
                AffectedOperation.Actuation => TimeSensitivity.Critical,
                AffectedOperation.Monitoring => TimeSensitivity.Medium,
                AffectedOperation.Communication => TimeSensitivity.High,
                AffectedOperation.Alerting => TimeSensitivity.High,
                _ => error.Severity == ErrorSeverity.Error ? TimeSensitivity.Medium : TimeSensitivity.Low
            };
        }
        
        /// <summary>
        /// Determines the data integrity status based on the error.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <returns>The data integrity status assessment.</returns>
        public static DataIntegrityStatus AssessDataIntegrity(string? errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
                return DataIntegrityStatus.Unknown;
                
            return errorCode switch
            {
                ErrorCodes.Storage.DATA_CORRUPTION => DataIntegrityStatus.RecoverableCorruption,
                ErrorCodes.Storage.TRANSACTION_FAILED => DataIntegrityStatus.PartialLoss,
                ErrorCodes.Storage.SERIALIZATION_ERROR => DataIntegrityStatus.RecoverableCorruption,
                _ => DataIntegrityStatus.Intact
            };
        }
        
        /// <summary>
        /// Determines the operation affected by an error.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <returns>The affected operation.</returns>
        public static AffectedOperation DetermineAffectedOperation(string? errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
                return AffectedOperation.Unknown;
                
            if (errorCode.StartsWith("DEVICE_SENSOR"))
                return AffectedOperation.Sensing;
                
            return errorCode switch
            {
                var code when code.StartsWith("COMM_") => AffectedOperation.Communication,
                var code when code.StartsWith("STORAGE_") => AffectedOperation.Storage,
                var code when code.StartsWith("EVENT_") => AffectedOperation.DataProcessing,
                ErrorCodes.Device.SENSOR_MALFUNCTION => AffectedOperation.Sensing,
                ErrorCodes.Device.HARDWARE_FAILURE => AffectedOperation.Actuation,
                _ => AffectedOperation.Unknown
            };
        }
        
        /// <summary>
        /// Creates a detailed error profile for sophisticated recovery planning.
        /// </summary>
        /// <param name="error">The application error to profile.</param>
        /// <returns>A dictionary containing the error profile.</returns>
        public static IDictionary<string, object> CreateErrorProfile(IApplicationError error)
        {
            var profile = new Dictionary<string, object>
            {
                ["RootCause"] = AnalyzeRootCause(error.ErrorCode),
                ["SystemImpact"] = DetermineSystemImpact(error.ErrorCode, error.Severity),
                ["RecoveryComplexity"] = AssessRecoveryComplexity(error),
                ["DataIntegrityStatus"] = AssessDataIntegrity(error.ErrorCode),
                ["TimeSensitivity"] = AssessTimeSensitivity(error),
                ["AffectedOperation"] = DetermineAffectedOperation(error.ErrorCode),
                ["ErrorCategory"] = error is ComponentError compError ? compError.Category : ErrorCategory.Unknown
            };
            
            return profile;
        }
    }
}
