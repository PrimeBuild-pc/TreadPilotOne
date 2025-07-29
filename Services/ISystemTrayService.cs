using System;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing system tray functionality
    /// </summary>
    public interface ISystemTrayService : IDisposable
    {
        /// <summary>
        /// Event fired when quick apply is requested from tray
        /// </summary>
        event EventHandler? QuickApplyRequested;

        /// <summary>
        /// Event fired when show main window is requested from tray
        /// </summary>
        event EventHandler? ShowMainWindowRequested;

        /// <summary>
        /// Event fired when exit is requested from tray
        /// </summary>
        event EventHandler? ExitRequested;

        /// <summary>
        /// Event fired when monitoring enable/disable is requested from tray
        /// </summary>
        event EventHandler<MonitoringToggleEventArgs>? MonitoringToggleRequested;

        /// <summary>
        /// Event fired when settings are requested from tray
        /// </summary>
        event EventHandler? SettingsRequested;

        /// <summary>
        /// Event fired when power plan change is requested from tray
        /// </summary>
        event EventHandler<PowerPlanChangeRequestedEventArgs>? PowerPlanChangeRequested;

        /// <summary>
        /// Event fired when profile application is requested from tray
        /// </summary>
        event EventHandler<ProfileApplicationRequestedEventArgs>? ProfileApplicationRequested;

        /// <summary>
        /// Event fired when performance dashboard is requested from tray
        /// </summary>
        event EventHandler? PerformanceDashboardRequested;

        /// <summary>
        /// Initializes the system tray icon
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shows the system tray icon
        /// </summary>
        void Show();

        /// <summary>
        /// Hides the system tray icon
        /// </summary>
        void Hide();

        /// <summary>
        /// Updates the tray icon tooltip
        /// </summary>
        void UpdateTooltip(string tooltip);

        /// <summary>
        /// Shows a balloon tip notification
        /// </summary>
        void ShowBalloonTip(string title, string text, int timeoutMs = 3000);

        /// <summary>
        /// Updates the context menu with current process information
        /// </summary>
        void UpdateContextMenu(string? selectedProcessName = null, bool hasSelection = false);

        /// <summary>
        /// Updates the monitoring status in the context menu
        /// </summary>
        void UpdateMonitoringStatus(bool isMonitoring, bool isWmiAvailable = true);

        /// <summary>
        /// Updates the tray icon based on application state
        /// </summary>
        void UpdateTrayIcon(TrayIconState state);

        /// <summary>
        /// Shows a notification through the tray icon
        /// </summary>
        void ShowTrayNotification(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000);

        /// <summary>
        /// Updates settings for the tray service
        /// </summary>
        void UpdateSettings(ApplicationSettingsModel settings);

        /// <summary>
        /// Updates the Game Boost status in the tray
        /// </summary>
        void UpdateGameBoostStatus(bool isGameBoostActive, string? currentGameName = null);

        /// <summary>
        /// Updates the available power plans in the context menu
        /// </summary>
        void UpdatePowerPlans(IEnumerable<PowerPlanModel> powerPlans, PowerPlanModel? activePlan);

        /// <summary>
        /// Updates the available profiles in the context menu
        /// </summary>
        void UpdateProfiles(IEnumerable<string> profileNames);

        /// <summary>
        /// Updates the current system status in the tray
        /// </summary>
        void UpdateSystemStatus(string currentPowerPlan, double cpuUsage, double memoryUsage);
    }

    /// <summary>
    /// Event args for monitoring toggle events
    /// </summary>
    public class MonitoringToggleEventArgs : EventArgs
    {
        public bool EnableMonitoring { get; }

        public MonitoringToggleEventArgs(bool enableMonitoring)
        {
            EnableMonitoring = enableMonitoring;
        }
    }

    /// <summary>
    /// Event args for power plan change requests
    /// </summary>
    public class PowerPlanChangeRequestedEventArgs : EventArgs
    {
        public string PowerPlanGuid { get; }
        public string PowerPlanName { get; }

        public PowerPlanChangeRequestedEventArgs(string powerPlanGuid, string powerPlanName)
        {
            PowerPlanGuid = powerPlanGuid;
            PowerPlanName = powerPlanName;
        }
    }

    /// <summary>
    /// Event args for profile application requests
    /// </summary>
    public class ProfileApplicationRequestedEventArgs : EventArgs
    {
        public string ProfileName { get; }

        public ProfileApplicationRequestedEventArgs(string profileName)
        {
            ProfileName = profileName;
        }
    }

    /// <summary>
    /// Tray icon states
    /// </summary>
    public enum TrayIconState
    {
        Normal,
        Monitoring,
        Error,
        Disabled,
        GameBoost
    }
}
