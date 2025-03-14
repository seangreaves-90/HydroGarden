using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
    /// <summary>
    /// Exception thrown when a device fails to initialize.
    /// </summary>
    public class DeviceInitializationException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceInitializationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="deviceId">The ID of the device that failed to initialize.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="context">Additional context information.</param>
        public DeviceInitializationException(
            string message,
            Guid deviceId,
            Exception? innerException = null,
            IDictionary<string, object>? context = null)
            : base(
                message,
                ErrorCodes.Device.INITIALIZATION_FAILED,
                ErrorSeverity.Error,
                ErrorSource.Device,
                innerException,
                deviceId,
                context,
                ErrorCategory.Device)
        {
        }
    }

    /// <summary>
    /// Exception thrown when communication with a device is lost.
    /// </summary>
    public class DeviceCommunicationException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceCommunicationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="deviceId">The ID of the device with communication issues.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="context">Additional context information.</param>
        public DeviceCommunicationException(
            string message,
            Guid deviceId,
            Exception? innerException = null,
            IDictionary<string, object>? context = null)
            : base(
                message,
                ErrorCodes.Device.COMMUNICATION_LOST,
                ErrorSeverity.Critical,
                ErrorSource.Communication,
                innerException,
                deviceId,
                context,
                ErrorCategory.Communication)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a device has a hardware failure.
    /// </summary>
    public class DeviceHardwareException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceHardwareException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="deviceId">The ID of the device with hardware issues.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="context">Additional context information.</param>
        public DeviceHardwareException(
            string message,
            Guid deviceId,
            Exception? innerException = null,
            IDictionary<string, object>? context = null)
            : base(
                message,
                ErrorCodes.Device.HARDWARE_FAILURE,
                ErrorSeverity.Critical,
                ErrorSource.Device,
                innerException,
                deviceId,
                context,
                ErrorCategory.Device)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a device sensor malfunctions.
    /// </summary>
    public class DeviceSensorException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceSensorException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="deviceId">The ID of the device with sensor issues.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="context">Additional context information.</param>
        public DeviceSensorException(
            string message,
            Guid deviceId,
            Exception? innerException = null,
            IDictionary<string, object>? context = null)
            : base(
                message,
                ErrorCodes.Device.SENSOR_MALFUNCTION,
                ErrorSeverity.Error,
                ErrorSource.Device,
                innerException,
                deviceId,
                context,
                ErrorCategory.Device)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a device has invalid configuration.
    /// </summary>
    public class DeviceConfigurationException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceConfigurationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="deviceId">The ID of the device with configuration issues.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="context">Additional context information.</param>
        public DeviceConfigurationException(
            string message,
            Guid deviceId,
            Exception? innerException = null,
            IDictionary<string, object>? context = null)
            : base(
                message,
                ErrorCodes.Device.CONFIGURATION_INVALID,
                ErrorSeverity.Error,
                ErrorSource.Device,
                innerException,
                deviceId,
                context,
                ErrorCategory.Device)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a device state transition fails.
    /// </summary>
    public class DeviceStateException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceStateException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="deviceId">The ID of the device with state transition issues.</param>
        /// <param name="fromState">The source state.</param>
        /// <param name="toState">The target state.</param>
        /// <param name="innerException">The inner exception.</param>
        public DeviceStateException(
            string message,
            Guid deviceId,
            string fromState,
            string toState,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Device.STATE_TRANSITION_FAILED,
                ErrorSeverity.Error,
                ErrorSource.Device,
                innerException,
                deviceId,
                new Dictionary<string, object>
                {
                    { "FromState", fromState },
                    { "ToState", toState }
                },
                ErrorCategory.Device)
        {
        }
    }
}