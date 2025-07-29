using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services.Core
{
    /// <summary>
    /// Base implementation for system services with common functionality
    /// </summary>
    public abstract class BaseSystemService : ISystemService, IDisposable
    {
        protected readonly ILogger Logger;
        private bool _isAvailable;
        private bool _disposed;

        public bool IsAvailable
        {
            get => _isAvailable;
            protected set
            {
                if (_isAvailable != value)
                {
                    _isAvailable = value;
                    OnAvailabilityChanged(value);
                }
            }
        }

        public event EventHandler<ServiceAvailabilityChangedEventArgs>? AvailabilityChanged;

        protected BaseSystemService(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task InitializeAsync()
        {
            try
            {
                Logger.LogInformation("Initializing {ServiceType}", GetType().Name);
                await InitializeServiceAsync();
                IsAvailable = true;
                Logger.LogInformation("{ServiceType} initialized successfully", GetType().Name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize {ServiceType}", GetType().Name);
                IsAvailable = false;
                throw;
            }
        }

        public virtual async Task DisposeAsync()
        {
            if (_disposed) return;

            try
            {
                Logger.LogInformation("Disposing {ServiceType}", GetType().Name);
                await DisposeServiceAsync();
                IsAvailable = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error disposing {ServiceType}", GetType().Name);
            }
            finally
            {
                _disposed = true;
            }
        }

        protected abstract Task InitializeServiceAsync();
        protected abstract Task DisposeServiceAsync();

        protected virtual void OnAvailabilityChanged(bool isAvailable, string? reason = null)
        {
            AvailabilityChanged?.Invoke(this, new ServiceAvailabilityChangedEventArgs(isAvailable, reason));
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _ = Task.Run(async () => await DisposeAsync());
            }
        }
    }
}
