using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Process monitoring service using WMI events with fallback polling
    /// </summary>
    public class ProcessMonitorService : IProcessMonitorService
    {
        private readonly IProcessService _processService;
        private readonly IApplicationSettingsService _settingsService;
        private readonly object _lockObject = new();
        private readonly ConcurrentDictionary<int, ProcessModel> _runningProcesses = new();

        private ManagementEventWatcher? _processStartWatcher;
        private ManagementEventWatcher? _processStopWatcher;
        private System.Threading.Timer? _fallbackTimer;
        private CancellationTokenSource? _cancellationTokenSource;

        private bool _isMonitoring;
        private bool _isWmiAvailable;
        private bool _isFallbackPollingActive;
        private bool _disposed;

        // Configuration - will be updated from settings
        private int _fallbackPollingIntervalMs = 5000; // Default 5 seconds
        private readonly int _wmiRetryDelayMs = 10000; // 10 seconds

        public event EventHandler<ProcessEventArgs>? ProcessStarted;
        public event EventHandler<ProcessEventArgs>? ProcessStopped;
        public event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged;

        public bool IsMonitoring => _isMonitoring;
        public bool IsWmiAvailable => _isWmiAvailable;
        public bool IsFallbackPollingActive => _isFallbackPollingActive;

        public ProcessMonitorService(IProcessService processService, IApplicationSettingsService settingsService)
        {
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize polling interval from settings
            UpdatePollingInterval();
        }

        public async Task StartMonitoringAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessMonitorService));
            
            lock (_lockObject)
            {
                if (_isMonitoring) return;
                _isMonitoring = true;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize current process list
            await InitializeProcessListAsync();

            // Try to start WMI monitoring first
            var wmiStarted = await TryStartWmiMonitoringAsync();
            
            if (!wmiStarted)
            {
                // Fall back to polling if WMI is not available
                StartFallbackPolling();
            }

            OnMonitoringStatusChanged();
        }

        public async Task StopMonitoringAsync()
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                if (!_isMonitoring) return;
                _isMonitoring = false;
            }

            // Stop WMI watchers
            StopWmiWatchers();

            // Stop fallback polling
            StopFallbackPolling();

            // Cancel any ongoing operations
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _runningProcesses.Clear();
            OnMonitoringStatusChanged();

            await Task.CompletedTask;
        }

        public async Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync()
        {
            try
            {
                var processes = await _processService.GetProcessesAsync();
                return processes;
            }
            catch (Exception)
            {
                return Enumerable.Empty<ProcessModel>();
            }
        }

        public async Task<bool> IsProcessRunningAsync(string executableName)
        {
            try
            {
                var processes = await GetRunningProcessesAsync();
                return processes.Any(p => string.Equals(p.Name, executableName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private async Task InitializeProcessListAsync()
        {
            try
            {
                var processes = await GetRunningProcessesAsync();
                _runningProcesses.Clear();
                
                foreach (var process in processes)
                {
                    _runningProcesses.TryAdd(process.ProcessId, process);
                }
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Failed to initialize process list: {ex.Message}", ex);
            }
        }

        private async Task<bool> TryStartWmiMonitoringAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Create WMI event watchers for process start and stop
                    var startQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
                    var stopQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace");

                    _processStartWatcher = new ManagementEventWatcher(startQuery);
                    _processStopWatcher = new ManagementEventWatcher(stopQuery);

                    _processStartWatcher.EventArrived += OnProcessStarted;
                    _processStopWatcher.EventArrived += OnProcessStopped;

                    _processStartWatcher.Start();
                    _processStopWatcher.Start();
                });

                _isWmiAvailable = true;
                OnMonitoringStatusChanged("WMI monitoring started successfully");
                return true;
            }
            catch (Exception ex)
            {
                _isWmiAvailable = false;
                OnMonitoringStatusChanged($"WMI monitoring failed: {ex.Message}", ex);
                
                // Clean up any partially created watchers
                StopWmiWatchers();
                
                return false;
            }
        }

        private void StartFallbackPolling()
        {
            // Update polling interval from current settings
            UpdatePollingInterval();

            _isFallbackPollingActive = true;
            _fallbackTimer = new System.Threading.Timer(FallbackPollingCallback, null, 0, _fallbackPollingIntervalMs);
            OnMonitoringStatusChanged($"Fallback polling started (interval: {_fallbackPollingIntervalMs}ms)");
        }

        private void StopWmiWatchers()
        {
            try
            {
                _processStartWatcher?.Stop();
                _processStartWatcher?.Dispose();
                _processStartWatcher = null;

                _processStopWatcher?.Stop();
                _processStopWatcher?.Dispose();
                _processStopWatcher = null;

                _isWmiAvailable = false;
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error stopping WMI watchers: {ex.Message}", ex);
            }
        }

        private void StopFallbackPolling()
        {
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
            _isFallbackPollingActive = false;
        }

        private async void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
                var processName = e.NewEvent["ProcessName"]?.ToString() ?? string.Empty;

                // Get detailed process information
                var process = await CreateProcessModelFromId(processId, processName);
                if (process != null)
                {
                    _runningProcesses.TryAdd(processId, process);
                    ProcessStarted?.Invoke(this, new ProcessEventArgs(process));
                }
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error handling process start event: {ex.Message}", ex);
            }
        }

        private void OnProcessStopped(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
                
                if (_runningProcesses.TryRemove(processId, out var process))
                {
                    ProcessStopped?.Invoke(this, new ProcessEventArgs(process));
                }
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error handling process stop event: {ex.Message}", ex);
            }
        }

        private async void FallbackPollingCallback(object? state)
        {
            if (!_isMonitoring || _cancellationTokenSource?.Token.IsCancellationRequested == true)
                return;

            try
            {
                var currentProcesses = await GetRunningProcessesAsync();
                var currentProcessDict = currentProcesses.ToDictionary(p => p.ProcessId, p => p);

                // Check for new processes
                foreach (var process in currentProcesses)
                {
                    if (!_runningProcesses.ContainsKey(process.ProcessId))
                    {
                        _runningProcesses.TryAdd(process.ProcessId, process);
                        ProcessStarted?.Invoke(this, new ProcessEventArgs(process));
                    }
                }

                // Check for stopped processes
                var stoppedProcesses = _runningProcesses.Keys
                    .Where(pid => !currentProcessDict.ContainsKey(pid))
                    .ToList();

                foreach (var pid in stoppedProcesses)
                {
                    if (_runningProcesses.TryRemove(pid, out var stoppedProcess))
                    {
                        ProcessStopped?.Invoke(this, new ProcessEventArgs(stoppedProcess));
                    }
                }
            }
            catch (Exception ex)
            {
                OnMonitoringStatusChanged($"Error in fallback polling: {ex.Message}", ex);
            }
        }

        private async Task<ProcessModel?> CreateProcessModelFromId(int processId, string processName)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                var model = new ProcessModel { Process = process };
                return model;
            }
            catch
            {
                // Process may have already terminated
                return null;
            }
        }

        private void OnMonitoringStatusChanged(string? message = null, Exception? error = null)
        {
            MonitoringStatusChanged?.Invoke(this, new MonitoringStatusEventArgs(
                _isMonitoring, _isWmiAvailable, _isFallbackPollingActive, message, error));
        }

        private void UpdatePollingInterval()
        {
            var settings = _settingsService.Settings;
            _fallbackPollingIntervalMs = settings.FallbackPollingIntervalMs;
        }

        public void UpdateSettings()
        {
            UpdatePollingInterval();

            // If fallback polling is active, restart it with new interval
            if (_isFallbackPollingActive && _fallbackTimer != null)
            {
                _fallbackTimer.Change(0, _fallbackPollingIntervalMs);
                OnMonitoringStatusChanged($"Polling interval updated to {_fallbackPollingIntervalMs}ms");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopMonitoringAsync().Wait(5000); // Wait up to 5 seconds for clean shutdown

            _disposed = true;
        }
    }
}
