using System;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for process monitoring service that uses WMI events with fallback polling
    /// </summary>
    public interface IProcessMonitorService : IDisposable
    {
        /// <summary>
        /// Event fired when a process starts
        /// </summary>
        event EventHandler<ProcessEventArgs>? ProcessStarted;

        /// <summary>
        /// Event fired when a process stops
        /// </summary>
        event EventHandler<ProcessEventArgs>? ProcessStopped;

        /// <summary>
        /// Event fired when monitoring status changes
        /// </summary>
        event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged;

        /// <summary>
        /// Gets whether the service is currently monitoring
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Gets whether WMI monitoring is available and working
        /// </summary>
        bool IsWmiAvailable { get; }

        /// <summary>
        /// Gets whether fallback polling is currently active
        /// </summary>
        bool IsFallbackPollingActive { get; }

        /// <summary>
        /// Starts monitoring processes
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stops monitoring processes
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Gets all currently running processes
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync();

        /// <summary>
        /// Checks if a specific process is currently running
        /// </summary>
        Task<bool> IsProcessRunningAsync(string executableName);

        /// <summary>
        /// Updates the service settings (polling intervals, etc.)
        /// </summary>
        void UpdateSettings();
    }

    /// <summary>
    /// Event arguments for process events
    /// </summary>
    public class ProcessEventArgs : EventArgs
    {
        public ProcessModel Process { get; }
        public DateTime Timestamp { get; }

        public ProcessEventArgs(ProcessModel process)
        {
            Process = process;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for monitoring status changes
    /// </summary>
    public class MonitoringStatusEventArgs : EventArgs
    {
        public bool IsMonitoring { get; }
        public bool IsWmiAvailable { get; }
        public bool IsFallbackPollingActive { get; }
        public string? StatusMessage { get; }
        public Exception? Error { get; }

        public MonitoringStatusEventArgs(bool isMonitoring, bool isWmiAvailable, bool isFallbackPollingActive, string? statusMessage = null, Exception? error = null)
        {
            IsMonitoring = isMonitoring;
            IsWmiAvailable = isWmiAvailable;
            IsFallbackPollingActive = isFallbackPollingActive;
            StatusMessage = statusMessage;
            Error = error;
        }
    }
}
