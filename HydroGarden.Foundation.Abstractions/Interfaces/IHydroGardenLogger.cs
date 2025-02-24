namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Base logger interface with two levels of logging: errors and warnings.
    /// </summary>
    public interface IHydroGardenLogger
    {
        void Log(string message);
        void Log(Exception ex, string message);
        void Log(object obj, string message);

    }
}
