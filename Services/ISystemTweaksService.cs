using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing Windows system tweaks and optimizations
    /// </summary>
    public interface ISystemTweaksService
    {
        /// <summary>
        /// Event raised when a tweak status changes
        /// </summary>
        event EventHandler<TweakStatusChangedEventArgs>? TweakStatusChanged;

        /// <summary>
        /// Get the current status of Core Parking
        /// </summary>
        Task<TweakStatus> GetCoreParkingStatusAsync();

        /// <summary>
        /// Enable or disable Core Parking
        /// </summary>
        Task<bool> SetCoreParkingAsync(bool enabled);

        /// <summary>
        /// Get the current status of C-States
        /// </summary>
        Task<TweakStatus> GetCStatesStatusAsync();

        /// <summary>
        /// Enable or disable C-States
        /// </summary>
        Task<bool> SetCStatesAsync(bool enabled);

        /// <summary>
        /// Get the current status of SysMain service
        /// </summary>
        Task<TweakStatus> GetSysMainStatusAsync();

        /// <summary>
        /// Enable or disable SysMain service
        /// </summary>
        Task<bool> SetSysMainAsync(bool enabled);

        /// <summary>
        /// Get the current status of Prefetch feature
        /// </summary>
        Task<TweakStatus> GetPrefetchStatusAsync();

        /// <summary>
        /// Enable or disable Prefetch feature
        /// </summary>
        Task<bool> SetPrefetchAsync(bool enabled);

        /// <summary>
        /// Get the current status of Power Throttling
        /// </summary>
        Task<TweakStatus> GetPowerThrottlingStatusAsync();

        /// <summary>
        /// Enable or disable Power Throttling
        /// </summary>
        Task<bool> SetPowerThrottlingAsync(bool enabled);

        /// <summary>
        /// Get the current status of HPET (High Precision Event Timer)
        /// </summary>
        Task<TweakStatus> GetHpetStatusAsync();

        /// <summary>
        /// Enable or disable HPET
        /// </summary>
        Task<bool> SetHpetAsync(bool enabled);

        /// <summary>
        /// Get the current status of High Scheduling Category for gaming
        /// </summary>
        Task<TweakStatus> GetHighSchedulingCategoryStatusAsync();

        /// <summary>
        /// Enable or disable High Scheduling Category for gaming
        /// </summary>
        Task<bool> SetHighSchedulingCategoryAsync(bool enabled);

        /// <summary>
        /// Get the current status of Menu Show Delay
        /// </summary>
        Task<TweakStatus> GetMenuShowDelayStatusAsync();

        /// <summary>
        /// Enable or disable Menu Show Delay
        /// </summary>
        Task<bool> SetMenuShowDelayAsync(bool enabled);

        /// <summary>
        /// Refresh all tweak statuses
        /// </summary>
        Task RefreshAllStatusesAsync();
    }

    /// <summary>
    /// Represents the status of a system tweak
    /// </summary>
    public class TweakStatus
    {
        public bool IsEnabled { get; set; }
        public bool IsAvailable { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Event args for tweak status changes
    /// </summary>
    public class TweakStatusChangedEventArgs : EventArgs
    {
        public string TweakName { get; }
        public TweakStatus Status { get; }
        public DateTime ChangeTime { get; }

        public TweakStatusChangedEventArgs(string tweakName, TweakStatus status)
        {
            TweakName = tweakName;
            Status = status;
            ChangeTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Enumeration of available system tweaks
    /// </summary>
    public enum SystemTweak
    {
        CoreParking,
        CStates,
        SysMain,
        Prefetch,
        PowerThrottling,
        Hpet,
        HighSchedulingCategory,
        MenuShowDelay
    }
}
