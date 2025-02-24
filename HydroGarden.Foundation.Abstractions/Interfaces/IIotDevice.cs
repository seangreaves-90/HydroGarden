using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public enum DeviceType
    {
        Sensor,
        Actuator,
        Controller,
        Pump,
        Valve,
        Relay
    }

    public enum DeviceState
    {
        Created,
        Initializing,
        Ready,
        Running,
        Stopping,
        Error,
        Disposed
    }
    public interface IIoTDevice : IDisposable
    {
        Guid Id { get; }
        string Name { get; }
        DeviceType Type { get; }
        DeviceState State { get; }

        Task InitializeAsync(CancellationToken ct = default);
        Task ExecuteCoreAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
    }
}