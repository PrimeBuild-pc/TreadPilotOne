using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the performance monitoring dashboard
    /// </summary>
    public partial class PerformanceViewModel : BaseViewModel
    {
        private readonly IPerformanceMonitoringService _performanceService;
        private readonly ILogger<PerformanceViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<CpuCoreUsage> coreUsages = new();

        [ObservableProperty]
        private double totalCpuUsage;

        [ObservableProperty]
        private long totalMemoryUsage;

        [ObservableProperty]
        private long availableMemory;

        [ObservableProperty]
        private long totalMemory;

        [ObservableProperty]
        private double memoryUsagePercentage;

        [ObservableProperty]
        private ProcessPerformanceInfo? topCpuProcess;

        [ObservableProperty]
        private ProcessPerformanceInfo? topMemoryProcess;

        [ObservableProperty]
        private ObservableCollection<ProcessPerformanceInfo> topCpuProcesses = new();

        [ObservableProperty]
        private ObservableCollection<ProcessPerformanceInfo> topMemoryProcesses = new();

        [ObservableProperty]
        private int activeProcessCount;

        [ObservableProperty]
        private bool isMonitoring;

        [ObservableProperty]
        private string monitoringStatusText = "Monitoring Stopped";

        [ObservableProperty]
        private DateTime lastUpdateTime;

        [ObservableProperty]
        private ObservableCollection<SystemPerformanceMetrics> historicalData = new();

        [ObservableProperty]
        private int updateInterval = 1000; // milliseconds

        [ObservableProperty]
        private bool showCoreDetails = true;

        [ObservableProperty]
        private bool showProcessDetails = true;

        [ObservableProperty]
        private string cpuUsageText = "0.0%";

        [ObservableProperty]
        private string memoryUsageText = "0 MB / 0 MB";

        [ObservableProperty]
        private string uptimeText = "00:00:00";

        public PerformanceViewModel(
            IPerformanceMonitoringService performanceService,
            ILogger<PerformanceViewModel> logger) : base(logger, null)
        {
            _performanceService = performanceService;
            _logger = logger;

            // Subscribe to performance updates
            _performanceService.MetricsUpdated += OnMetricsUpdated;
        }

        [RelayCommand]
        public async Task StartMonitoringAsync()
        {
            try
            {
                SetStatus("Starting performance monitoring...");
                await _performanceService.StartMonitoringAsync();
                IsMonitoring = true;
                MonitoringStatusText = "Monitoring Active";
                SetStatus("Performance monitoring started");
                _logger.LogInformation("Performance monitoring started");
            }
            catch (Exception ex)
            {
                SetError("Failed to start performance monitoring", ex);
                _logger.LogError(ex, "Error starting performance monitoring");
            }
        }

        [RelayCommand]
        public async Task StopMonitoringAsync()
        {
            try
            {
                SetStatus("Stopping performance monitoring...");
                await _performanceService.StopMonitoringAsync();
                IsMonitoring = false;
                MonitoringStatusText = "Monitoring Stopped";
                SetStatus("Performance monitoring stopped");
                _logger.LogInformation("Performance monitoring stopped");
            }
            catch (Exception ex)
            {
                SetError("Failed to stop performance monitoring", ex);
                _logger.LogError(ex, "Error stopping performance monitoring");
            }
        }

        [RelayCommand]
        public async Task RefreshMetricsAsync()
        {
            try
            {
                SetStatus("Refreshing performance metrics...");
                var metrics = await _performanceService.GetSystemMetricsAsync();
                UpdateMetrics(metrics);
                SetStatus("Performance metrics refreshed");
            }
            catch (Exception ex)
            {
                SetError("Failed to refresh performance metrics", ex);
                _logger.LogError(ex, "Error refreshing performance metrics");
            }
        }

        [RelayCommand]
        public async Task ClearHistoricalDataAsync()
        {
            try
            {
                await _performanceService.ClearHistoricalDataAsync();
                HistoricalData.Clear();
                SetStatus("Historical data cleared");
                _logger.LogInformation("Historical performance data cleared");
            }
            catch (Exception ex)
            {
                SetError("Failed to clear historical data", ex);
                _logger.LogError(ex, "Error clearing historical data");
            }
        }

        [RelayCommand]
        public async Task LoadHistoricalDataAsync()
        {
            try
            {
                SetStatus("Loading historical data...");
                var data = await _performanceService.GetHistoricalDataAsync(TimeSpan.FromHours(1));
                HistoricalData = new ObservableCollection<SystemPerformanceMetrics>(data);
                SetStatus($"Loaded {data.Count} historical data points");
            }
            catch (Exception ex)
            {
                SetError("Failed to load historical data", ex);
                _logger.LogError(ex, "Error loading historical data");
            }
        }

        [RelayCommand]
        public void ToggleCoreDetails()
        {
            ShowCoreDetails = !ShowCoreDetails;
        }

        [RelayCommand]
        public void ToggleProcessDetails()
        {
            ShowProcessDetails = !ShowProcessDetails;
        }

        private void OnMetricsUpdated(object? sender, PerformanceMetricsUpdatedEventArgs e)
        {
            try
            {
                // Update on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateMetrics(e.Metrics);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating performance metrics in UI");
            }
        }

        private void UpdateMetrics(SystemPerformanceMetrics metrics)
        {
            try
            {
                TotalCpuUsage = metrics.TotalCpuUsage;
                TotalMemoryUsage = metrics.TotalMemoryUsage;
                AvailableMemory = metrics.AvailableMemory;
                TotalMemory = metrics.TotalMemory;
                MemoryUsagePercentage = metrics.MemoryUsagePercentage;
                TopCpuProcess = metrics.TopCpuProcess;
                TopMemoryProcess = metrics.TopMemoryProcess;
                ActiveProcessCount = metrics.ActiveProcessCount;
                LastUpdateTime = metrics.Timestamp;

                // Update core usages
                CoreUsages.Clear();
                foreach (var coreUsage in metrics.CpuCoreUsages)
                {
                    CoreUsages.Add(coreUsage);
                }

                // Update formatted text
                CpuUsageText = $"{TotalCpuUsage:F1}%";
                MemoryUsageText = $"{TotalMemoryUsage / (1024 * 1024):N0} MB / {TotalMemory / (1024 * 1024):N0} MB";
                UptimeText = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime).ToString(@"hh\:mm\:ss");

                // Add to historical data if monitoring
                if (IsMonitoring)
                {
                    HistoricalData.Add(metrics);
                    
                    // Keep only last 300 entries (5 minutes at 1-second intervals)
                    while (HistoricalData.Count > 300)
                    {
                        HistoricalData.RemoveAt(0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics display");
            }
        }

        public override async Task InitializeAsync()
        {
            try
            {
                SetStatus("Initializing performance monitoring...");
                
                // Load initial metrics
                await RefreshMetricsAsync();
                
                // Load top processes
                await LoadTopProcessesAsync();
                
                SetStatus("Performance monitoring initialized");
            }
            catch (Exception ex)
            {
                SetError("Failed to initialize performance monitoring", ex);
                _logger.LogError(ex, "Error initializing performance monitoring");
            }
        }

        private async Task LoadTopProcessesAsync()
        {
            try
            {
                var topCpu = await _performanceService.GetTopCpuProcessesAsync(10);
                var topMemory = await _performanceService.GetTopMemoryProcessesAsync(10);

                TopCpuProcesses = new ObservableCollection<ProcessPerformanceInfo>(topCpu);
                TopMemoryProcesses = new ObservableCollection<ProcessPerformanceInfo>(topMemory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading top processes");
            }
        }

        protected override void OnDispose()
        {
            _performanceService.MetricsUpdated -= OnMetricsUpdated;

            // Stop monitoring if active
            if (IsMonitoring)
            {
                _ = Task.Run(async () => await StopMonitoringAsync());
            }

            base.OnDispose();
        }
    }
}
