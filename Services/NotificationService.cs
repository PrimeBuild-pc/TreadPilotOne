using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing notifications with balloon tips and toast support
    /// </summary>
    public class NotificationService : INotificationService, IDisposable
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IApplicationSettingsService _settingsService;
        private readonly ISystemTrayService _systemTrayService;
        private readonly List<NotificationModel> _notificationHistory;
        private ApplicationSettingsModel _settings;
        private bool _disposed = false;

        public event EventHandler<NotificationEventArgs>? NotificationShown;
        public event EventHandler<NotificationEventArgs>? NotificationDismissed;
        public event EventHandler<NotificationActionEventArgs>? NotificationActionClicked;

        public IReadOnlyList<NotificationModel> NotificationHistory => _notificationHistory.AsReadOnly();

        public NotificationService(
            ILogger<NotificationService> logger,
            IApplicationSettingsService settingsService,
            ISystemTrayService systemTrayService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));

            _notificationHistory = new List<NotificationModel>();
            _settings = _settingsService.Settings;

            // Subscribe to settings changes
            _settingsService.SettingsChanged += OnSettingsChanged;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing notification service");
                
                // Load settings
                _settings = _settingsService.Settings;
                
                _logger.LogInformation("Notification service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize notification service");
                throw;
            }
        }

        public async Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Information)
        {
            var notification = new NotificationModel(title, message, type)
            {
                DurationMs = _settings.NotificationDisplayDurationMs,
                Category = "General",
                SourceService = "NotificationService"
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowNotificationAsync(NotificationModel notification)
        {
            if (notification == null) return;

            try
            {
                // Check if notifications are enabled
                if (!AreNotificationsEnabled(notification.Type))
                {
                    _logger.LogDebug("Notifications disabled for type {Type}", notification.Type);
                    return;
                }

                // Add to history
                AddToHistory(notification);

                // Show balloon tip if enabled
                if (_settings.EnableBalloonNotifications)
                {
                    await ShowBalloonTipInternalAsync(notification);
                }

                // Show toast notification if enabled and available
                if (_settings.EnableToastNotifications)
                {
                    await ShowToastNotificationInternalAsync(notification);
                }

                // Fire event
                NotificationShown?.Invoke(this, new NotificationEventArgs(notification));

                _logger.LogDebug("Notification shown: {Title}", notification.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing notification: {Title}", notification.Title);
            }
        }

        public async Task ShowBalloonTipAsync(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000)
        {
            var notification = new NotificationModel(title, message, type)
            {
                DurationMs = timeoutMs,
                Category = "BalloonTip",
                SourceService = "NotificationService"
            };

            if (_settings.EnableBalloonNotifications && AreNotificationsEnabled(type))
            {
                AddToHistory(notification);
                await ShowBalloonTipInternalAsync(notification);
                NotificationShown?.Invoke(this, new NotificationEventArgs(notification));
            }
        }

        public async Task ShowToastNotificationAsync(string title, string message, NotificationType type = NotificationType.Information)
        {
            var notification = new NotificationModel(title, message, type)
            {
                Category = "Toast",
                SourceService = "NotificationService"
            };

            if (_settings.EnableToastNotifications && AreNotificationsEnabled(type))
            {
                AddToHistory(notification);
                await ShowToastNotificationInternalAsync(notification);
                NotificationShown?.Invoke(this, new NotificationEventArgs(notification));
            }
        }

        public async Task ShowPowerPlanChangeNotificationAsync(string oldPlan, string newPlan, string processName = "")
        {
            if (!_settings.EnablePowerPlanChangeNotifications) return;

            var message = string.IsNullOrEmpty(processName) 
                ? $"Power plan changed from '{oldPlan}' to '{newPlan}'"
                : $"Power plan changed to '{newPlan}' for process '{processName}'";

            var notification = new NotificationModel("Power Plan Changed", message, NotificationType.PowerPlanChange)
            {
                Category = "PowerPlan",
                SourceService = "PowerPlanService",
                Priority = NotificationPriority.Normal
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowProcessMonitoringNotificationAsync(string message, bool isEnabled)
        {
            if (!_settings.EnableProcessMonitoringNotifications) return;

            var title = isEnabled ? "Process Monitoring Enabled" : "Process Monitoring Disabled";
            var type = isEnabled ? NotificationType.Success : NotificationType.Warning;

            var notification = new NotificationModel(title, message, type)
            {
                Category = "ProcessMonitoring",
                SourceService = "ProcessMonitorService",
                Priority = NotificationPriority.Normal
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowCpuAffinityNotificationAsync(string processName, string affinityInfo)
        {
            var notification = new NotificationModel(
                "CPU Affinity Applied", 
                $"CPU affinity set for '{processName}': {affinityInfo}",
                NotificationType.CpuAffinity)
            {
                Category = "CpuAffinity",
                SourceService = "ProcessService",
                Priority = NotificationPriority.Normal
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowErrorNotificationAsync(string title, string message, Exception? exception = null)
        {
            if (!_settings.EnableErrorNotifications) return;

            var fullMessage = exception != null ? $"{message}\n\nError: {exception.Message}" : message;
            
            var notification = new NotificationModel(title, fullMessage, NotificationType.Error)
            {
                Category = "Error",
                SourceService = "System",
                Priority = NotificationPriority.High,
                IsPersistent = true
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowSuccessNotificationAsync(string title, string message)
        {
            if (!_settings.EnableSuccessNotifications) return;

            var notification = new NotificationModel(title, message, NotificationType.Success)
            {
                Category = "Success",
                SourceService = "System",
                Priority = NotificationPriority.Normal
            };

            await ShowNotificationAsync(notification);
        }

        public async Task DismissNotificationAsync(string notificationId)
        {
            var notification = _notificationHistory.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                NotificationDismissed?.Invoke(this, new NotificationEventArgs(notification));
                _logger.LogDebug("Notification dismissed: {Id}", notificationId);
            }
            await Task.CompletedTask;
        }

        public async Task DismissAllNotificationsAsync()
        {
            foreach (var notification in _notificationHistory.ToList())
            {
                NotificationDismissed?.Invoke(this, new NotificationEventArgs(notification));
            }
            _logger.LogDebug("All notifications dismissed");
            await Task.CompletedTask;
        }

        public async Task ClearNotificationHistoryAsync()
        {
            _notificationHistory.Clear();
            _logger.LogInformation("Notification history cleared");
            await Task.CompletedTask;
        }

        public int GetUnreadNotificationCount()
        {
            return _notificationHistory.Count(n => !n.IsRead);
        }

        public async Task MarkAllNotificationsAsReadAsync()
        {
            foreach (var notification in _notificationHistory)
            {
                notification.MarkAsRead();
            }
            _logger.LogDebug("All notifications marked as read");
            await Task.CompletedTask;
        }

        public bool AreNotificationsEnabled(NotificationType type)
        {
            if (!_settings.EnableNotifications) return false;

            return type switch
            {
                NotificationType.PowerPlanChange => _settings.EnablePowerPlanChangeNotifications,
                NotificationType.ProcessMonitoring => _settings.EnableProcessMonitoringNotifications,
                NotificationType.Error => _settings.EnableErrorNotifications,
                NotificationType.Success => _settings.EnableSuccessNotifications,
                _ => true
            };
        }

        public void UpdateSettings(ApplicationSettingsModel settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger.LogDebug("Notification settings updated");
        }

        private void AddToHistory(NotificationModel notification)
        {
            if (!_settings.EnableNotificationHistory) return;

            _notificationHistory.Insert(0, notification);

            // Trim history if it exceeds max items
            while (_notificationHistory.Count > _settings.MaxNotificationHistoryItems)
            {
                _notificationHistory.RemoveAt(_notificationHistory.Count - 1);
            }
        }

        private async Task ShowBalloonTipInternalAsync(NotificationModel notification)
        {
            try
            {
                // Use the system tray service to show the actual balloon tip
                _systemTrayService.ShowTrayNotification(
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.DurationMs);

                _logger.LogDebug("Balloon tip shown via system tray: {Title} - {Message}", notification.Title, notification.Message);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing balloon tip");
            }
        }

        private async Task ShowToastNotificationInternalAsync(NotificationModel notification)
        {
            try
            {
                // Toast notifications would require Windows 10+ and additional setup
                // For now, we'll just log it
                _logger.LogDebug("Toast notification: {Title} - {Message}", notification.Title, notification.Message);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing toast notification");
            }
        }

        private void OnSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            UpdateSettings(e.NewSettings);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
                _disposed = true;
                _logger.LogInformation("Notification service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing notification service");
            }
        }
    }
}
