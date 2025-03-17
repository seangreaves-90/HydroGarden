using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
    /// <summary>
    /// Exception thrown when message delivery fails.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="MessageDeliveryException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="messageId">The ID of the message that failed to deliver.</param>
    /// <param name="destination">The destination of the message.</param>
    /// <param name="innerException">The inner exception.</param>
    public class MessageDeliveryException(
        string message,
        string messageId,
        string destination,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Communication.MESSAGE_DELIVERY_FAILED,
            ErrorSeverity.Error,
            ErrorSource.Communication,
            innerException,
            null,
            new Dictionary<string, object>
                {
                    { "MessageId", messageId },
                    { "Destination", destination }
                },
            ErrorCategory.Communication)
    {
    }

    /// <summary>
    /// Exception thrown when a connection fails.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConnectionFailedException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="endpoint">The endpoint that failed to connect.</param>
    /// <param name="deviceId">The ID of the device associated with the connection.</param>
    /// <param name="innerException">The inner exception.</param>
    public class ConnectionFailedException(
        string message,
        string endpoint,
        Guid? deviceId = null,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Communication.CONNECTION_FAILED,
            ErrorSeverity.Critical,
            ErrorSource.Communication,
            innerException,
            deviceId,
            new Dictionary<string, object>
                {
                    { "Endpoint", endpoint }
                },
            ErrorCategory.Communication)
    {
    }

    /// <summary>
    /// Exception thrown when a protocol error occurs.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProtocolException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="protocolName">The name of the protocol.</param>
    /// <param name="deviceId">The ID of the device associated with the protocol error.</param>
    /// <param name="innerException">The inner exception.</param>
    public class ProtocolException(
        string message,
        string protocolName,
        Guid? deviceId = null,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Communication.PROTOCOL_ERROR,
            ErrorSeverity.Error,
            ErrorSource.Communication,
            innerException,
            deviceId,
            new Dictionary<string, object>
                {
                    { "ProtocolName", protocolName }
                },
            ErrorCategory.Communication)
    {
    }

    /// <summary>
    /// Exception thrown when a communication timeout occurs.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CommunicationTimeoutException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="endpoint">The endpoint that timed out.</param>
    /// <param name="timeout">The timeout value in milliseconds.</param>
    /// <param name="deviceId">The ID of the device associated with the timeout.</param>
    /// <param name="innerException">The inner exception.</param>
    public class CommunicationTimeoutException(
        string message,
        string endpoint,
        int timeout,
        Guid? deviceId = null,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Communication.TIMEOUT,
            ErrorSeverity.Error,
            ErrorSource.Communication,
            innerException,
            deviceId,
            new Dictionary<string, object>
                {
                    { "Endpoint", endpoint },
                    { "TimeoutMs", timeout }
                },
            ErrorCategory.Communication)
    {
    }

    /// <summary>
    /// Exception thrown when serialization or deserialization fails.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SerializationException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="dataType">The type of the data that failed to serialize or deserialize.</param>
    /// <param name="innerException">The inner exception.</param>
    public class SerializationException(
        string message,
        string dataType,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Communication.SERIALIZATION_ERROR,
            ErrorSeverity.Error,
            ErrorSource.Communication,
            innerException,
            null,
            new Dictionary<string, object>
                {
                    { "DataType", dataType }
                },
            ErrorCategory.Communication)
    {
    }
}