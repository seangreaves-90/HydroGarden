using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.PropertyManager
{
    public class PropertyMetadata<T>
    {
        private T _value;
        private readonly IPropertyValidator<IValidationResult, T>? _validator;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public Type PropertyType => typeof(T);
        public bool IsReadOnly { get; }
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

        public async Task<T> GetValueAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                return _value;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> TrySetValueAsync(T value, CancellationToken ct = default)
        {
            if (IsReadOnly)
            {
                LastError = "Property is read-only";
                return false;
            }

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
