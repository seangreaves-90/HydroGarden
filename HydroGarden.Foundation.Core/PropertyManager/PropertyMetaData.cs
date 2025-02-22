using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Core.PropertyManager
{
    public class PropertyMetadata<T> : IPropertyMetadata
    {
        private T _value;
        private readonly IPropertyValidator<IValidationResult, T>? _validator;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public Type PropertyType => typeof(T);
        public bool IsReadOnly { get; } // This flag is now only for UI/informational purposes.
        public bool IsVisible { get; }
        public DateTimeOffset LastModified { get; private set; }
        public string? LastError { get; private set; }

        public PropertyMetadata(
            T initialValue,
            bool isReadOnly = false,
            bool isVisible = true,
            IPropertyValidator<IValidationResult, T>? validator = null)
        {
            _value = initialValue;
            IsReadOnly = isReadOnly;
            IsVisible = isVisible;
            _validator = validator;
            LastModified = DateTimeOffset.UtcNow;
        }

        public async Task<TValue> GetValueAsync<TValue>(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_value is TValue result)
                {
                    return result;
                }
                else
                {
                    // Optionally, convert _value to TValue
                    throw new InvalidCastException($"Cannot convert value of type {typeof(T).Name} to {typeof(TValue).Name}.");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<object> GetValueAsync(CancellationToken ct = default)
        {
            return await GetValueAsync<T>(ct);
        }

        public async Task<bool> TrySetValueAsync(T value, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_validator != null)
                {
                    var validationResult = await _validator.ValidateAsync(value, ct);
                    if (!validationResult.IsValid)
                    {
                        LastError = validationResult.Error;
                        return false;
                    }
                }

                _value = value;
                LastModified = DateTimeOffset.UtcNow;
                LastError = null;
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
