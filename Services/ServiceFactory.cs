using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using ThreadPilot.Services.Core;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Factory for creating and managing service instances with proper dependency resolution
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// Create a service instance of the specified type
        /// </summary>
        T CreateService<T>() where T : class;

        /// <summary>
        /// Create a service instance with additional parameters
        /// </summary>
        T CreateService<T>(params object[] parameters) where T : class;

        /// <summary>
        /// Get or create a singleton service instance
        /// </summary>
        T GetSingletonService<T>() where T : class;

        /// <summary>
        /// Initialize all core services
        /// </summary>
        Task InitializeAllServicesAsync();

        /// <summary>
        /// Dispose all managed services
        /// </summary>
        Task DisposeAllServicesAsync();
    }

    /// <summary>
    /// Implementation of service factory with dependency injection support
    /// </summary>
    public class ServiceFactory : IServiceFactory, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServiceFactory> _logger;
        private readonly Dictionary<Type, object> _singletonInstances = new();
        private readonly List<ISystemService> _managedServices = new();
        private bool _disposed;

        public ServiceFactory(IServiceProvider serviceProvider, ILogger<ServiceFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public T CreateService<T>() where T : class
        {
            try
            {
                var service = _serviceProvider.GetRequiredService<T>();
                
                // Track system services for lifecycle management
                if (service is ISystemService systemService)
                {
                    _managedServices.Add(systemService);
                }

                return service;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create service of type {ServiceType}", typeof(T).Name);
                throw;
            }
        }

        public T CreateService<T>(params object[] parameters) where T : class
        {
            try
            {
                // For services with additional parameters, use ActivatorUtilities
                var service = ActivatorUtilities.CreateInstance<T>(_serviceProvider, parameters);
                
                if (service is ISystemService systemService)
                {
                    _managedServices.Add(systemService);
                }

                return service;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create service of type {ServiceType} with parameters", typeof(T).Name);
                throw;
            }
        }

        public T GetSingletonService<T>() where T : class
        {
            var serviceType = typeof(T);
            
            if (_singletonInstances.TryGetValue(serviceType, out var existingInstance))
            {
                return (T)existingInstance;
            }

            var newInstance = CreateService<T>();
            _singletonInstances[serviceType] = newInstance;
            
            return newInstance;
        }

        public async Task InitializeAllServicesAsync()
        {
            _logger.LogInformation("Initializing all managed services");
            
            var initializationTasks = _managedServices.Select(async service =>
            {
                try
                {
                    await service.InitializeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize service {ServiceType}", service.GetType().Name);
                    throw;
                }
            });

            await Task.WhenAll(initializationTasks);
            _logger.LogInformation("All managed services initialized successfully");
        }

        public async Task DisposeAllServicesAsync()
        {
            _logger.LogInformation("Disposing all managed services");
            
            var disposalTasks = _managedServices.Select(async service =>
            {
                try
                {
                    await service.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing service {ServiceType}", service.GetType().Name);
                }
            });

            await Task.WhenAll(disposalTasks);
            _managedServices.Clear();
            _singletonInstances.Clear();
            
            _logger.LogInformation("All managed services disposed");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _ = Task.Run(async () => await DisposeAllServicesAsync());
                _disposed = true;
            }
        }
    }
}
