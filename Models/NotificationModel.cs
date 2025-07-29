using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Model representing a notification
    /// </summary>
    public partial class NotificationModel : ObservableObject
    {
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string message = string.Empty;

        [ObservableProperty]
        private NotificationType type = NotificationType.Information;

        [ObservableProperty]
        private DateTime timestamp = DateTime.Now;

        [ObservableProperty]
        private int durationMs = 3000;

        [ObservableProperty]
        private bool isRead = false;

        [ObservableProperty]
        private bool isPersistent = false;

        [ObservableProperty]
        private string? actionText;

        [ObservableProperty]
        private string? actionCommand;

        [ObservableProperty]
        private string? iconPath;

        [ObservableProperty]
        private NotificationPriority priority = NotificationPriority.Normal;

        [ObservableProperty]
        private string? category;

        [ObservableProperty]
        private string? sourceService;

        /// <summary>
        /// Creates a new notification
        /// </summary>
        public NotificationModel()
        {
        }

        /// <summary>
        /// Creates a new notification with basic information
        /// </summary>
        public NotificationModel(string title, string message, NotificationType type = NotificationType.Information)
        {
            Title = title;
            Message = message;
            Type = type;
        }

        /// <summary>
        /// Creates a new notification with full information
        /// </summary>
        public NotificationModel(string title, string message, NotificationType type, int durationMs, bool isPersistent = false)
        {
            Title = title;
            Message = message;
            Type = type;
            DurationMs = durationMs;
            IsPersistent = isPersistent;
        }

        /// <summary>
        /// Marks the notification as read
        /// </summary>
        public void MarkAsRead()
        {
            IsRead = true;
        }

        /// <summary>
        /// Gets the display text for the notification type
        /// </summary>
        public string TypeDisplayText => Type switch
        {
            NotificationType.Information => "Info",
            NotificationType.Success => "Success",
            NotificationType.Warning => "Warning",
            NotificationType.Error => "Error",
            NotificationType.PowerPlanChange => "Power Plan",
            NotificationType.ProcessMonitoring => "Process Monitor",
            NotificationType.CpuAffinity => "CPU Affinity",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the formatted timestamp
        /// </summary>
        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");

        /// <summary>
        /// Gets the formatted date and time
        /// </summary>
        public string FormattedDateTime => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Types of notifications
    /// </summary>
    public enum NotificationType
    {
        Information,
        Success,
        Warning,
        Error,
        PowerPlanChange,
        ProcessMonitoring,
        CpuAffinity
    }

    /// <summary>
    /// Notification priority levels
    /// </summary>
    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}
