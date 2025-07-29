using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ThreadPilot.Services;
using ThreadPilot.Models.Core;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Model for application settings including notifications and tray preferences
    /// </summary>
    public partial class ApplicationSettingsModel : ObservableObject, IModel
    {
        [ObservableProperty]
        private string id = "ApplicationSettings"; // Singleton settings

        [ObservableProperty]
        private DateTime createdAt = DateTime.UtcNow;

        [ObservableProperty]
        private DateTime updatedAt = DateTime.UtcNow;

        [ObservableProperty]
        private bool enableNotifications = true;

        [ObservableProperty]
        private bool enableBalloonNotifications = true;

        [ObservableProperty]
        private bool enableToastNotifications = true;

        [ObservableProperty]
        private bool enablePowerPlanChangeNotifications = true;

        [ObservableProperty]
        private bool enableProcessMonitoringNotifications = true;

        [ObservableProperty]
        private bool enableErrorNotifications = true;

        [ObservableProperty]
        private bool enableSuccessNotifications = true;

        [ObservableProperty]
        private bool minimizeToTray = true;

        [ObservableProperty]
        private bool closeToTray = false;

        [ObservableProperty]
        private bool startMinimized = false;

        [ObservableProperty]
        private bool showTrayIcon = true;

        [ObservableProperty]
        private bool enableQuickApplyFromTray = true;

        [ObservableProperty]
        private bool enableMonitoringControlFromTray = true;

        [ObservableProperty]
        private int notificationDisplayDurationMs = 3000;

        [ObservableProperty]
        private int balloonNotificationTimeoutMs = 5000;

        [ObservableProperty]
        private NotificationPosition notificationPosition = NotificationPosition.BottomRight;

        [ObservableProperty]
        private NotificationSound notificationSound = NotificationSound.Default;

        [ObservableProperty]
        private bool enableNotificationSound = false;

        [ObservableProperty]
        private string customTrayIconPath = string.Empty;

        [ObservableProperty]
        private bool useCustomTrayIcon = false;

        [ObservableProperty]
        private TrayIconStyle trayIconStyle = TrayIconStyle.Default;

        [ObservableProperty]
        private bool showDetailedTooltips = true;

        [ObservableProperty]
        private bool enableContextMenuAnimations = true;

        [ObservableProperty]
        private bool autoHideNotifications = true;

        [ObservableProperty]
        private bool enableNotificationHistory = true;

        [ObservableProperty]
        private int maxNotificationHistoryItems = 50;

        // Autostart Settings
        [ObservableProperty]
        private bool autostartWithWindows = false;

        // Power Plan Settings
        [ObservableProperty]
        private string defaultPowerPlanId = string.Empty;

        [ObservableProperty]
        private string defaultPowerPlanName = "Balanced";

        [ObservableProperty]
        private bool restoreDefaultPowerPlanOnExit = true;

        // Monitoring Settings
        [ObservableProperty]
        private int pollingIntervalMs = 5000;

        [ObservableProperty]
        private int fallbackPollingIntervalMs = 10000;

        [ObservableProperty]
        private bool enableWmiMonitoring = true;

        [ObservableProperty]
        private bool enableFallbackPolling = true;

        // Game Boost Mode Settings
        [ObservableProperty]
        private bool enableGameBoostMode = false;

        [ObservableProperty]
        private string gameBoostPowerPlanId = string.Empty;

        [ObservableProperty]
        private string gameBoostPowerPlanName = "High performance";

        [ObservableProperty]
        private bool gameBoostAutoDetectGames = true;

        [ObservableProperty]
        private bool gameBoostSetHighPriority = true;

        [ObservableProperty]
        private bool gameBoostOptimizeCpuAffinity = true;

        [ObservableProperty]
        private int gameBoostDetectionDelayMs = 2000;

        // Advanced Settings
        [ObservableProperty]
        private bool enableDebugLogging = false;

        [ObservableProperty]
        private bool enablePerformanceCounters = false;

        [ObservableProperty]
        private int maxLogFileSizeMb = 10;

        [ObservableProperty]
        private int logRetentionDays = 7;

        /// <summary>
        /// Keyboard shortcuts configuration
        /// </summary>
        [ObservableProperty]
        private List<KeyboardShortcut> keyboardShortcuts = new();

        /// <summary>
        /// Copies settings from another instance
        /// </summary>
        public void CopyFrom(ApplicationSettingsModel other)
        {
            if (other == null) return;

            EnableNotifications = other.EnableNotifications;
            EnableBalloonNotifications = other.EnableBalloonNotifications;
            EnableToastNotifications = other.EnableToastNotifications;
            EnablePowerPlanChangeNotifications = other.EnablePowerPlanChangeNotifications;
            EnableProcessMonitoringNotifications = other.EnableProcessMonitoringNotifications;
            EnableErrorNotifications = other.EnableErrorNotifications;
            EnableSuccessNotifications = other.EnableSuccessNotifications;
            MinimizeToTray = other.MinimizeToTray;
            CloseToTray = other.CloseToTray;
            StartMinimized = other.StartMinimized;
            ShowTrayIcon = other.ShowTrayIcon;
            EnableQuickApplyFromTray = other.EnableQuickApplyFromTray;
            EnableMonitoringControlFromTray = other.EnableMonitoringControlFromTray;
            NotificationDisplayDurationMs = other.NotificationDisplayDurationMs;
            BalloonNotificationTimeoutMs = other.BalloonNotificationTimeoutMs;
            NotificationPosition = other.NotificationPosition;
            NotificationSound = other.NotificationSound;
            EnableNotificationSound = other.EnableNotificationSound;
            CustomTrayIconPath = other.CustomTrayIconPath;
            UseCustomTrayIcon = other.UseCustomTrayIcon;
            TrayIconStyle = other.TrayIconStyle;
            ShowDetailedTooltips = other.ShowDetailedTooltips;
            EnableContextMenuAnimations = other.EnableContextMenuAnimations;
            AutoHideNotifications = other.AutoHideNotifications;
            EnableNotificationHistory = other.EnableNotificationHistory;
            MaxNotificationHistoryItems = other.MaxNotificationHistoryItems;

            // Autostart Settings
            AutostartWithWindows = other.AutostartWithWindows;

            // Power Plan Settings
            DefaultPowerPlanId = other.DefaultPowerPlanId;
            DefaultPowerPlanName = other.DefaultPowerPlanName;
            RestoreDefaultPowerPlanOnExit = other.RestoreDefaultPowerPlanOnExit;

            // Monitoring Settings
            PollingIntervalMs = other.PollingIntervalMs;
            FallbackPollingIntervalMs = other.FallbackPollingIntervalMs;
            EnableWmiMonitoring = other.EnableWmiMonitoring;
            EnableFallbackPolling = other.EnableFallbackPolling;

            // Game Boost Mode Settings
            EnableGameBoostMode = other.EnableGameBoostMode;
            GameBoostPowerPlanId = other.GameBoostPowerPlanId;
            GameBoostPowerPlanName = other.GameBoostPowerPlanName;
            GameBoostAutoDetectGames = other.GameBoostAutoDetectGames;
            GameBoostSetHighPriority = other.GameBoostSetHighPriority;
            GameBoostOptimizeCpuAffinity = other.GameBoostOptimizeCpuAffinity;
            GameBoostDetectionDelayMs = other.GameBoostDetectionDelayMs;

            // Advanced Settings
            EnableDebugLogging = other.EnableDebugLogging;
            EnablePerformanceCounters = other.EnablePerformanceCounters;
            MaxLogFileSizeMb = other.MaxLogFileSizeMb;
            LogRetentionDays = other.LogRetentionDays;

            // Keyboard Shortcuts
            KeyboardShortcuts = new List<KeyboardShortcut>(other.KeyboardShortcuts);
        }

        // IModel implementation - properties are auto-generated by ObservableProperty
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (NotificationDisplayDurationMs < 1000 || NotificationDisplayDurationMs > 30000)
                errors.Add("Notification display duration must be between 1 and 30 seconds");

            if (PollingIntervalMs < 1000 || PollingIntervalMs > 60000)
                errors.Add("Process polling interval must be between 1 and 60 seconds");

            if (FallbackPollingIntervalMs < 1000 || FallbackPollingIntervalMs > 60000)
                errors.Add("Fallback polling interval must be between 1 and 60 seconds");

            return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors.ToArray());
        }

        public IModel Clone()
        {
            return new ApplicationSettingsModel
            {
                id = "ApplicationSettings",
                EnableNotifications = this.EnableNotifications,
                EnableBalloonNotifications = this.EnableBalloonNotifications,
                EnableToastNotifications = this.EnableToastNotifications,
                EnablePowerPlanChangeNotifications = this.EnablePowerPlanChangeNotifications,
                EnableProcessMonitoringNotifications = this.EnableProcessMonitoringNotifications,
                EnableErrorNotifications = this.EnableErrorNotifications,
                EnableSuccessNotifications = this.EnableSuccessNotifications,
                MinimizeToTray = this.MinimizeToTray,
                CloseToTray = this.CloseToTray,
                StartMinimized = this.StartMinimized,
                ShowTrayIcon = this.ShowTrayIcon,
                EnableQuickApplyFromTray = this.EnableQuickApplyFromTray,
                EnableMonitoringControlFromTray = this.EnableMonitoringControlFromTray,
                NotificationDisplayDurationMs = this.NotificationDisplayDurationMs,
                BalloonNotificationTimeoutMs = this.BalloonNotificationTimeoutMs,
                NotificationPosition = this.NotificationPosition,
                NotificationSound = this.NotificationSound,
                EnableNotificationSound = this.EnableNotificationSound,
                TrayIconStyle = this.TrayIconStyle,
                AutostartWithWindows = this.AutostartWithWindows,
                DefaultPowerPlanId = this.DefaultPowerPlanId,
                DefaultPowerPlanName = this.DefaultPowerPlanName,
                RestoreDefaultPowerPlanOnExit = this.RestoreDefaultPowerPlanOnExit,
                PollingIntervalMs = this.PollingIntervalMs,
                FallbackPollingIntervalMs = this.FallbackPollingIntervalMs,
                EnableWmiMonitoring = this.EnableWmiMonitoring,
                EnableFallbackPolling = this.EnableFallbackPolling,
                EnableGameBoostMode = this.EnableGameBoostMode,
                GameBoostPowerPlanId = this.GameBoostPowerPlanId,
                GameBoostPowerPlanName = this.GameBoostPowerPlanName,
                GameBoostAutoDetectGames = this.GameBoostAutoDetectGames,
                GameBoostSetHighPriority = this.GameBoostSetHighPriority,
                GameBoostOptimizeCpuAffinity = this.GameBoostOptimizeCpuAffinity,
                GameBoostDetectionDelayMs = this.GameBoostDetectionDelayMs,
                EnableDebugLogging = this.EnableDebugLogging,
                EnablePerformanceCounters = this.EnablePerformanceCounters,
                MaxLogFileSizeMb = this.MaxLogFileSizeMb,
                LogRetentionDays = this.LogRetentionDays,
                KeyboardShortcuts = new List<KeyboardShortcut>(this.KeyboardShortcuts),
                CreatedAt = this.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Notification position options
    /// </summary>
    public enum NotificationPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    /// <summary>
    /// Notification sound options
    /// </summary>
    public enum NotificationSound
    {
        None,
        Default,
        Information,
        Warning,
        Error,
        Custom
    }

    /// <summary>
    /// Tray icon style options
    /// </summary>
    public enum TrayIconStyle
    {
        Default,
        Monochrome,
        Colored,
        Custom
    }
}
