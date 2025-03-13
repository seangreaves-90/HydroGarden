namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy
{
    /// <summary>
    /// Provides taxonomy enums for error categorization, enabling precise targeting of recovery strategies.
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
    }
}