namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface ILogger
    {
        void LogError(Exception ex, string message);
        void LogWarning(Exception? exception, string message);
    }
}
