namespace HydroGarden.ErrorHandling.Core.Common
{
    /// <summary>
    /// Standardized error codes for the HydroGarden system.
    /// All error codes follow the format: [COMPONENT]_[CATEGORY]_[SPECIFIC]
    /// </summary>
    public static class ErrorCodes
    {
        // Device-related error codes
        public static class Device
        {
            public const string INITIALIZATION_FAILED = "DEVICE_INIT_FAILED";
            public const string COMMUNICATION_LOST = "DEVICE_COMM_LOST";
            public const string CALIBRATION_ERROR = "DEVICE_CALIB_ERROR";
            public const string HARDWARE_FAILURE = "DEVICE_HW_FAILURE";
            public const string SENSOR_MALFUNCTION = "DEVICE_SENSOR_ERROR";
            public const string CONFIGURATION_INVALID = "DEVICE_CONFIG_INVALID";
            public const string STATE_TRANSITION_FAILED = "DEVICE_STATE_ERROR";
            public const string RESOURCE_EXHAUSTED = "DEVICE_RESOURCE_EXHAUSTED";
        }

        // Service-related error codes
        public static class Service
        {
            public const string INITIALIZATION_FAILED = "SERVICE_INIT_FAILED";
            public const string OPERATION_TIMEOUT = "SERVICE_OP_TIMEOUT";
            public const string DEPENDENCY_UNAVAILABLE = "SERVICE_DEP_UNAVAILABLE";
            public const string RESOURCE_EXHAUSTED = "SERVICE_RESOURCE_EXHAUSTED";
            public const string CONCURRENT_ACCESS_CONFLICT = "SERVICE_CONCURRENCY_ERROR";
            public const string CONFIGURATION_INVALID = "SERVICE_CONFIG_INVALID";
        }

        // Communication-related error codes
        public static class Communication
        {
            public const string MESSAGE_DELIVERY_FAILED = "COMM_DELIVERY_FAILED";
            public const string CONNECTION_FAILED = "COMM_CONNECTION_FAILED";
            public const string PROTOCOL_ERROR = "COMM_PROTOCOL_ERROR";
            public const string TIMEOUT = "COMM_TIMEOUT";
            public const string SERIALIZATION_ERROR = "COMM_SERIALIZATION_ERROR";
        }

        // Event system error codes
        public static class Event
        {
            public const string PUBLICATION_FAILED = "EVENT_PUB_FAILED";
            public const string SUBSCRIPTION_ERROR = "EVENT_SUB_ERROR";
            public const string HANDLER_EXCEPTION = "EVENT_HANDLER_FAILED";
            public const string ROUTING_ERROR = "EVENT_ROUTING_ERROR";
            public const string PROCESSING_TIMEOUT = "EVENT_PROC_TIMEOUT";
        }

        // Storage-related error codes
        public static class Storage
        {
            public const string READ_FAILED = "STORAGE_READ_FAILED";
            public const string WRITE_FAILED = "STORAGE_WRITE_FAILED";
            public const string TRANSACTION_FAILED = "STORAGE_TRANS_FAILED";
            public const string DATA_CORRUPTION = "STORAGE_DATA_CORRUPT";
            public const string SERIALIZATION_ERROR = "STORAGE_SERIAL_ERROR";
        }

        // Recovery-related error codes
        public static class Recovery
        {
            public const string STRATEGY_FAILED = "RECOVERY_STRATEGY_FAILED";
            public const string ATTEMPT_LIMIT_REACHED = "RECOVERY_LIMIT_EXCEEDED";
            public const string CIRCUIT_OPEN = "RECOVERY_CIRCUIT_OPEN";
            public const string DEPENDENCY_UNRECOVERABLE = "RECOVERY_DEP_FAILED";
        }

        // Helper method to check if an error is unrecoverable based on its code
        public static bool IsUnrecoverable(string? errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
                return false;

            return errorCode switch
            {
                Device.HARDWARE_FAILURE => true,
                Device.CONFIGURATION_INVALID => true,
                Recovery.ATTEMPT_LIMIT_REACHED => true,
                Recovery.DEPENDENCY_UNRECOVERABLE => true,
                Storage.DATA_CORRUPTION => true,
                _ => false
            };
        }
    }
}