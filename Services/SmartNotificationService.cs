using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of smart notification service with throttling and priority queuing
    /// </summary>
    public class SmartNotificationService : ISmartNotificationService, IDisposable
    {
        private readonly ILogger<SmartNotificationService> _logger;
        private readonly INotificationService _baseNotificationService;
        private readonly ConcurrentQueue<SmartNotification> _notificationQueue = new();
        private readonly ConcurrentDictionary<string, SmartNotification> _scheduledNotifications = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastNotificationTimes = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> _notificationHistory = new();
        private readonly List<SmartNotification> _sentNotifications = new();
        private readonly System.Threading.Timer _processingTimer;
        private readonly System.Threading.Timer _cleanupTimer;
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        
        private NotificationPreferences _preferences = new();
        private DateTime? _doNotDisturbUntil;
        private bool _disposed;

        public event EventHandler<SmartNotificationEventArgs>? NotificationSent;
        public event EventHandler<SmartNotificationEventArgs>? NotificationThrottled;
        public event EventHandler<SmartNotificationEventArgs>? NotificationDeduplicated;
        public event EventHandler<bool>? DoNotDisturbChanged;

        public SmartNotificationService(
            ILogger<SmartNotificationService> logger,
            INotificationService baseNotificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseNotificationService = baseNotificationService ?? throw new ArgumentNullException(nameof(baseNotificationService));

            // Set up processing timer (process queue every 2 seconds)
            _processingTimer = new System.Threading.Timer(ProcessQueueCallback, null, 
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            // Set up cleanup timer (clean history every hour)
            _cleanupTimer = new System.Threading.Timer(CleanupCallback, null, 
                TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing SmartNotificationService");
            
            // Initialize default preferences
            _preferences = CreateDefaultPreferences();
            
            // Load preferences from storage (simplified)
            await LoadPreferencesAsync();
        }

        public async Task<bool> SendNotificationAsync(SmartNotification notification)
        {
            try
            {
                // Validate notification
                if (string.IsNullOrWhiteSpace(notification.Title) && string.IsNullOrWhiteSpace(notification.Message))
                {
                    _logger.LogWarning("Attempted to send notification with empty title and message");
                    return false;
                }

                // Check if notifications are enabled
                if (!_preferences.IsEnabled)
                {
                    _logger.LogDebug("Notifications are disabled, skipping notification: {Title}", notification.Title);
                    return false;
                }

                // Check category preferences
                if (_preferences.CategoryEnabled.TryGetValue(notification.Category, out var categoryEnabled) && !categoryEnabled)
                {
                    _logger.LogDebug("Category {Category} is disabled, skipping notification: {Title}", 
                        notification.Category, notification.Title);
                    return false;
                }

                // Check minimum priority
                if (notification.Priority < _preferences.MinimumPriority)
                {
                    _logger.LogDebug("Notification priority {Priority} below minimum {MinPriority}, skipping: {Title}", 
                        notification.Priority, _preferences.MinimumPriority, notification.Title);
                    return false;
                }

                // Check Do Not Disturb mode
                if (IsDoNotDisturbActive() && notification.Priority < NotificationPriority.Critical)
                {
                    _logger.LogDebug("Do Not Disturb is active, skipping non-critical notification: {Title}", notification.Title);
                    return false;
                }

                // Check throttling
                if (IsThrottled(notification))
                {
                    NotificationThrottled?.Invoke(this, new SmartNotificationEventArgs
                    {
                        Notification = notification,
                        Reason = "Throttled due to rate limiting"
                    });
                    return false;
                }

                // Check deduplication
                if (IsDuplicate(notification))
                {
                    NotificationDeduplicated?.Invoke(this, new SmartNotificationEventArgs
                    {
                        Notification = notification,
                        Reason = "Deduplicated - similar notification recently sent"
                    });
                    return false;
                }

                // Add to queue for processing
                _notificationQueue.Enqueue(notification);
                _logger.LogDebug("Queued notification: {Title} (Priority: {Priority}, Category: {Category})", 
                    notification.Title, notification.Priority, notification.Category);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification: {Title}", notification.Title);
                return false;
            }
        }

        public async Task<bool> SendNotificationAsync(string title, string message, 
            NotificationPriority priority = NotificationPriority.Normal,
            NotificationCategory category = NotificationCategory.Information)
        {
            var notification = new SmartNotification
            {
                Title = title,
                Message = message,
                Priority = priority,
                Category = category,
                DeduplicationKey = $"{category}:{title}:{message}".GetHashCode().ToString()
            };

            return await SendNotificationAsync(notification);
        }

        public async Task<bool> ScheduleNotificationAsync(SmartNotification notification, DateTime deliveryTime)
        {
            notification.ScheduledFor = deliveryTime;
            _scheduledNotifications.TryAdd(notification.Id, notification);
            
            _logger.LogDebug("Scheduled notification {Id} for delivery at {DeliveryTime}", 
                notification.Id, deliveryTime);
            
            return true;
        }

        public async Task<bool> CancelNotificationAsync(string notificationId)
        {
            var removed = _scheduledNotifications.TryRemove(notificationId, out var notification);
            if (removed)
            {
                _logger.LogDebug("Cancelled scheduled notification: {Id}", notificationId);
            }
            return removed;
        }

        public async Task<List<SmartNotification>> GetPendingNotificationsAsync()
        {
            var pending = new List<SmartNotification>();
            
            // Add queued notifications
            while (_notificationQueue.TryDequeue(out var notification))
            {
                pending.Add(notification);
                _notificationQueue.Enqueue(notification); // Put it back
            }

            // Add scheduled notifications
            pending.AddRange(_scheduledNotifications.Values);

            return pending.OrderByDescending(n => n.Priority).ThenBy(n => n.CreatedAt).ToList();
        }

        public async Task<List<SmartNotification>> GetNotificationHistoryAsync(TimeSpan? period = null)
        {
            var cutoff = period.HasValue ? DateTime.UtcNow - period.Value : DateTime.MinValue;
            
            lock (_sentNotifications)
            {
                return _sentNotifications
                    .Where(n => n.CreatedAt >= cutoff)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();
            }
        }

        public async Task ClearHistoryAsync()
        {
            lock (_sentNotifications)
            {
                _sentNotifications.Clear();
            }
            
            _notificationHistory.Clear();
            _logger.LogInformation("Cleared notification history");
        }

        public async Task UpdatePreferencesAsync(NotificationPreferences preferences)
        {
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            await SavePreferencesAsync();
            _logger.LogInformation("Updated notification preferences");
        }

        public async Task<NotificationPreferences> GetPreferencesAsync()
        {
            return _preferences;
        }

        public async Task SetDoNotDisturbAsync(bool enabled, TimeSpan? duration = null)
        {
            var wasActive = IsDoNotDisturbActive();
            
            if (enabled)
            {
                _doNotDisturbUntil = duration.HasValue 
                    ? DateTime.UtcNow + duration.Value 
                    : DateTime.MaxValue;
                _preferences.DoNotDisturbMode = true;
            }
            else
            {
                _doNotDisturbUntil = null;
                _preferences.DoNotDisturbMode = false;
            }

            var isActive = IsDoNotDisturbActive();
            if (wasActive != isActive)
            {
                DoNotDisturbChanged?.Invoke(this, isActive);
                _logger.LogInformation("Do Not Disturb mode {Status}", isActive ? "enabled" : "disabled");
            }
        }

        public bool IsDoNotDisturbActive()
        {
            if (!_preferences.DoNotDisturbMode) return false;
            
            if (_doNotDisturbUntil.HasValue && DateTime.UtcNow > _doNotDisturbUntil.Value)
            {
                _preferences.DoNotDisturbMode = false;
                _doNotDisturbUntil = null;
                return false;
            }

            // Check time-based DND
            var now = DateTime.Now.TimeOfDay;
            if (_preferences.DoNotDisturbStart < _preferences.DoNotDisturbEnd)
            {
                // Same day range (e.g., 10 PM to 8 AM next day)
                return now >= _preferences.DoNotDisturbStart || now <= _preferences.DoNotDisturbEnd;
            }
            else
            {
                // Cross-midnight range (e.g., 10 PM to 8 AM)
                return now >= _preferences.DoNotDisturbStart && now <= _preferences.DoNotDisturbEnd;
            }
        }

        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();
            
            lock (_sentNotifications)
            {
                var last24Hours = _sentNotifications.Where(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-1)).ToList();
                var lastWeek = _sentNotifications.Where(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-7)).ToList();
                
                stats["TotalSent"] = _sentNotifications.Count;
                stats["SentLast24Hours"] = last24Hours.Count;
                stats["SentLastWeek"] = lastWeek.Count;
                stats["PendingCount"] = _notificationQueue.Count;
                stats["ScheduledCount"] = _scheduledNotifications.Count;
                
                // Category breakdown
                var categoryStats = _sentNotifications
                    .GroupBy(n => n.Category)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());
                stats["ByCategory"] = categoryStats;
                
                // Priority breakdown
                var priorityStats = _sentNotifications
                    .GroupBy(n => n.Priority)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());
                stats["ByPriority"] = priorityStats;
            }
            
            return stats;
        }

        public async Task<bool> TestNotificationAsync()
        {
            var testNotification = new SmartNotification
            {
                Title = "Test Notification",
                Message = "This is a test notification from ThreadPilot Smart Notification System",
                Priority = NotificationPriority.Normal,
                Category = NotificationCategory.System
            };

            return await SendNotificationAsync(testNotification);
        }

        private async void ProcessQueueCallback(object? state)
        {
            if (_disposed) return;

            await _processingLock.WaitAsync();
            try
            {
                var processedCount = 0;
                var maxProcessPerCycle = 10;

                while (_notificationQueue.TryDequeue(out var notification) && processedCount < maxProcessPerCycle)
                {
                    await ProcessNotificationAsync(notification);
                    processedCount++;
                }

                // Process scheduled notifications
                var now = DateTime.UtcNow;
                var dueNotifications = _scheduledNotifications.Values
                    .Where(n => n.ScheduledFor <= now)
                    .ToList();

                foreach (var notification in dueNotifications)
                {
                    _scheduledNotifications.TryRemove(notification.Id, out _);
                    await ProcessNotificationAsync(notification);
                }
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task ProcessNotificationAsync(SmartNotification notification)
        {
            try
            {
                // Check if notification has expired
                if (notification.ExpiresAfter.HasValue &&
                    DateTime.UtcNow - notification.CreatedAt > notification.ExpiresAfter.Value)
                {
                    _logger.LogDebug("Notification expired: {Title}", notification.Title);
                    return;
                }

                // Send through base notification service
                await _baseNotificationService.ShowNotificationAsync(
                    notification.Title,
                    notification.Message,
                    ConvertToNotificationType(notification.Priority));

                // Assume success since no exception was thrown
                var success = true;

                if (success)
                {
                    // Record successful delivery
                    RecordNotificationSent(notification);

                    NotificationSent?.Invoke(this, new SmartNotificationEventArgs
                    {
                        Notification = notification,
                        Reason = "Successfully delivered"
                    });

                    _logger.LogDebug("Successfully sent notification: {Title}", notification.Title);
                }
                else if (notification.RetryCount < notification.MaxRetries)
                {
                    // Retry failed notification
                    notification.RetryCount++;
                    _notificationQueue.Enqueue(notification);
                    _logger.LogDebug("Retrying notification: {Title} (Attempt {Retry}/{Max})",
                        notification.Title, notification.RetryCount, notification.MaxRetries);
                }
                else
                {
                    _logger.LogWarning("Failed to send notification after {MaxRetries} attempts: {Title}",
                        notification.MaxRetries, notification.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification: {Title}", notification.Title);
            }
        }

        private bool IsThrottled(SmartNotification notification)
        {
            if (!_preferences.ThrottleConfigs.TryGetValue(notification.Category, out var config))
            {
                return false; // No throttling configured for this category
            }

            var key = $"{notification.Category}:{notification.DeduplicationKey}";
            var now = DateTime.UtcNow;

            // Check minimum interval
            if (_lastNotificationTimes.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < config.MinInterval)
                {
                    return true;
                }
            }

            // Check hourly and daily limits
            if (!_notificationHistory.TryGetValue(key, out var history))
            {
                history = new List<DateTime>();
                _notificationHistory[key] = history;
            }

            // Clean old entries
            var oneHourAgo = now.AddHours(-1);
            var oneDayAgo = now.AddDays(-1);
            history.RemoveAll(t => t < oneDayAgo);

            var hourlyCount = history.Count(t => t >= oneHourAgo);
            var dailyCount = history.Count;

            return hourlyCount >= config.MaxPerHour || dailyCount >= config.MaxPerDay;
        }

        private bool IsDuplicate(SmartNotification notification)
        {
            if (string.IsNullOrEmpty(notification.DeduplicationKey)) return false;

            if (!_preferences.ThrottleConfigs.TryGetValue(notification.Category, out var config) ||
                !config.EnableDeduplication)
            {
                return false;
            }

            var key = $"{notification.Category}:{notification.DeduplicationKey}";
            if (_lastNotificationTimes.TryGetValue(key, out var lastTime))
            {
                return DateTime.UtcNow - lastTime < config.DeduplicationWindow;
            }

            return false;
        }

        private void RecordNotificationSent(SmartNotification notification)
        {
            var key = $"{notification.Category}:{notification.DeduplicationKey}";
            var now = DateTime.UtcNow;

            _lastNotificationTimes[key] = now;

            if (!_notificationHistory.TryGetValue(key, out var history))
            {
                history = new List<DateTime>();
                _notificationHistory[key] = history;
            }
            history.Add(now);

            lock (_sentNotifications)
            {
                _sentNotifications.Add(notification);

                // Keep only last 1000 notifications in memory
                if (_sentNotifications.Count > 1000)
                {
                    _sentNotifications.RemoveRange(0, _sentNotifications.Count - 1000);
                }
            }
        }

        private NotificationType ConvertToNotificationType(NotificationPriority priority)
        {
            return priority switch
            {
                NotificationPriority.Critical => NotificationType.Error,
                NotificationPriority.High => NotificationType.Warning,
                NotificationPriority.Normal => NotificationType.Information,
                NotificationPriority.Low => NotificationType.Information,
                _ => NotificationType.Information
            };
        }

        private NotificationPreferences CreateDefaultPreferences()
        {
            var preferences = new NotificationPreferences();

            // Enable all categories by default
            foreach (NotificationCategory category in Enum.GetValues<NotificationCategory>())
            {
                preferences.CategoryEnabled[category] = true;
                preferences.ThrottleConfigs[category] = new NotificationThrottleConfig
                {
                    Category = category,
                    MinInterval = TimeSpan.FromSeconds(30),
                    MaxPerHour = category == NotificationCategory.Error ? 20 : 10,
                    MaxPerDay = category == NotificationCategory.Error ? 100 : 50
                };
            }

            return preferences;
        }

        private async Task LoadPreferencesAsync()
        {
            // Simplified - would load from actual storage
            _logger.LogDebug("Loaded notification preferences");
        }

        private async Task SavePreferencesAsync()
        {
            // Simplified - would save to actual storage
            _logger.LogDebug("Saved notification preferences");
        }

        private async void CleanupCallback(object? state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-7);

                // Clean notification history
                var keysToRemove = new List<string>();
                foreach (var kvp in _notificationHistory)
                {
                    kvp.Value.RemoveAll(t => t < cutoff);
                    if (!kvp.Value.Any())
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _notificationHistory.TryRemove(key, out _);
                }

                _logger.LogDebug("Cleaned up notification history, removed {Count} empty entries", keysToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during notification cleanup");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _processingTimer?.Dispose();
                    _cleanupTimer?.Dispose();
                    _processingLock?.Dispose();
                    _logger.LogInformation("SmartNotificationService disposed");
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
