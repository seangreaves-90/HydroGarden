using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Validation
{
    public class CustomValidationResult : IValidationResult
    {
        public bool IsValid { get; }
        public string? Error { get; }
        public IDictionary<string, object> Context { get; }

        private CustomValidationResult(bool isValid, string? error, IDictionary<string, object>? context)
        {
            IsValid = isValid;
            Error = error;
            Context = context ?? new Dictionary<string, object>();
        }

        public static CustomValidationResult Success() =>
            new CustomValidationResult(true, null, null);

        public static CustomValidationResult Failure(string? error) =>
            new CustomValidationResult(false, error, null);
    }
}
