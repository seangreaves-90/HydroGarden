using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.EventArgs
{
    public class PropertyChangedEventArgs(string name, object? oldValue, object? newValue) : IPropertyChangedEventArgs
    {
        public string Name { get; } = name;
        public object? OldValue { get; } = oldValue;
        public object? NewValue { get; } = newValue;
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    }
}
