using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for real-time performance monitoring
    /// </summary>
    public interface IPerformanceMonitoringService
    {
        /// <summary>
        /// Get current system performance metrics
        /// </summary>
        Task<SystemPerformanceMetrics> GetSystemMetricsAsync();

        /// <summary>
        /// Get per-core CPU usage
        /// </summary>
        Task<List<CpuCoreUsage>> GetCpuCoreUsageAsync();

        /// <summary>
        /// Get memory usage information
        /// </summary>
        Task<MemoryUsageInfo> GetMemoryUsageAsync();

        /// <summary>
        /// Get top CPU consuming processes
        /// </summary>
        Task<List<ProcessPerformanceInfo>> GetTopCpuProcessesAsync(int count = 10);

        /// <summary>
        /// Get top memory consuming processes
        /// </summary>
        Task<List<ProcessPerformanceInfo>> GetTopMemoryProcessesAsync(int count = 10);

        /// <summary>
        /// Start real-time monitoring
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stop real-time monitoring
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Event raised when performance metrics are updated
        /// </summary>
        event EventHandler<PerformanceMetricsUpdatedEventArgs>? MetricsUpdated;

        /// <summary>
        /// Get historical performance data
        /// </summary>
        Task<List<SystemPerformanceMetrics>> GetHistoricalDataAsync(TimeSpan duration);

        /// <summary>
        /// Clear historical performance data
        /// </summary>
        Task ClearHistoricalDataAsync();
    }

    /// <summary>
    /// System performance metrics
    /// </summary>
    public class SystemPerformanceMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double TotalCpuUsage { get; set; }
        public long TotalMemoryUsage { get; set; }
        public long AvailableMemory { get; set; }
        public long TotalMemory { get; set; }
        public double MemoryUsagePercentage { get; set; }
        public List<CpuCoreUsage> CpuCoreUsages { get; set; } = new();
        public ProcessPerformanceInfo? TopCpuProcess { get; set; }
        public ProcessPerformanceInfo? TopMemoryProcess { get; set; }
        public int ActiveProcessCount { get; set; }
        public int ThreadCount { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkUsage { get; set; }
    }

    /// <summary>
    /// CPU core usage information
    /// </summary>
    public class CpuCoreUsage
    {
        public int CoreId { get; set; }
        public string CoreName { get; set; } = string.Empty;
        public double Usage { get; set; }
        public string CoreType { get; set; } = "Unknown"; // P-Core, E-Core, etc.
        public bool IsHyperThreaded { get; set; }
        public int PhysicalCoreId { get; set; }
        public double Frequency { get; set; }
        public double Temperature { get; set; }
    }

    /// <summary>
    /// Memory usage information
    /// </summary>
    public class MemoryUsageInfo
    {
        public long TotalPhysicalMemory { get; set; }
        public long AvailablePhysicalMemory { get; set; }
        public long UsedPhysicalMemory { get; set; }
        public double PhysicalMemoryUsagePercentage { get; set; }
        public long TotalVirtualMemory { get; set; }
        public long AvailableVirtualMemory { get; set; }
        public long UsedVirtualMemory { get; set; }
        public double VirtualMemoryUsagePercentage { get; set; }
        public long PageFileSize { get; set; }
        public long PageFileUsage { get; set; }
        public double PageFileUsagePercentage { get; set; }
    }

    /// <summary>
    /// Process performance information
    /// </summary>
    public class ProcessPerformanceInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public long VirtualMemoryUsage { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan RunTime { get; set; }
        public string ExecutablePath { get; set; } = string.Empty;
        public bool IsResponding { get; set; }
        public string Priority { get; set; } = string.Empty;
        public IntPtr ProcessorAffinity { get; set; }
    }

    /// <summary>
    /// Event args for performance metrics updates
    /// </summary>
    public class PerformanceMetricsUpdatedEventArgs : EventArgs
    {
        public SystemPerformanceMetrics Metrics { get; }
        public DateTime UpdateTime { get; }

        public PerformanceMetricsUpdatedEventArgs(SystemPerformanceMetrics metrics)
        {
            Metrics = metrics;
            UpdateTime = DateTime.UtcNow;
        }
    }
}
