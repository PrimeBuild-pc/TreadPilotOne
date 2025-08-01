using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for real-time performance monitoring
    /// </summary>
    public class PerformanceMonitoringService : IPerformanceMonitoringService, IDisposable
    {
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly IProcessService _processService;
        private readonly ICpuTopologyService _cpuTopologyService;
        private readonly List<SystemPerformanceMetrics> _historicalData;
        private readonly PerformanceCounter _totalCpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly List<PerformanceCounter> _cpuCoreCounters;
        private System.Threading.Timer? _monitoringTimer;
        private bool _isMonitoring;
        private bool _disposed;

        public event EventHandler<PerformanceMetricsUpdatedEventArgs>? MetricsUpdated;

        public PerformanceMonitoringService(
            ILogger<PerformanceMonitoringService> logger,
            IProcessService processService,
            ICpuTopologyService cpuTopologyService)
        {
            _logger = logger;
            _processService = processService;
            _cpuTopologyService = cpuTopologyService;
            _historicalData = new List<SystemPerformanceMetrics>();
            
            // Initialize performance counters
            _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            _cpuCoreCounters = new List<PerformanceCounter>();

            InitializeCpuCoreCounters();
        }

        public async Task<SystemPerformanceMetrics> GetSystemMetricsAsync()
        {
            try
            {
                var metrics = new SystemPerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    TotalCpuUsage = await GetTotalCpuUsageAsync(),
                    CpuCoreUsages = await GetCpuCoreUsageAsync(),
                    TotalMemoryUsage = await GetTotalMemoryUsageAsync(),
                    AvailableMemory = await GetAvailableMemoryAsync(),
                    ActiveProcessCount = Process.GetProcesses().Length
                };

                // Calculate memory percentage
                metrics.TotalMemory = await GetTotalPhysicalMemoryAsync();
                metrics.MemoryUsagePercentage = metrics.TotalMemory > 0 
                    ? ((double)(metrics.TotalMemory - metrics.AvailableMemory) / metrics.TotalMemory) * 100 
                    : 0;

                // Get top processes
                var topCpuProcesses = await GetTopCpuProcessesAsync(1);
                metrics.TopCpuProcess = topCpuProcesses.FirstOrDefault();

                var topMemoryProcesses = await GetTopMemoryProcessesAsync(1);
                metrics.TopMemoryProcess = topMemoryProcesses.FirstOrDefault();

                // Store in historical data
                _historicalData.Add(metrics);
                
                // Keep only last 1000 entries (about 16 minutes at 1-second intervals)
                if (_historicalData.Count > 1000)
                {
                    _historicalData.RemoveAt(0);
                }

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system metrics");
                return new SystemPerformanceMetrics();
            }
        }

        public async Task<List<CpuCoreUsage>> GetCpuCoreUsageAsync()
        {
            var coreUsages = new List<CpuCoreUsage>();

            try
            {
                var topology = await _cpuTopologyService.DetectTopologyAsync();
                
                for (int i = 0; i < _cpuCoreCounters.Count; i++)
                {
                    var counter = _cpuCoreCounters[i];
                    var usage = counter.NextValue();

                    var coreUsage = new CpuCoreUsage
                    {
                        CoreId = i,
                        CoreName = $"Core {i}",
                        Usage = usage,
                        CoreType = DetermineCoreType(i, topology),
                        IsHyperThreaded = IsHyperThreadedCore(i, topology),
                        PhysicalCoreId = GetPhysicalCoreId(i, topology)
                    };

                    coreUsages.Add(coreUsage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CPU core usage");
            }

            return coreUsages;
        }

        public async Task<MemoryUsageInfo> GetMemoryUsageAsync()
        {
            try
            {
                var memoryInfo = new MemoryUsageInfo();

                // Get physical memory info
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    memoryInfo.TotalPhysicalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                }

                // Get available memory
                memoryInfo.AvailablePhysicalMemory = (long)_memoryCounter.NextValue() * 1024 * 1024; // Convert MB to bytes
                memoryInfo.UsedPhysicalMemory = memoryInfo.TotalPhysicalMemory - memoryInfo.AvailablePhysicalMemory;
                memoryInfo.PhysicalMemoryUsagePercentage = memoryInfo.TotalPhysicalMemory > 0
                    ? ((double)memoryInfo.UsedPhysicalMemory / memoryInfo.TotalPhysicalMemory) * 100
                    : 0;

                // Get virtual memory info
                using var memSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (var obj in memSearcher.Get())
                {
                    memoryInfo.TotalVirtualMemory = Convert.ToInt64(obj["TotalVirtualMemorySize"]) * 1024; // Convert KB to bytes
                    memoryInfo.AvailableVirtualMemory = Convert.ToInt64(obj["FreeVirtualMemory"]) * 1024;
                }

                memoryInfo.UsedVirtualMemory = memoryInfo.TotalVirtualMemory - memoryInfo.AvailableVirtualMemory;
                memoryInfo.VirtualMemoryUsagePercentage = memoryInfo.TotalVirtualMemory > 0
                    ? ((double)memoryInfo.UsedVirtualMemory / memoryInfo.TotalVirtualMemory) * 100
                    : 0;

                return memoryInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory usage");
                return new MemoryUsageInfo();
            }
        }

        public async Task<List<ProcessPerformanceInfo>> GetTopCpuProcessesAsync(int count = 10)
        {
            try
            {
                var processes = await _processService.GetProcessesAsync();
                return processes
                    .OrderByDescending(p => p.CpuUsage)
                    .Take(count)
                    .Select(p => new ProcessPerformanceInfo
                    {
                        ProcessId = p.ProcessId,
                        ProcessName = p.Name,
                        WindowTitle = p.MainWindowTitle,
                        CpuUsage = p.CpuUsage,
                        MemoryUsage = p.MemoryUsage,
                        ThreadCount = p.Process?.Threads?.Count ?? 0,
                        ExecutablePath = p.ExecutablePath ?? "",
                        Priority = p.Priority.ToString()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top CPU processes");
                return new List<ProcessPerformanceInfo>();
            }
        }

        public async Task<List<ProcessPerformanceInfo>> GetTopMemoryProcessesAsync(int count = 10)
        {
            try
            {
                var processes = await _processService.GetProcessesAsync();
                return processes
                    .OrderByDescending(p => p.MemoryUsage)
                    .Take(count)
                    .Select(p => new ProcessPerformanceInfo
                    {
                        ProcessId = p.ProcessId,
                        ProcessName = p.Name,
                        WindowTitle = p.MainWindowTitle,
                        CpuUsage = p.CpuUsage,
                        MemoryUsage = p.MemoryUsage,
                        ThreadCount = p.Process?.Threads?.Count ?? 0,
                        ExecutablePath = p.ExecutablePath ?? "",
                        Priority = p.Priority.ToString()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top memory processes");
                return new List<ProcessPerformanceInfo>();
            }
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring) return;

            _logger.LogInformation("Starting performance monitoring");
            _isMonitoring = true;

            // PERFORMANCE OPTIMIZATION: Increased interval from 1s to 2s for better performance
            _monitoringTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    var metrics = await GetSystemMetricsAsync();
                    MetricsUpdated?.Invoke(this, new PerformanceMetricsUpdatedEventArgs(metrics));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during performance monitoring update");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring) return;

            _logger.LogInformation("Stopping performance monitoring");
            _isMonitoring = false;

            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
        }

        public async Task<List<SystemPerformanceMetrics>> GetHistoricalDataAsync(TimeSpan duration)
        {
            var cutoffTime = DateTime.UtcNow - duration;
            return _historicalData.Where(m => m.Timestamp >= cutoffTime).ToList();
        }

        public async Task ClearHistoricalDataAsync()
        {
            _historicalData.Clear();
            _logger.LogInformation("Historical performance data cleared");
        }

        private void InitializeCpuCoreCounters()
        {
            try
            {
                var coreCount = Environment.ProcessorCount;
                for (int i = 0; i < coreCount; i++)
                {
                    var counter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                    _cpuCoreCounters.Add(counter);
                }

                _logger.LogInformation("Initialized {CoreCount} CPU core performance counters", coreCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing CPU core counters");
            }
        }

        private async Task<double> GetTotalCpuUsageAsync()
        {
            try
            {
                return _totalCpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total CPU usage");
                return 0;
            }
        }

        private async Task<long> GetTotalMemoryUsageAsync()
        {
            try
            {
                var totalMemory = await GetTotalPhysicalMemoryAsync();
                var availableMemory = await GetAvailableMemoryAsync();
                return totalMemory - availableMemory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total memory usage");
                return 0;
            }
        }

        private async Task<long> GetAvailableMemoryAsync()
        {
            try
            {
                return (long)_memoryCounter.NextValue() * 1024 * 1024; // Convert MB to bytes
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available memory");
                return 0;
            }
        }

        private async Task<long> GetTotalPhysicalMemoryAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    return Convert.ToInt64(obj["TotalPhysicalMemory"]);
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total physical memory");
                return 0;
            }
        }

        private static string DetermineCoreType(int coreId, CpuTopologyModel? topology)
        {
            if (topology?.HasIntelHybrid == true)
            {
                // Intel hybrid architecture
                if (coreId < topology.PerformanceCores.Count())
                    return "P-Core";
                else
                    return "E-Core";
            }

            return "Standard";
        }

        private static bool IsHyperThreadedCore(int coreId, CpuTopologyModel? topology)
        {
            if (topology?.HasHyperThreading != true) return false;

            // Simplified logic - in reality this would be more complex
            return coreId >= topology.TotalPhysicalCores;
        }

        private static int GetPhysicalCoreId(int coreId, CpuTopologyModel? topology)
        {
            if (topology?.HasHyperThreading == true)
            {
                return coreId / 2; // Simplified - assumes 2 threads per core
            }

            return coreId;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _monitoringTimer?.Dispose();
            _totalCpuCounter?.Dispose();
            _memoryCounter?.Dispose();

            foreach (var counter in _cpuCoreCounters)
            {
                counter?.Dispose();
            }

            _cpuCoreCounters.Clear();
            _disposed = true;
        }
    }
}
