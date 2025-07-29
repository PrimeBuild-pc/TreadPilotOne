using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing notifications (balloon tips and toast notifications)
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Event fired when a notification is shown
        /// </summary>
        event EventHandler<NotificationEventArgs>? NotificationShown;

        /// <summary>
        /// Event fired when a notification is dismissed
        /// </summary>
        event EventHandler<NotificationEventArgs>? NotificationDismissed;

        /// <summary>
        /// Event fired when a notification action is clicked
        /// </summary>
        event EventHandler<NotificationActionEventArgs>? NotificationActionClicked;

        /// <summary>
        /// Gets the notification history
        /// </summary>
        IReadOnlyList<NotificationModel> NotificationHistory { get; }

        /// <summary>
        /// Shows a simple notification
        /// </summary>
        Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Information);

        /// <summary>
        /// Shows a notification with custom settings
        /// </summary>
        Task ShowNotificationAsync(NotificationModel notification);

        /// <summary>
        /// Shows a balloon tip notification
        /// </summary>
        Task ShowBalloonTipAsync(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000);

        /// <summary>
        /// Shows a Windows toast notification (if available)
        /// </summary>
        Task ShowToastNotificationAsync(string title, string message, NotificationType type = NotificationType.Information);

        /// <summary>
        /// Shows a notification for power plan changes
        /// </summary>
        Task ShowPowerPlanChangeNotificationAsync(string oldPlan, string newPlan, string processName = "");

        /// <summary>
        /// Shows a notification for process monitoring events
        /// </summary>
        Task ShowProcessMonitoringNotificationAsync(string message, bool isEnabled);

        /// <summary>
        /// Shows a notification for CPU affinity changes
        /// </summary>
        Task ShowCpuAffinityNotificationAsync(string processName, string affinityInfo);

        /// <summary>
        /// Shows an error notification
        /// </summary>
        Task ShowErrorNotificationAsync(string title, string message, Exception? exception = null);

        /// <summary>
        /// Shows a success notification
        /// </summary>
        Task ShowSuccessNotificationAsync(string title, string message);

        /// <summary>
        /// Dismisses a specific notification
        /// </summary>
        Task DismissNotificationAsync(string notificationId);

        /// <summary>
        /// Dismisses all notifications
        /// </summary>
        Task DismissAllNotificationsAsync();

        /// <summary>
        /// Clears notification history
        /// </summary>
        Task ClearNotificationHistoryAsync();

        /// <summary>
        /// Gets unread notification count
        /// </summary>
        int GetUnreadNotificationCount();

        /// <summary>
        /// Marks all notifications as read
        /// </summary>
        Task MarkAllNotificationsAsReadAsync();

        /// <summary>
        /// Checks if notifications are enabled for the given type
        /// </summary>
        bool AreNotificationsEnabled(NotificationType type);

        /// <summary>
        /// Updates notification settings
        /// </summary>
        void UpdateSettings(ApplicationSettingsModel settings);

        /// <summary>
        /// Initializes the notification service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Disposes the notification service
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Event args for notification events
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        public NotificationModel Notification { get; }

        public NotificationEventArgs(NotificationModel notification)
        {
            Notification = notification;
        }
    }

    /// <summary>
    /// Event args for notification action events
    /// </summary>
    public class NotificationActionEventArgs : EventArgs
    {
        public NotificationModel Notification { get; }
        public string ActionCommand { get; }

        public NotificationActionEventArgs(NotificationModel notification, string actionCommand)
        {
            Notification = notification;
            ActionCommand = actionCommand;
        }
    }
}
