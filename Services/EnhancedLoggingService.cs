using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Enhanced logging service with file persistence and structured logging
    /// </summary>
    public class EnhancedLoggingService : IEnhancedLoggingService, IDisposable
    {
        private readonly ILogger<EnhancedLoggingService> _logger;
        private readonly IApplicationSettingsService _settingsService;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly System.Threading.Timer _flushTimer;
        private readonly string _logDirectory;
        private string _currentLogFilePath;
        private bool _isInitialized;
        private bool _disposed;

        public string CurrentLogFilePath => _currentLogFilePath;
        public string LogDirectoryPath => _logDirectory;
        public bool IsDebugLoggingEnabled => _settingsService.Settings.EnableDebugLogging;

        public event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        public EnhancedLoggingService(ILogger<EnhancedLoggingService> logger, IApplicationSettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Set up log directory
            _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreadPilot", "Logs");
            _currentLogFilePath = GetCurrentLogFilePath();

            // Create flush timer (flush every 5 seconds)
            _flushTimer = new System.Threading.Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Ensure log directory exists
                Directory.CreateDirectory(_logDirectory);

                // Create initial log file if it doesn't exist
                if (!File.Exists(_currentLogFilePath))
                {
                    await CreateNewLogFileAsync();
                }

                // Log initialization
                await LogSystemEventAsync("LoggingService", "Enhanced logging service initialized", LogLevel.Information);

                // Clean up old logs
                await CleanupOldLogsAsync();

                _isInitialized = true;
                _logger.LogInformation("Enhanced logging service initialized. Log directory: {LogDirectory}", _logDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize enhanced logging service");
                throw;
            }
        }

        public async Task LogPowerPlanChangeAsync(string fromPlan, string toPlan, string reason, string? processName = null)
        {
            var properties = new Dictionary<string, object>
            {
                ["FromPlan"] = fromPlan,
                ["ToPlan"] = toPlan,
                ["Reason"] = reason,
                ["ProcessName"] = processName ?? "N/A"
            };

            var message = processName != null 
                ? $"Power plan changed from '{fromPlan}' to '{toPlan}' due to process '{processName}' ({reason})"
                : $"Power plan changed from '{fromPlan}' to '{toPlan}' ({reason})";

            await LogStructuredEventAsync("PowerPlan", message, LogLevel.Information, properties);
        }

        public async Task LogProcessMonitoringEventAsync(string eventType, string processName, int processId, string details)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["ProcessName"] = processName,
                ["ProcessId"] = processId,
                ["Details"] = details
            };

            var message = $"Process monitoring event: {eventType} - {processName} (PID: {processId}) - {details}";
            await LogStructuredEventAsync("ProcessMonitoring", message, LogLevel.Information, properties);
        }

        public async Task LogGameBoostEventAsync(string eventType, string gameName, string details)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["GameName"] = gameName,
                ["Details"] = details
            };

            var message = $"Game Boost {eventType}: {gameName} - {details}";
            await LogStructuredEventAsync("GameBoost", message, LogLevel.Information, properties);
        }

        public async Task LogUserActionAsync(string action, string details, string? context = null)
        {
            var properties = new Dictionary<string, object>
            {
                ["Action"] = action,
                ["Details"] = details,
                ["Context"] = context ?? "N/A"
            };

            var message = $"User action: {action} - {details}";
            if (!string.IsNullOrEmpty(context))
            {
                message += $" (Context: {context})";
            }

            await LogStructuredEventAsync("UserAction", message, LogLevel.Information, properties);
        }

        public async Task LogSystemEventAsync(string eventType, string message, LogLevel level = LogLevel.Information)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType
            };

            await LogStructuredEventAsync("System", message, level, properties);
        }

        public async Task LogErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null)
        {
            var properties = new Dictionary<string, object>
            {
                ["Context"] = context,
                ["ExceptionType"] = exception.GetType().Name,
                ["StackTrace"] = exception.StackTrace ?? "N/A"
            };

            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }

            var message = $"Error in {context}: {exception.Message}";
            await LogStructuredEventAsync("Error", message, LogLevel.Error, properties, exception);

            // Raise critical error event for severe exceptions
            if (exception is OutOfMemoryException or StackOverflowException or AccessViolationException)
            {
                CriticalErrorOccurred?.Invoke(this, new CriticalErrorEventArgs(exception, context, additionalData));
            }
        }

        public async Task LogApplicationLifecycleEventAsync(string eventType, string details)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Details"] = details,
                ["Version"] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"
            };

            var message = $"Application {eventType}: {details}";
            await LogStructuredEventAsync("Lifecycle", message, LogLevel.Information, properties);
        }

        private async Task LogStructuredEventAsync(string category, string message, LogLevel level, Dictionary<string, object> properties, Exception? exception = null)
        {
            if (!_isInitialized && category != "System") return;

            // Skip debug messages if debug logging is disabled
            if (level == LogLevel.Debug && !IsDebugLoggingEnabled) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                Exception = exception?.ToString(),
                Properties = properties,
                CorrelationId = Thread.CurrentThread.ManagedThreadId.ToString()
            };

            _logQueue.Enqueue(logEntry);

            // Force immediate flush for errors and critical events
            if (level >= LogLevel.Error)
            {
                await FlushLogsAsync();
            }
        }

        private async void FlushLogs(object? state)
        {
            await FlushLogsAsync();
        }

        private async Task FlushLogsAsync()
        {
            if (_logQueue.IsEmpty) return;

            await _fileLock.WaitAsync();
            try
            {
                // Check if we need to rotate the log file
                await CheckLogRotationAsync();

                var logEntries = new List<LogEntry>();
                while (_logQueue.TryDequeue(out var entry))
                {
                    logEntries.Add(entry);
                }

                if (logEntries.Count == 0) return;

                // Write entries to file
                await WriteLogEntriesToFileAsync(logEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush logs to file");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task WriteLogEntriesToFileAsync(List<LogEntry> entries)
        {
            var logLines = entries.Select(FormatLogEntry);
            await File.AppendAllLinesAsync(_currentLogFilePath, logLines);
        }

        private string FormatLogEntry(LogEntry entry)
        {
            var logData = new
            {
                timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level = entry.Level.ToString(),
                category = entry.Category,
                message = entry.Message,
                exception = entry.Exception,
                properties = entry.Properties,
                correlationId = entry.CorrelationId
            };

            return JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = false });
        }

        private async Task CheckLogRotationAsync()
        {
            var fileInfo = new FileInfo(_currentLogFilePath);
            var maxSizeBytes = _settingsService.Settings.MaxLogFileSizeMb * 1024 * 1024;

            if (fileInfo.Exists && fileInfo.Length > maxSizeBytes)
            {
                // Rotate log file
                var rotatedPath = Path.Combine(_logDirectory, $"ThreadPilot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
                File.Move(_currentLogFilePath, rotatedPath);
                await CreateNewLogFileAsync();
            }
        }

        private async Task CreateNewLogFileAsync()
        {
            _currentLogFilePath = GetCurrentLogFilePath();
            await File.WriteAllTextAsync(_currentLogFilePath, $"# ThreadPilot Log File - Created {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{Environment.NewLine}");
        }

        private string GetCurrentLogFilePath()
        {
            return Path.Combine(_logDirectory, "ThreadPilot.log");
        }

        public async Task<List<LogEntry>> GetRecentLogEntriesAsync(int count = 100)
        {
            return await GetLogEntriesAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        }

        public async Task<List<LogEntry>> GetLogEntriesAsync(DateTime fromDate, DateTime toDate)
        {
            var entries = new List<LogEntry>();

            await _fileLock.WaitAsync();
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                    .OrderByDescending(f => new FileInfo(f).CreationTime);

                foreach (var logFile in logFiles)
                {
                    var fileEntries = await ReadLogEntriesFromFileAsync(logFile, fromDate, toDate);
                    entries.AddRange(fileEntries);
                }

                return entries.OrderByDescending(e => e.Timestamp).Take(1000).ToList();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<List<LogEntry>> ReadLogEntriesFromFileAsync(string filePath, DateTime fromDate, DateTime toDate)
        {
            var entries = new List<LogEntry>();

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var logData = JsonSerializer.Deserialize<JsonElement>(line);
                        var timestamp = DateTime.Parse(logData.GetProperty("timestamp").GetString()!);

                        if (timestamp >= fromDate && timestamp <= toDate)
                        {
                            var entry = new LogEntry
                            {
                                Timestamp = timestamp,
                                Level = Enum.Parse<LogLevel>(logData.GetProperty("level").GetString()!),
                                Category = logData.GetProperty("category").GetString()!,
                                Message = logData.GetProperty("message").GetString()!,
                                Exception = logData.TryGetProperty("exception", out var ex) ? ex.GetString() : null,
                                CorrelationId = logData.TryGetProperty("correlationId", out var cid) ? cid.GetString() : null
                            };

                            if (logData.TryGetProperty("properties", out var props))
                            {
                                entry.Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(props.GetRawText()) ?? new();
                            }

                            entries.Add(entry);
                        }
                    }
                    catch
                    {
                        // Skip malformed log entries
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read log entries from file: {FilePath}", filePath);
            }

            return entries;
        }

        public async Task CleanupOldLogsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var retentionDate = DateTime.UtcNow.AddDays(-_settingsService.Settings.LogRetentionDays);
                var logFiles = Directory.GetFiles(_logDirectory, "*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < retentionDate && Path.GetFileName(logFile) != "ThreadPilot.log")
                    {
                        try
                        {
                            File.Delete(logFile);
                            _logger.LogDebug("Deleted old log file: {LogFile}", logFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete old log file: {LogFile}", logFile);
                        }
                    }
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<LogFileStatistics> GetLogStatisticsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var stats = new LogFileStatistics();
                var logFiles = Directory.GetFiles(_logDirectory, "*.log");

                stats.TotalLogFiles = logFiles.Length;

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    stats.TotalLogSizeBytes += fileInfo.Length;

                    if (Path.GetFileName(logFile) == "ThreadPilot.log")
                    {
                        stats.CurrentFileSizeBytes = fileInfo.Length;
                    }

                    if (stats.OldestLogDate == default || fileInfo.CreationTime < stats.OldestLogDate)
                    {
                        stats.OldestLogDate = fileInfo.CreationTime;
                    }

                    if (fileInfo.CreationTime > stats.NewestLogDate)
                    {
                        stats.NewestLogDate = fileInfo.CreationTime;
                    }
                }

                return stats;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<string> ExportLogsAsync(DateTime fromDate, DateTime toDate, string? exportPath = null)
        {
            exportPath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                $"ThreadPilot_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var entries = await GetLogEntriesAsync(fromDate, toDate);
            var exportLines = entries.Select(e => $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{e.Level}] {e.Category}: {e.Message}");

            await File.WriteAllLinesAsync(exportPath, exportLines);
            return exportPath;
        }

        public async Task UpdateConfigurationAsync(bool enableDebugLogging, int maxFileSizeMb, int retentionDays)
        {
            _settingsService.Settings.EnableDebugLogging = enableDebugLogging;
            _settingsService.Settings.MaxLogFileSizeMb = maxFileSizeMb;
            _settingsService.Settings.LogRetentionDays = retentionDays;

            await _settingsService.SaveSettingsAsync();
            await LogSystemEventAsync("Configuration", $"Logging configuration updated: Debug={enableDebugLogging}, MaxSize={maxFileSizeMb}MB, Retention={retentionDays}days");
        }

        public void Dispose()
        {
            if (_disposed) return;

            _flushTimer?.Dispose();
            FlushLogsAsync().Wait(TimeSpan.FromSeconds(5));
            _fileLock?.Dispose();
            _disposed = true;
        }
    }
}
