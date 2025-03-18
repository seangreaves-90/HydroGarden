using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
    /// <summary>
    /// Exception thrown when a storage read operation fails.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StorageReadException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="storageName">The name of the storage system.</param>
    /// <param name="resourcePath">The path or identifier of the resource.</param>
    /// <param name="innerException">The inner exception.</param>
    public class StorageReadException(
        string message,
        string storageName,
        string resourcePath,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Storage.READ_FAILED,
            ErrorSeverity.Error,
            ErrorSource.Database,
            innerException,
            null,
            new Dictionary<string, object>
                {
                    { "StorageName", storageName },
                    { "ResourcePath", resourcePath }
                },
            ErrorCategory.Storage)
    {
    }

    /// <summary>
    /// Exception thrown when a storage write operation fails.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StorageWriteException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="storageName">The name of the storage system.</param>
    /// <param name="resourcePath">The path or identifier of the resource.</param>
    /// <param name="innerException">The inner exception.</param>
    public class StorageWriteException(
        string message,
        string storageName,
        string resourcePath,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Storage.WRITE_FAILED,
            ErrorSeverity.Error,
            ErrorSource.Database,
            innerException,
            null,
            new Dictionary<string, object>
                {
                    { "StorageName", storageName },
                    { "ResourcePath", resourcePath }
                },
            ErrorCategory.Storage)
    {
    }

    /// <summary>
    /// Exception thrown when a storage transaction fails.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StorageTransactionException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="storageName">The name of the storage system.</param>
    /// <param name="transactionId">The ID of the transaction.</param>
    /// <param name="innerException">The inner exception.</param>
    public class StorageTransactionException(
        string message,
        string storageName,
        string transactionId,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Storage.TRANSACTION_FAILED,
            ErrorSeverity.Error,
            ErrorSource.Database,
            innerException,
            null,
            new Dictionary<string, object>
                {
                    { "StorageName", storageName },
                    { "TransactionId", transactionId }
                },
            ErrorCategory.Storage)
    {
    }

    /// <summary>
    /// Exception thrown when data corruption is detected.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DataCorruptionException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="dataType">The type of the corrupted data.</param>
    /// <param name="dataId">The ID of the corrupted data.</param>
    /// <param name="innerException">The inner exception.</param>
    public class DataCorruptionException(
        string message,
        string dataType,
        string dataId,
        Exception? innerException = null) : ApplicationException(
            message,
            ErrorCodes.Storage.DATA_CORRUPTION,
            ErrorSeverity.Critical,
            ErrorSource.Database,
            innerException,
            null,
            new Dictionary<string, object>
                {
                    { "DataType", dataType },
                    { "DataId", dataId }
                },
            ErrorCategory.Storage)
    {
    }

    /// <summary>
    /// Exception thrown when a data validation error occurs.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DataValidationException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="validationErrors">The list of validation errors.</param>
    /// <param name="dataType">The type of the data that failed validation.</param>
    /// <param name="innerException">The inner exception.</param>
    public class DataValidationException(
        string message,
        IEnumerable<string> validationErrors,
        string dataType,
        Exception? innerException = null) : ApplicationException(
            message,
            "STORAGE_VALIDATION_ERROR",
            ErrorSeverity.Error,
            ErrorSource.Service,
            innerException,
            null,
            new Dictionary<string, object>
                {
                    { "DataType", dataType },
                    { "ValidationErrors", string.Join(", ", validationErrors) }
                },
            ErrorCategory.Storage)
    {
    }
}