using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Base ViewModel with common functionality for all ViewModels
    /// </summary>
    public abstract partial class BaseViewModel : ObservableObject, IDisposable
    {
        protected readonly ILogger Logger;
        protected readonly IEnhancedLoggingService? EnhancedLoggingService;
        private bool _disposed;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool hasError;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        protected BaseViewModel(ILogger logger, IEnhancedLoggingService? enhancedLoggingService = null)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            EnhancedLoggingService = enhancedLoggingService;
        }

        /// <summary>
        /// Set status message and busy state
        /// </summary>
        protected void SetStatus(string message, bool isBusyState = true)
        {
            StatusMessage = message;
            IsBusy = isBusyState;
            ClearError();
        }

        /// <summary>
        /// Clear status and busy state
        /// </summary>
        protected void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsBusy = false;
        }

        /// <summary>
        /// Set error message and clear busy state
        /// </summary>
        protected void SetError(string message, Exception? exception = null)
        {
            ErrorMessage = message;
            HasError = true;
            IsBusy = false;

            if (exception != null)
            {
                Logger.LogError(exception, "Error in {ViewModelType}: {Message}", GetType().Name, message);
            }
            else
            {
                Logger.LogWarning("Error in {ViewModelType}: {Message}", GetType().Name, message);
            }
        }

        /// <summary>
        /// Clear error state
        /// </summary>
        protected void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }

        /// <summary>
        /// Execute an async operation with error handling and status updates
        /// </summary>
        protected async Task ExecuteAsync(Func<Task> operation, string? statusMessage = null, string? successMessage = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(statusMessage))
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetStatus(statusMessage);
                    });
                }

                await operation();

                if (!string.IsNullOrEmpty(successMessage))
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetStatus(successMessage, false);
                    });
                }
                else
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ClearStatus();
                    });
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetError($"Operation failed: {ex.Message}", ex);
                });
            }
        }

        /// <summary>
        /// Execute an async operation with return value and error handling
        /// </summary>
        protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, string? statusMessage = null, string? successMessage = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(statusMessage))
                {
                    // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetStatus(statusMessage);
                    });
                }

                var result = await operation();

                if (!string.IsNullOrEmpty(successMessage))
                {
                    // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetStatus(successMessage, false);
                    });
                }
                else
                {
                    // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ClearStatus();
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetError($"Operation failed: {ex.Message}", ex);
                });
                return default;
            }
        }

        /// <summary>
        /// Log user action for audit purposes
        /// </summary>
        protected async Task LogUserActionAsync(string action, string details, string? context = null)
        {
            try
            {
                if (EnhancedLoggingService != null)
                {
                    await EnhancedLoggingService.LogUserActionAsync(action, details, context);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to log user action: {Action}", action);
            }
        }

        /// <summary>
        /// Initialize the ViewModel - override in derived classes
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            // Base implementation does nothing
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cleanup resources - override in derived classes
        /// </summary>
        protected virtual void OnDispose()
        {
            // Base implementation does nothing
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                OnDispose();
                _disposed = true;
            }
        }
    }
}