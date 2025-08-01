using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Coordinates proper disposal of services in priority order
    /// </summary>
    public class ServiceDisposalCoordinator : IServiceDisposalCoordinator
    {
        private readonly ILogger<ServiceDisposalCoordinator> _logger;
        private readonly List<DisposalItem> _disposalItems = new();
        private readonly object _lockObject = new();
        private bool _disposed;

        public bool IsDisposed => _disposed;

        public ServiceDisposalCoordinator(ILogger<ServiceDisposalCoordinator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterService(string serviceName, IDisposable service, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
            
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            lock (_lockObject)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ServiceDisposalCoordinator));

                _disposalItems.Add(new DisposalItem
                {
                    Name = serviceName,
                    Priority = priority,
                    DisposalAction = () =>
                    {
                        service.Dispose();
                        return Task.CompletedTask;
                    }
                });

                _logger.LogDebug("Registered service for disposal: {ServiceName} (Priority: {Priority})", 
                    serviceName, priority);
            }
        }

        public void RegisterAsyncService(string serviceName, IAsyncDisposable service, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
            
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            lock (_lockObject)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ServiceDisposalCoordinator));

                _disposalItems.Add(new DisposalItem
                {
                    Name = serviceName,
                    Priority = priority,
                    DisposalAction = async () => await service.DisposeAsync()
                });

                _logger.LogDebug("Registered async service for disposal: {ServiceName} (Priority: {Priority})", 
                    serviceName, priority);
            }
        }

        public void RegisterDisposalAction(string actionName, Func<Task> disposalAction, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                throw new ArgumentException("Action name cannot be null or empty", nameof(actionName));
            
            if (disposalAction == null)
                throw new ArgumentNullException(nameof(disposalAction));

            lock (_lockObject)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ServiceDisposalCoordinator));

                _disposalItems.Add(new DisposalItem
                {
                    Name = actionName,
                    Priority = priority,
                    DisposalAction = disposalAction
                });

                _logger.LogDebug("Registered disposal action: {ActionName} (Priority: {Priority})", 
                    actionName, priority);
            }
        }

        public async Task DisposeAllAsync()
        {
            if (_disposed)
                return;

            List<DisposalItem> itemsToDispose;
            lock (_lockObject)
            {
                if (_disposed)
                    return;

                // Sort by priority (higher priority disposed first)
                itemsToDispose = _disposalItems.OrderByDescending(x => x.Priority).ToList();
                _disposed = true;
            }

            _logger.LogInformation("Starting coordinated disposal of {Count} services/actions", itemsToDispose.Count);

            foreach (var item in itemsToDispose)
            {
                try
                {
                    _logger.LogDebug("Disposing: {Name} (Priority: {Priority})", item.Name, item.Priority);
                    await item.DisposalAction();
                    _logger.LogDebug("Successfully disposed: {Name}", item.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing {Name}: {Error}", item.Name, ex.Message);
                    // Continue with other disposals even if one fails
                }
            }

            _logger.LogInformation("Coordinated disposal completed");
        }

        public void Dispose()
        {
            DisposeAllAsync().GetAwaiter().GetResult();
        }

        private class DisposalItem
        {
            public string Name { get; set; } = string.Empty;
            public int Priority { get; set; }
            public Func<Task> DisposalAction { get; set; } = () => Task.CompletedTask;
        }
    }
}
