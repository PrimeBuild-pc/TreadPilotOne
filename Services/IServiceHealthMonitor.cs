using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service health status enumeration
    /// </summary>
    public enum ServiceHealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        Critical
    }

    /// <summary>
    /// Service health check result
    /// </summary>
    public class ServiceHealthResult
    {
        public string ServiceName { get; set; } = string.Empty;
        public ServiceHealthStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public DateTime CheckTime { get; set; }
        public Exception? Exception { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Interface for monitoring service health and lifecycle
    /// </summary>
    public interface IServiceHealthMonitor
    {
        /// <summary>
        /// Register a service for health monitoring
        /// </summary>
        void RegisterService(string serviceName, Func<Task<ServiceHealthResult>> healthCheck);

        /// <summary>
        /// Unregister a service from health monitoring
        /// </summary>
        void UnregisterService(string serviceName);

        /// <summary>
        /// Perform health check on a specific service
        /// </summary>
        Task<ServiceHealthResult> CheckServiceHealthAsync(string serviceName);

        /// <summary>
        /// Perform health check on all registered services
        /// </summary>
        Task<Dictionary<string, ServiceHealthResult>> CheckAllServicesHealthAsync();

        /// <summary>
        /// Get the current health status of all services
        /// </summary>
        Dictionary<string, ServiceHealthResult> GetCurrentHealthStatus();

        /// <summary>
        /// Event raised when a service health status changes
        /// </summary>
        event EventHandler<ServiceHealthChangedEventArgs>? ServiceHealthChanged;
    }

    /// <summary>
    /// Event arguments for service health changes
    /// </summary>
    public class ServiceHealthChangedEventArgs : EventArgs
    {
        public string ServiceName { get; set; } = string.Empty;
        public ServiceHealthStatus PreviousStatus { get; set; }
        public ServiceHealthStatus CurrentStatus { get; set; }
        public ServiceHealthResult HealthResult { get; set; } = new();
    }
}
