using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{


    /// <summary>
    /// Notification categories for throttling
    /// </summary>
    public enum NotificationCategory
    {
        System,
        Process,
        Performance,
        GameBoost,
        PowerPlan,
        Error,
        Warning,
        Information,
        UserAction
    }

    /// <summary>
    /// Smart notification with metadata
    /// </summary>
    public class SmartNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        public NotificationCategory Category { get; set; } = NotificationCategory.Information;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ScheduledFor { get; set; }
        public TimeSpan? ExpiresAfter { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string DeduplicationKey { get; set; } = string.Empty;
        public bool IsPersistent { get; set; } = false;
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
    }

    /// <summary>
    /// Notification throttling configuration
    /// </summary>
    public class NotificationThrottleConfig
    {
        public NotificationCategory Category { get; set; }
        public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxPerHour { get; set; } = 10;
        public int MaxPerDay { get; set; } = 50;
        public bool EnableDeduplication { get; set; } = true;
        public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// User notification preferences
    /// </summary>
    public class NotificationPreferences
    {
        public bool IsEnabled { get; set; } = true;
        public bool DoNotDisturbMode { get; set; } = false;
        public TimeSpan DoNotDisturbStart { get; set; } = TimeSpan.FromHours(22); // 10 PM
        public TimeSpan DoNotDisturbEnd { get; set; } = TimeSpan.FromHours(8);   // 8 AM
        public NotificationPriority MinimumPriority { get; set; } = NotificationPriority.Normal;
        public Dictionary<NotificationCategory, bool> CategoryEnabled { get; set; } = new();
        public Dictionary<NotificationCategory, NotificationThrottleConfig> ThrottleConfigs { get; set; } = new();
        public bool ShowOnlyWhenMinimized { get; set; } = false;
        public bool PlaySounds { get; set; } = true;
        public int DefaultDisplayDuration { get; set; } = 5000; // milliseconds
    }

    /// <summary>
    /// Event arguments for smart notification events
    /// </summary>
    public class SmartNotificationEventArgs : EventArgs
    {
        public SmartNotification Notification { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Smart notification service with throttling and priority queuing
    /// </summary>
    public interface ISmartNotificationService
    {
        /// <summary>
        /// Initialize the smart notification service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Send a smart notification
        /// </summary>
        Task<bool> SendNotificationAsync(SmartNotification notification);

        /// <summary>
        /// Send a simple notification
        /// </summary>
        Task<bool> SendNotificationAsync(string title, string message, 
            NotificationPriority priority = NotificationPriority.Normal,
            NotificationCategory category = NotificationCategory.Information);

        /// <summary>
        /// Schedule a notification for later delivery
        /// </summary>
        Task<bool> ScheduleNotificationAsync(SmartNotification notification, DateTime deliveryTime);

        /// <summary>
        /// Cancel a scheduled notification
        /// </summary>
        Task<bool> CancelNotificationAsync(string notificationId);

        /// <summary>
        /// Get pending notifications
        /// </summary>
        Task<List<SmartNotification>> GetPendingNotificationsAsync();

        /// <summary>
        /// Get notification history
        /// </summary>
        Task<List<SmartNotification>> GetNotificationHistoryAsync(TimeSpan? period = null);

        /// <summary>
        /// Clear notification history
        /// </summary>
        Task ClearHistoryAsync();

        /// <summary>
        /// Update user preferences
        /// </summary>
        Task UpdatePreferencesAsync(NotificationPreferences preferences);

        /// <summary>
        /// Get current user preferences
        /// </summary>
        Task<NotificationPreferences> GetPreferencesAsync();

        /// <summary>
        /// Enable/disable Do Not Disturb mode
        /// </summary>
        Task SetDoNotDisturbAsync(bool enabled, TimeSpan? duration = null);

        /// <summary>
        /// Check if Do Not Disturb is currently active
        /// </summary>
        bool IsDoNotDisturbActive();

        /// <summary>
        /// Get notification statistics
        /// </summary>
        Task<Dictionary<string, object>> GetStatisticsAsync();

        /// <summary>
        /// Test notification delivery
        /// </summary>
        Task<bool> TestNotificationAsync();

        /// <summary>
        /// Event raised when a notification is sent
        /// </summary>
        event EventHandler<SmartNotificationEventArgs>? NotificationSent;

        /// <summary>
        /// Event raised when a notification is throttled
        /// </summary>
        event EventHandler<SmartNotificationEventArgs>? NotificationThrottled;

        /// <summary>
        /// Event raised when a notification is deduplicated
        /// </summary>
        event EventHandler<SmartNotificationEventArgs>? NotificationDeduplicated;

        /// <summary>
        /// Event raised when Do Not Disturb mode changes
        /// </summary>
        event EventHandler<bool>? DoNotDisturbChanged;
    }
}
