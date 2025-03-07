﻿namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for command events
    /// </summary>
    public interface ICommandEvent : IEvent
    {
        /// <summary>
        /// The name of the command to execute
        /// </summary>
        string CommandName { get; }

        /// <summary>
        /// Optional parameters for the command
        /// </summary>
        IDictionary<string, object?>? Parameters { get; }
    }

    /// <summary>
    /// Specialized handler for command events
    /// </summary>
    public interface ICommandEventHandler : IEventHandler
    {
    }
}
