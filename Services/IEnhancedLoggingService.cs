using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Enhanced logging service interface for persistent, structured logging
    /// </summary>
    public interface IEnhancedLoggingService
    {
        /// <summary>
        /// Gets the current log file path
        /// </summary>
        string CurrentLogFilePath { get; }

        /// <summary>
        /// Gets the log directory path
        /// </summary>
        string LogDirectoryPath { get; }

        /// <summary>
        /// Gets whether debug logging is enabled
        /// </summary>
        bool IsDebugLoggingEnabled { get; }

        /// <summary>
        /// Event raised when a critical error occurs
        /// </summary>
        event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        /// <summary>
        /// Initialize the logging service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Log a power plan change event
        /// </summary>
        Task LogPowerPlanChangeAsync(string fromPlan, string toPlan, string reason, string? processName = null);

        /// <summary>
        /// Log a process monitoring event
        /// </summary>
        Task LogProcessMonitoringEventAsync(string eventType, string processName, int processId, string details);

        /// <summary>
        /// Log a game boost event
        /// </summary>
        Task LogGameBoostEventAsync(string eventType, string gameName, string details);

        /// <summary>
        /// Log a user action
        /// </summary>
        Task LogUserActionAsync(string action, string details, string? context = null);

        /// <summary>
        /// Log a system event
        /// </summary>
        Task LogSystemEventAsync(string eventType, string message, LogLevel level = LogLevel.Information);

        /// <summary>
        /// Log an error with structured data
        /// </summary>
        Task LogErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null);

        /// <summary>
        /// Log application startup/shutdown events
        /// </summary>
        Task LogApplicationLifecycleEventAsync(string eventType, string details);

        /// <summary>
        /// Get recent log entries
        /// </summary>
        Task<List<LogEntry>> GetRecentLogEntriesAsync(int count = 100);

        /// <summary>
        /// Begin a correlated operation scope for better debugging
        /// </summary>
        IDisposable BeginScope(string operationName, object? parameters = null);

        /// <summary>
        /// Get the current correlation ID
        /// </summary>
        string? GetCurrentCorrelationId();

        /// <summary>
        /// Get log entries for a specific date range
        /// </summary>
        Task<List<LogEntry>> GetLogEntriesAsync(DateTime fromDate, DateTime toDate);

        /// <summary>
        /// Clean up old log files based on retention policy
        /// </summary>
        Task CleanupOldLogsAsync();

        /// <summary>
        /// Get log file statistics
        /// </summary>
        Task<LogFileStatistics> GetLogStatisticsAsync();

        /// <summary>
        /// Export logs to a file
        /// </summary>
        Task<string> ExportLogsAsync(DateTime fromDate, DateTime toDate, string? exportPath = null);

        /// <summary>
        /// Update logging configuration
        /// </summary>
        Task UpdateConfigurationAsync(bool enableDebugLogging, int maxFileSizeMb, int retentionDays);
    }

    /// <summary>
    /// Event args for critical errors
    /// </summary>
    public class CriticalErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Context { get; }
        public DateTime Timestamp { get; }
        public Dictionary<string, object> AdditionalData { get; }

        public CriticalErrorEventArgs(Exception exception, string context, Dictionary<string, object>? additionalData = null)
        {
            Exception = exception;
            Context = context;
            Timestamp = DateTime.UtcNow;
            AdditionalData = additionalData ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Represents a log entry
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Log file statistics
    /// </summary>
    public class LogFileStatistics
    {
        public long CurrentFileSizeBytes { get; set; }
        public int TotalLogFiles { get; set; }
        public long TotalLogSizeBytes { get; set; }
        public DateTime OldestLogDate { get; set; }
        public DateTime NewestLogDate { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
    }
}
