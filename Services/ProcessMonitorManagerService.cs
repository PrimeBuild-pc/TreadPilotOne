using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Main orchestration service that coordinates process monitoring and power plan management
    /// </summary>
    public class ProcessMonitorManagerService : IProcessMonitorManagerService
    {
        private readonly IProcessMonitorService _processMonitorService;
        private readonly IProcessPowerPlanAssociationService _associationService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly IGameBoostService _gameBoostService;
        private readonly INotificationService _notificationService;
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILogger<ProcessMonitorManagerService> _logger;
        private readonly IEnhancedLoggingService _enhancedLogger;
        private readonly object _lockObject = new();
        
        private readonly ConcurrentDictionary<int, ProcessModel> _runningAssociatedProcesses = new();
        private readonly System.Threading.Timer _delayTimer;
        private readonly SemaphoreSlim _powerPlanChangeSemaphore = new(1, 1);
        
        private bool _isRunning;
        private string _status = "Stopped";
        private bool _disposed;
        private ProcessMonitorConfiguration? _configuration;

        public event EventHandler<ProcessPowerPlanChangeEventArgs>? ProcessPowerPlanChanged;
        public event EventHandler<ServiceStatusEventArgs>? ServiceStatusChanged;

        public bool IsRunning => _isRunning;
        public string Status => _status;
        public IEnumerable<ProcessModel> RunningAssociatedProcesses => _runningAssociatedProcesses.Values.ToList();

        public ProcessMonitorManagerService(
            IProcessMonitorService processMonitorService,
            IProcessPowerPlanAssociationService associationService,
            IPowerPlanService powerPlanService,
            IGameBoostService gameBoostService,
            INotificationService notificationService,
            IApplicationSettingsService settingsService,
            ILogger<ProcessMonitorManagerService> logger,
            IEnhancedLoggingService enhancedLogger)
        {
            _processMonitorService = processMonitorService ?? throw new ArgumentNullException(nameof(processMonitorService));
            _associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _gameBoostService = gameBoostService ?? throw new ArgumentNullException(nameof(gameBoostService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _enhancedLogger = enhancedLogger ?? throw new ArgumentNullException(nameof(enhancedLogger));

            // Initialize delay timer (used for delayed power plan changes)
            _delayTimer = new System.Threading.Timer(DelayedPowerPlanChangeCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Subscribe to events
            _processMonitorService.ProcessStarted += OnProcessStarted;
            _processMonitorService.ProcessStopped += OnProcessStopped;
            _processMonitorService.MonitoringStatusChanged += OnMonitoringStatusChanged;
            _associationService.ConfigurationChanged += OnConfigurationChanged;
        }

        public async Task StartAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessMonitorManagerService));
            
            if (_isRunning) return;

            try
            {
                _logger.LogInformation("Starting Process Monitor Manager Service");
                await _enhancedLogger.LogSystemEventAsync(LogEventTypes.System.ServiceStarted,
                    "Process Monitor Manager Service starting");
                SetStatus(true, "Starting...");

                // Load configuration
                await _associationService.LoadConfigurationAsync();
                _configuration = _associationService.Configuration;
                _logger.LogInformation("Configuration loaded with {AssociationCount} associations",
                    _configuration.Associations.Count);

                await _enhancedLogger.LogSystemEventAsync(LogEventTypes.System.ConfigurationLoaded,
                    $"Process monitoring configuration loaded with {_configuration.Associations.Count} associations");

                // Start process monitoring
                await _processMonitorService.StartMonitoringAsync();
                _logger.LogInformation("Process monitoring started");

                await _enhancedLogger.LogProcessMonitoringEventAsync(LogEventTypes.ProcessMonitoring.MonitoringStarted,
                    "ProcessMonitorService", 0, "WMI-based process monitoring started");

                // Evaluate current processes
                await EvaluateCurrentProcessesAsync();

                _isRunning = true;
                SetStatus(true, "Running");
                _logger.LogInformation("Process Monitor Manager Service started successfully");

                await _enhancedLogger.LogSystemEventAsync(LogEventTypes.System.ServiceStarted,
                    "Process Monitor Manager Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Process Monitor Manager Service");
                await _enhancedLogger.LogErrorAsync(ex, "ProcessMonitorManagerService.StartAsync",
                    new Dictionary<string, object> { ["ServiceName"] = "ProcessMonitorManagerService" });
                _isRunning = false;
                SetStatus(false, "Failed to start", $"Error: {ex.Message}", ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            try
            {
                SetStatus(false, "Stopping...");

                // Stop process monitoring
                await _processMonitorService.StopMonitoringAsync();

                // Clear running processes
                _runningAssociatedProcesses.Clear();

                // Restore default power plan if configured
                if (_configuration?.DefaultPowerPlanGuid != null)
                {
                    await ForceDefaultPowerPlanAsync();
                }

                _isRunning = false;
                SetStatus(false, "Stopped");
            }
            catch (Exception ex)
            {
                SetStatus(false, "Error stopping", $"Error: {ex.Message}", ex);
                throw;
            }
        }

        public async Task EvaluateCurrentProcessesAsync()
        {
            if (!_isRunning || _configuration == null) return;

            try
            {
                var currentProcesses = await _processMonitorService.GetRunningProcessesAsync();
                var associatedProcesses = new List<ProcessModel>();

                // Find all currently running processes that have associations
                foreach (var process in currentProcesses)
                {
                    var association = _configuration.FindMatchingAssociation(process);
                    if (association != null)
                    {
                        associatedProcesses.Add(process);
                        _runningAssociatedProcesses.TryAdd(process.ProcessId, process);
                    }
                }

                // Determine which power plan should be active
                await DeterminePowerPlanAsync(associatedProcesses);
            }
            catch (Exception ex)
            {
                SetStatus(_isRunning, "Error evaluating processes", $"Error: {ex.Message}", ex);
            }
        }

        public async Task ForceDefaultPowerPlanAsync()
        {
            if (_configuration?.DefaultPowerPlanGuid == null) return;

            try
            {
                await _powerPlanChangeSemaphore.WaitAsync();
                
                var currentPowerPlan = await _powerPlanService.GetActivePowerPlan();
                var success = await _powerPlanService.SetActivePowerPlanByGuidAsync(
                    _configuration.DefaultPowerPlanGuid, 
                    _configuration.PreventDuplicatePowerPlanChanges);

                if (success)
                {
                    var newPowerPlan = await _powerPlanService.GetPowerPlanByGuidAsync(_configuration.DefaultPowerPlanGuid);
                    // Note: We don't have a specific process for this event, so we'll use a dummy one
                    var dummyProcess = new ProcessModel { Name = "System", ProcessId = -1 };
                    var dummyAssociation = new ProcessPowerPlanAssociation("System", _configuration.DefaultPowerPlanGuid, _configuration.DefaultPowerPlanName);

                    ProcessPowerPlanChanged?.Invoke(this, new ProcessPowerPlanChangeEventArgs(
                        dummyProcess, dummyAssociation, currentPowerPlan, newPowerPlan, "DefaultRestored"));

                    // Show notification for default power plan restoration
                    await _notificationService.ShowPowerPlanChangeNotificationAsync(
                        currentPowerPlan?.Name ?? "Unknown",
                        newPowerPlan?.Name ?? _configuration.DefaultPowerPlanName,
                        "");
                }
            }
            catch (Exception ex)
            {
                SetStatus(_isRunning, "Error setting default power plan", $"Error: {ex.Message}", ex);
            }
            finally
            {
                _powerPlanChangeSemaphore.Release();
            }
        }

        public async Task<PowerPlanModel?> GetCurrentActivePowerPlanAsync()
        {
            return await _powerPlanService.GetActivePowerPlan();
        }

        public async Task RefreshConfigurationAsync()
        {
            await _associationService.LoadConfigurationAsync();
            _configuration = _associationService.Configuration;
            
            if (_isRunning)
            {
                await EvaluateCurrentProcessesAsync();
            }
        }

        private async void OnProcessStarted(object? sender, ProcessEventArgs e)
        {
            if (!_isRunning || _configuration == null) return;

            try
            {
                await _enhancedLogger.LogProcessMonitoringEventAsync(LogEventTypes.ProcessMonitoring.Started,
                    e.Process.Name, e.Process.ProcessId, "Process started and detected by monitoring");

                // Check for Game Boost first
                await CheckForGameBoostAsync(e.Process);

                var association = _configuration.FindMatchingAssociation(e.Process);
                if (association != null)
                {
                    _runningAssociatedProcesses.TryAdd(e.Process.ProcessId, e.Process);

                    await _enhancedLogger.LogProcessMonitoringEventAsync(LogEventTypes.ProcessMonitoring.AssociationTriggered,
                        e.Process.Name, e.Process.ProcessId,
                        $"Process matched association for power plan: {association.PowerPlanName}");

                    // Schedule power plan change with delay if configured
                    if (_configuration.PowerPlanChangeDelayMs > 0)
                    {
                        _delayTimer.Change(_configuration.PowerPlanChangeDelayMs, Timeout.Infinite);

                        await _enhancedLogger.LogSystemEventAsync(LogEventTypes.System.ConfigurationLoaded,
                            $"Power plan change scheduled with {_configuration.PowerPlanChangeDelayMs}ms delay for process {e.Process.Name}");
                    }
                    else
                    {
                        await ChangePowerPlanForProcess(e.Process, association, "ProcessStarted");
                    }
                }
            }
            catch (Exception ex)
            {
                await _enhancedLogger.LogErrorAsync(ex, "ProcessMonitorManagerService.OnProcessStarted",
                    new Dictionary<string, object>
                    {
                        ["ProcessName"] = e.Process.Name,
                        ["ProcessId"] = e.Process.ProcessId
                    });
                SetStatus(_isRunning, "Error handling process start", $"Error: {ex.Message}", ex);
            }
        }

        private async void OnProcessStopped(object? sender, ProcessEventArgs e)
        {
            if (!_isRunning || _configuration == null) return;

            try
            {
                // Check if this was a game process and deactivate Game Boost if needed
                await CheckForGameBoostDeactivationAsync(e.Process);

                if (_runningAssociatedProcesses.TryRemove(e.Process.ProcessId, out var removedProcess))
                {
                    // Check if there are any other associated processes still running
                    var remainingProcesses = _runningAssociatedProcesses.Values.ToList();
                    await DeterminePowerPlanAsync(remainingProcesses);
                }
            }
            catch (Exception ex)
            {
                SetStatus(_isRunning, "Error handling process stop", $"Error: {ex.Message}", ex);
            }
        }

        private void OnMonitoringStatusChanged(object? sender, MonitoringStatusEventArgs e)
        {
            var details = e.StatusMessage ?? (e.IsMonitoring ? "Monitoring active" : "Monitoring inactive");
            SetStatus(_isRunning, $"Monitor: {details}", e.StatusMessage, e.Error);
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            _configuration = _associationService.Configuration;
            
            if (_isRunning)
            {
                Task.Run(async () => await EvaluateCurrentProcessesAsync());
            }
        }

        private async void DelayedPowerPlanChangeCallback(object? state)
        {
            if (!_isRunning) return;
            
            var runningProcesses = _runningAssociatedProcesses.Values.ToList();
            await DeterminePowerPlanAsync(runningProcesses);
        }

        private async Task DeterminePowerPlanAsync(IList<ProcessModel> associatedProcesses)
        {
            if (_configuration == null) return;

            try
            {
                if (associatedProcesses.Any())
                {
                    // Find the highest priority association among running processes
                    var associations = associatedProcesses
                        .Select(p => _configuration.FindMatchingAssociation(p))
                        .Where(a => a != null)
                        .OrderByDescending(a => a!.Priority)
                        .ToList();

                    if (associations.Any())
                    {
                        var topAssociation = associations.First()!;
                        var matchingProcess = associatedProcesses.First(p => topAssociation.MatchesProcess(p));
                        await ChangePowerPlanForProcess(matchingProcess, topAssociation, "ProcessStarted");
                    }
                }
                else
                {
                    // No associated processes running, revert to default
                    if (!string.IsNullOrEmpty(_configuration.DefaultPowerPlanGuid))
                    {
                        await ForceDefaultPowerPlanAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus(_isRunning, "Error determining power plan", $"Error: {ex.Message}", ex);
            }
        }

        private async Task ChangePowerPlanForProcess(ProcessModel process, ProcessPowerPlanAssociation association, string action)
        {
            try
            {
                await _powerPlanChangeSemaphore.WaitAsync();
                
                var currentPowerPlan = await _powerPlanService.GetActivePowerPlan();
                var success = await _powerPlanService.SetActivePowerPlanByGuidAsync(
                    association.PowerPlanGuid, 
                    _configuration?.PreventDuplicatePowerPlanChanges ?? true);

                if (success)
                {
                    var newPowerPlan = await _powerPlanService.GetPowerPlanByGuidAsync(association.PowerPlanGuid);
                    ProcessPowerPlanChanged?.Invoke(this, new ProcessPowerPlanChangeEventArgs(
                        process, association, currentPowerPlan, newPowerPlan, action));

                    // Show notification for power plan change
                    await _notificationService.ShowPowerPlanChangeNotificationAsync(
                        currentPowerPlan?.Name ?? "Unknown",
                        newPowerPlan?.Name ?? association.PowerPlanName,
                        process.Name);
                }
            }
            catch (Exception ex)
            {
                SetStatus(_isRunning, "Error changing power plan", $"Error: {ex.Message}", ex);
            }
            finally
            {
                _powerPlanChangeSemaphore.Release();
            }
        }

        private void SetStatus(bool isRunning, string status, string? details = null, Exception? error = null)
        {
            lock (_lockObject)
            {
                _status = status;
            }

            ServiceStatusChanged?.Invoke(this, new ServiceStatusEventArgs(isRunning, status, details, error));

            // Show error notification if there's an error
            if (error != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _notificationService.ShowErrorNotificationAsync(
                            "Process Monitor Error",
                            details ?? status,
                            error);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to show error notification");
                    }
                });
            }
        }

        /// <summary>
        /// Checks if a process is a game and activates Game Boost if enabled
        /// </summary>
        private async Task CheckForGameBoostAsync(ProcessModel process)
        {
            try
            {
                if (_gameBoostService.IsGameProcess(process))
                {
                    _logger.LogInformation("Game detected: {ProcessName} (PID: {ProcessId})", process.Name, process.ProcessId);

                    // Activate Game Boost if enabled and not already active for this process
                    if (!_gameBoostService.IsGameBoostActive || _gameBoostService.CurrentGameProcess?.ProcessId != process.ProcessId)
                    {
                        await _gameBoostService.ActivateGameBoostAsync(process);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for Game Boost activation for process {ProcessName}", process.Name);
            }
        }

        /// <summary>
        /// Checks if a stopped process was the current game and deactivates Game Boost if needed
        /// </summary>
        private async Task CheckForGameBoostDeactivationAsync(ProcessModel process)
        {
            try
            {
                if (_gameBoostService.IsGameBoostActive &&
                    _gameBoostService.CurrentGameProcess?.ProcessId == process.ProcessId)
                {
                    _logger.LogInformation("Game process stopped: {ProcessName} (PID: {ProcessId})", process.Name, process.ProcessId);
                    await _gameBoostService.DeactivateGameBoostAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for Game Boost deactivation for process {ProcessName}", process.Name);
            }
        }

        public void UpdateSettings()
        {
            // Update the process monitor service with new settings
            _processMonitorService.UpdateSettings();

            _logger.LogDebug("ProcessMonitorManagerService settings updated");
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopAsync().Wait(5000); // Wait up to 5 seconds for clean shutdown

            _delayTimer?.Dispose();
            _powerPlanChangeSemaphore?.Dispose();
            _processMonitorService?.Dispose();

            _disposed = true;
        }
    }
}
