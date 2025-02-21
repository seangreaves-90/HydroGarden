namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyValidator<TValidationResult, in TValue> where TValidationResult : IValidationResult
    {
        Task<TValidationResult> ValidateAsync(TValue value, CancellationToken ct = default);
    }
}
