using System.Xml.Linq;

namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyChangedEventArgs
    {
        string Name { get; }
        object? OldValue { get; }
        object? NewValue { get; }
        DateTimeOffset Timestamp { get; }
    }
}
