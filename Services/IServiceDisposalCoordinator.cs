using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for coordinating proper disposal of services
    /// </summary>
    public interface IServiceDisposalCoordinator : IDisposable
    {
        /// <summary>
        /// Register a service for coordinated disposal
        /// </summary>
        void RegisterService(string serviceName, IDisposable service, int priority = 0);

        /// <summary>
        /// Register an async disposable service
        /// </summary>
        void RegisterAsyncService(string serviceName, IAsyncDisposable service, int priority = 0);

        /// <summary>
        /// Register a custom disposal action
        /// </summary>
        void RegisterDisposalAction(string actionName, Func<Task> disposalAction, int priority = 0);

        /// <summary>
        /// Dispose all registered services in priority order
        /// </summary>
        Task DisposeAllAsync();

        /// <summary>
        /// Get disposal status
        /// </summary>
        bool IsDisposed { get; }
    }
}
