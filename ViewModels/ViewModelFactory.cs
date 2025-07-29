using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Factory for creating and managing ViewModel instances
    /// </summary>
    public interface IViewModelFactory
    {
        /// <summary>
        /// Create a ViewModel instance of the specified type
        /// </summary>
        T CreateViewModel<T>() where T : BaseViewModel;

        /// <summary>
        /// Create a ViewModel instance with initialization
        /// </summary>
        Task<T> CreateAndInitializeViewModelAsync<T>() where T : BaseViewModel;

        /// <summary>
        /// Dispose all managed ViewModels
        /// </summary>
        void DisposeAllViewModels();
    }

    /// <summary>
    /// Implementation of ViewModel factory with dependency injection support
    /// </summary>
    public class ViewModelFactory : IViewModelFactory, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ViewModelFactory> _logger;
        private readonly List<BaseViewModel> _managedViewModels = new();
        private bool _disposed;

        public ViewModelFactory(IServiceProvider serviceProvider, ILogger<ViewModelFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public T CreateViewModel<T>() where T : BaseViewModel
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<T>();
                _managedViewModels.Add(viewModel);
                
                _logger.LogDebug("Created ViewModel of type {ViewModelType}", typeof(T).Name);
                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create ViewModel of type {ViewModelType}", typeof(T).Name);
                throw;
            }
        }

        public async Task<T> CreateAndInitializeViewModelAsync<T>() where T : BaseViewModel
        {
            var viewModel = CreateViewModel<T>();
            
            try
            {
                await viewModel.InitializeAsync();
                _logger.LogDebug("Initialized ViewModel of type {ViewModelType}", typeof(T).Name);
                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ViewModel of type {ViewModelType}", typeof(T).Name);
                
                // Dispose the failed ViewModel
                viewModel.Dispose();
                _managedViewModels.Remove(viewModel);
                
                throw;
            }
        }

        public void DisposeAllViewModels()
        {
            _logger.LogInformation("Disposing all managed ViewModels");
            
            foreach (var viewModel in _managedViewModels)
            {
                try
                {
                    viewModel.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing ViewModel {ViewModelType}", viewModel.GetType().Name);
                }
            }
            
            _managedViewModels.Clear();
            _logger.LogInformation("All managed ViewModels disposed");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DisposeAllViewModels();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Extension methods for ViewModel factory registration
    /// </summary>
    public static class ViewModelFactoryExtensions
    {
        /// <summary>
        /// Register ViewModel factory in dependency injection container
        /// </summary>
        public static IServiceCollection AddViewModelFactory(this IServiceCollection services)
        {
            services.AddSingleton<IViewModelFactory, ViewModelFactory>();
            return services;
        }
    }
}
