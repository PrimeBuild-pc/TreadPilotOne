using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the log viewer and management interface
    /// </summary>
    public partial class LogViewerViewModel : ObservableObject
    {
        private readonly IEnhancedLoggingService _loggingService;
        private readonly IApplicationSettingsService _settingsService;
        private readonly ILogger<LogViewerViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<LogEntryDisplayModel> _logEntries = new();

        [ObservableProperty]
        private LogEntryDisplayModel? _selectedLogEntry;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private LogLevel _selectedLogLevel = LogLevel.Information;

        [ObservableProperty]
        private string _selectedCategory = "All";

        [ObservableProperty]
        private DateTime _fromDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Today.AddDays(1);

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private LogFileStatistics? _logStatistics;

        [ObservableProperty]
        private bool _enableDebugLogging;

        [ObservableProperty]
        private int _maxLogFileSizeMb = 10;

        [ObservableProperty]
        private int _logRetentionDays = 7;

        [ObservableProperty]
        private bool _autoRefresh = true;

        [ObservableProperty]
        private int _refreshIntervalSeconds = 30;

        public ObservableCollection<string> AvailableCategories { get; } = new()
        {
            "All", "PowerPlan", "ProcessMonitoring", "GameBoost", "UserAction", "System", "Error", "Performance"
        };

        public ObservableCollection<LogLevel> AvailableLogLevels { get; } = new()
        {
            LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical
        };

        public ICommand RefreshLogsCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand ExportLogsCommand { get; }
        public ICommand CleanupOldLogsCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand OpenLogDirectoryCommand { get; }
        public ICommand CopyLogEntryCommand { get; }

        public LogViewerViewModel(
            IEnhancedLoggingService loggingService,
            IApplicationSettingsService settingsService,
            ILogger<LogViewerViewModel> logger)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize commands
            RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
            ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            CleanupOldLogsCommand = new AsyncRelayCommand(CleanupOldLogsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            OpenLogDirectoryCommand = new RelayCommand(OpenLogDirectory);
            CopyLogEntryCommand = new RelayCommand<LogEntryDisplayModel>(CopyLogEntry);

            // Load initial settings
            LoadSettings();

            // Start auto-refresh if enabled
            if (_autoRefresh)
            {
                StartAutoRefresh();
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading logs...";

                await RefreshLogsAsync();
                await RefreshStatisticsAsync();

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize log viewer");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshLogsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing logs...";

                var logEntries = await _loggingService.GetLogEntriesAsync(FromDate, ToDate);
                
                // Filter by category and log level
                var filteredEntries = logEntries.Where(entry =>
                {
                    var categoryMatch = SelectedCategory == "All" || entry.Category == SelectedCategory;
                    var levelMatch = entry.Level >= SelectedLogLevel;
                    var searchMatch = string.IsNullOrEmpty(SearchText) || 
                                    entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                    entry.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

                    return categoryMatch && levelMatch && searchMatch;
                }).ToList();

                // Convert to display models
                var displayModels = filteredEntries.Select(entry => new LogEntryDisplayModel
                {
                    Timestamp = entry.Timestamp,
                    Level = entry.Level,
                    Category = entry.Category,
                    Message = entry.Message,
                    Exception = entry.Exception,
                    Properties = entry.Properties,
                    CorrelationId = entry.CorrelationId
                }).ToList();

                // PERFORMANCE OPTIMIZATION: Replace collection instead of Clear() + Add() loop
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LogEntries = new ObservableCollection<LogEntryDisplayModel>(displayModels);
                    StatusMessage = $"Loaded {LogEntries.Count} log entries";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh logs");
                StatusMessage = $"Error refreshing logs: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshStatisticsAsync()
        {
            try
            {
                LogStatistics = await _loggingService.GetLogStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh log statistics");
            }
        }

        private async Task ClearLogsAsync()
        {
            try
            {
                LogEntries.Clear();
                StatusMessage = "Log display cleared";
                await _loggingService.LogUserActionAsync("LogsCleared", "User cleared log display");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear logs");
                StatusMessage = $"Error clearing logs: {ex.Message}";
            }
        }

        private async Task ExportLogsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Exporting logs...";

                var exportPath = await _loggingService.ExportLogsAsync(FromDate, ToDate);
                StatusMessage = $"Logs exported to: {exportPath}";

                await _loggingService.LogUserActionAsync("LogsExported", 
                    $"Logs exported to {exportPath}", $"DateRange: {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export logs");
                StatusMessage = $"Error exporting logs: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CleanupOldLogsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Cleaning up old logs...";

                await _loggingService.CleanupOldLogsAsync();
                await RefreshStatisticsAsync();

                StatusMessage = "Old logs cleaned up successfully";
                await _loggingService.LogUserActionAsync("LogsCleanup", "User initiated log cleanup");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old logs");
                StatusMessage = $"Error cleaning up logs: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                await _loggingService.UpdateConfigurationAsync(EnableDebugLogging, MaxLogFileSizeMb, LogRetentionDays);
                
                _settingsService.Settings.EnableDebugLogging = EnableDebugLogging;
                _settingsService.Settings.MaxLogFileSizeMb = MaxLogFileSizeMb;
                _settingsService.Settings.LogRetentionDays = LogRetentionDays;
                
                await _settingsService.SaveSettingsAsync();

                StatusMessage = "Logging settings saved successfully";
                await _loggingService.LogUserActionAsync("LoggingSettingsChanged", 
                    $"Debug: {EnableDebugLogging}, MaxSize: {MaxLogFileSizeMb}MB, Retention: {LogRetentionDays} days");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save logging settings");
                StatusMessage = $"Error saving settings: {ex.Message}";
            }
        }

        private void OpenLogDirectory()
        {
            try
            {
                var logDirectory = _loggingService.LogDirectoryPath;
                if (Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                }
                else
                {
                    StatusMessage = "Log directory not found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open log directory");
                StatusMessage = $"Error opening log directory: {ex.Message}";
            }
        }

        private void CopyLogEntry(LogEntryDisplayModel? logEntry)
        {
            if (logEntry == null) return;

            try
            {
                var logText = $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{logEntry.Level}] {logEntry.Category}: {logEntry.Message}";
                if (!string.IsNullOrEmpty(logEntry.Exception))
                {
                    logText += $"\nException: {logEntry.Exception}";
                }

                System.Windows.Clipboard.SetText(logText);
                StatusMessage = "Log entry copied to clipboard";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy log entry to clipboard");
                StatusMessage = "Failed to copy log entry";
            }
        }

        private void LoadSettings()
        {
            var settings = _settingsService.Settings;
            EnableDebugLogging = settings.EnableDebugLogging;
            MaxLogFileSizeMb = settings.MaxLogFileSizeMb;
            LogRetentionDays = settings.LogRetentionDays;
        }

        private void StartAutoRefresh()
        {
            // Implementation for auto-refresh timer would go here
            // For now, we'll keep it simple without the timer
        }

        partial void OnSearchTextChanged(string value)
        {
            // Trigger refresh when search text changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshLogsAsync());
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            // Trigger refresh when category changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshLogsAsync());
        }

        partial void OnSelectedLogLevelChanged(LogLevel value)
        {
            // Trigger refresh when log level changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshLogsAsync());
        }
    }

    /// <summary>
    /// Display model for log entries in the UI
    /// </summary>
    public class LogEntryDisplayModel
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public string? CorrelationId { get; set; }

        public string LevelColor => Level switch
        {
            LogLevel.Critical => "#FF0000",
            LogLevel.Error => "#FF4444",
            LogLevel.Warning => "#FFA500",
            LogLevel.Information => "#0066CC",
            LogLevel.Debug => "#808080",
            LogLevel.Trace => "#C0C0C0",
            _ => "#000000"
        };

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        public string ShortMessage => Message.Length > 100 ? Message.Substring(0, 100) + "..." : Message;
        public bool HasException => !string.IsNullOrEmpty(Exception);
        public bool HasProperties => Properties.Any();
    }
}
