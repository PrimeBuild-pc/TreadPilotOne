using System;

namespace ThreadPilot.Services.Core
{
    /// <summary>
    /// Base interface for core system services that interact directly with the operating system
    /// </summary>
    public interface ISystemService
    {
        /// <summary>
        /// Gets whether the service is currently available and functional
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Event fired when the service availability changes
        /// </summary>
        event EventHandler<ServiceAvailabilityChangedEventArgs>? AvailabilityChanged;

        /// <summary>
        /// Initialize the service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Cleanup and dispose of service resources
        /// </summary>
        Task DisposeAsync();
    }

    /// <summary>
    /// Event args for service availability changes
    /// </summary>
    public class ServiceAvailabilityChangedEventArgs : EventArgs
    {
        public bool IsAvailable { get; }
        public string? Reason { get; }

        public ServiceAvailabilityChangedEventArgs(bool isAvailable, string? reason = null)
        {
            IsAvailable = isAvailable;
            Reason = reason;
        }
    }
}
