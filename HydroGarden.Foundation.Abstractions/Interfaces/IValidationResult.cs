namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IValidationResult
    {
        public bool IsValid { get; }
        public string? Error { get; }
        public IDictionary<string, object> Context { get; }
    }
}
