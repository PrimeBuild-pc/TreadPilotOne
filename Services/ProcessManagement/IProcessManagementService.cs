using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services.ProcessManagement
{
    /// <summary>
    /// Unified interface for process management operations
    /// </summary>
    public interface IProcessManagementService
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
        /// Event fired when process monitoring status changes
        /// </summary>
        event EventHandler<MonitoringStatusChangedEventArgs>? MonitoringStatusChanged;

        /// <summary>
        /// Gets whether process monitoring is currently active
        /// </summary>
        bool IsMonitoringActive { get; }

        /// <summary>
        /// Gets all currently running processes
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync();

        /// <summary>
        /// Gets a specific process by ID
        /// </summary>
        Task<ProcessModel?> GetProcessByIdAsync(int processId);

        /// <summary>
        /// Gets processes by executable name
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetProcessesByNameAsync(string executableName);

        /// <summary>
        /// Start monitoring for process events
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stop monitoring for process events
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Set processor affinity for a process
        /// </summary>
        Task SetProcessorAffinityAsync(ProcessModel process, long affinityMask);

        /// <summary>
        /// Set priority for a process
        /// </summary>
        Task SetProcessPriorityAsync(ProcessModel process, ProcessPriorityClass priority);

        /// <summary>
        /// Refresh process information
        /// </summary>
        Task RefreshProcessInfoAsync(ProcessModel process);
    }

    /// <summary>
    /// Event args for process events
    /// </summary>
    public class ProcessEventArgs : EventArgs
    {
        public ProcessModel Process { get; }
        public DateTime Timestamp { get; }

        public ProcessEventArgs(ProcessModel process)
        {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Event args for monitoring status changes
    /// </summary>
    public class MonitoringStatusChangedEventArgs : EventArgs
    {
        public bool IsActive { get; }
        public string? Reason { get; }

        public MonitoringStatusChangedEventArgs(bool isActive, string? reason = null)
        {
            IsActive = isActive;
            Reason = reason;
        }
    }
}
