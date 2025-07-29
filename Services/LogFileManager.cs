using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Manages log file operations including rotation, cleanup, and concurrent access
    /// </summary>
    public class LogFileManager : IDisposable
    {
        private readonly ILogger<LogFileManager> _logger;
        private readonly string _logDirectory;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly ReaderWriterLockSlim _configLock = new();
        private bool _disposed;

        // Configuration
        private int _maxFileSizeMb = 10;
        private int _retentionDays = 7;
        private int _maxLogFiles = 50;

        public string LogDirectory => _logDirectory;
        public string CurrentLogFilePath { get; private set; }

        public LogFileManager(ILogger<LogFileManager> logger, string? logDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _logDirectory = logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "ThreadPilot", 
                "Logs");

            CurrentLogFilePath = Path.Combine(_logDirectory, "ThreadPilot.log");
        }

        /// <summary>
        /// Initialize the log file manager
        /// </summary>
        public async Task InitializeAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                // Ensure log directory exists
                Directory.CreateDirectory(_logDirectory);

                // Create current log file if it doesn't exist
                if (!File.Exists(CurrentLogFilePath))
                {
                    await CreateNewLogFileAsync();
                }

                _logger.LogInformation("Log file manager initialized. Directory: {LogDirectory}", _logDirectory);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Write log entries to the current log file with automatic rotation
        /// </summary>
        public async Task WriteLogEntriesAsync(IEnumerable<string> logLines)
        {
            await _fileLock.WaitAsync();
            try
            {
                // Check if rotation is needed
                await CheckAndRotateLogFileAsync();

                // Write entries
                await File.AppendAllLinesAsync(CurrentLogFilePath, logLines);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Write a single log entry
        /// </summary>
        public async Task WriteLogEntryAsync(string logLine)
        {
            await WriteLogEntriesAsync(new[] { logLine });
        }

        /// <summary>
        /// Read log entries from all log files within date range
        /// </summary>
        public async Task<List<string>> ReadLogEntriesAsync(DateTime fromDate, DateTime toDate, int maxEntries = 1000)
        {
            await _fileLock.WaitAsync();
            try
            {
                var allEntries = new List<(DateTime timestamp, string line)>();
                var logFiles = GetLogFiles();

                foreach (var logFile in logFiles)
                {
                    var entries = await ReadLogEntriesFromFileAsync(logFile, fromDate, toDate);
                    allEntries.AddRange(entries);
                }

                return allEntries
                    .OrderByDescending(e => e.timestamp)
                    .Take(maxEntries)
                    .Select(e => e.line)
                    .ToList();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Get log file statistics
        /// </summary>
        public async Task<LogFileStatistics> GetStatisticsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var stats = new LogFileStatistics();
                var logFiles = GetLogFiles();

                stats.TotalLogFiles = logFiles.Count;

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

                // Count log levels by reading recent entries
                await CountLogLevelsAsync(stats);

                return stats;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Clean up old log files based on retention policy
        /// </summary>
        public async Task CleanupOldLogsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                _configLock.EnterReadLock();
                var retentionDate = DateTime.UtcNow.AddDays(-_retentionDays);
                var maxFiles = _maxLogFiles;
                _configLock.ExitReadLock();

                var logFiles = GetLogFiles()
                    .Where(f => Path.GetFileName(f) != "ThreadPilot.log") // Don't delete current log
                    .OrderBy(f => new FileInfo(f).CreationTime)
                    .ToList();

                var deletedCount = 0;

                // Delete files older than retention period
                foreach (var logFile in logFiles.ToList())
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < retentionDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                            logFiles.Remove(logFile);
                            deletedCount++;
                            _logger.LogDebug("Deleted old log file: {LogFile}", logFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete old log file: {LogFile}", logFile);
                        }
                    }
                }

                // Delete excess files if we have too many
                if (logFiles.Count > maxFiles)
                {
                    var excessFiles = logFiles.Take(logFiles.Count - maxFiles);
                    foreach (var logFile in excessFiles)
                    {
                        try
                        {
                            File.Delete(logFile);
                            deletedCount++;
                            _logger.LogDebug("Deleted excess log file: {LogFile}", logFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete excess log file: {LogFile}", logFile);
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {DeletedCount} old log files", deletedCount);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Export logs to a specified file
        /// </summary>
        public async Task<string> ExportLogsAsync(DateTime fromDate, DateTime toDate, string? exportPath = null)
        {
            exportPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"ThreadPilot_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var logEntries = await ReadLogEntriesAsync(fromDate, toDate, int.MaxValue);
            
            var exportContent = new List<string>
            {
                $"# ThreadPilot Log Export",
                $"# Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"# Date Range: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}",
                $"# Total Entries: {logEntries.Count}",
                ""
            };

            exportContent.AddRange(logEntries);

            await File.WriteAllLinesAsync(exportPath, exportContent);
            return exportPath;
        }

        /// <summary>
        /// Update configuration
        /// </summary>
        public void UpdateConfiguration(int maxFileSizeMb, int retentionDays, int maxLogFiles = 50)
        {
            _configLock.EnterWriteLock();
            try
            {
                _maxFileSizeMb = maxFileSizeMb;
                _retentionDays = retentionDays;
                _maxLogFiles = maxLogFiles;
            }
            finally
            {
                _configLock.ExitWriteLock();
            }
        }

        private async Task CheckAndRotateLogFileAsync()
        {
            var fileInfo = new FileInfo(CurrentLogFilePath);
            
            _configLock.EnterReadLock();
            var maxSizeBytes = _maxFileSizeMb * 1024 * 1024;
            _configLock.ExitReadLock();

            if (fileInfo.Exists && fileInfo.Length > maxSizeBytes)
            {
                await RotateLogFileAsync();
            }
        }

        private async Task RotateLogFileAsync()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var rotatedPath = Path.Combine(_logDirectory, $"ThreadPilot_{timestamp}.log");

            try
            {
                // Move current log to rotated name
                File.Move(CurrentLogFilePath, rotatedPath);
                
                // Create new current log file
                await CreateNewLogFileAsync();

                _logger.LogInformation("Log file rotated: {RotatedPath}", rotatedPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rotate log file");
                throw;
            }
        }

        private async Task CreateNewLogFileAsync()
        {
            var header = new[]
            {
                $"# ThreadPilot Log File",
                $"# Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                $"# Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}",
                $"# Machine: {Environment.MachineName}",
                ""
            };

            await File.WriteAllLinesAsync(CurrentLogFilePath, header);
        }

        private List<string> GetLogFiles()
        {
            return Directory.GetFiles(_logDirectory, "*.log")
                .OrderByDescending(f => new FileInfo(f).CreationTime)
                .ToList();
        }

        private async Task<List<(DateTime timestamp, string line)>> ReadLogEntriesFromFileAsync(string filePath, DateTime fromDate, DateTime toDate)
        {
            var entries = new List<(DateTime timestamp, string line)>();

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;

                    // Try to extract timestamp from JSON log entry
                    if (TryExtractTimestamp(line, out var timestamp))
                    {
                        if (timestamp >= fromDate && timestamp <= toDate)
                        {
                            entries.Add((timestamp, line));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read log entries from file: {FilePath}", filePath);
            }

            return entries;
        }

        private bool TryExtractTimestamp(string logLine, out DateTime timestamp)
        {
            timestamp = default;

            try
            {
                // Look for timestamp in JSON format: "timestamp":"2024-01-01 12:00:00.000"
                var timestampStart = logLine.IndexOf("\"timestamp\":\"");
                if (timestampStart >= 0)
                {
                    timestampStart += 13; // Length of "timestamp":""
                    var timestampEnd = logLine.IndexOf("\"", timestampStart);
                    if (timestampEnd > timestampStart)
                    {
                        var timestampStr = logLine.Substring(timestampStart, timestampEnd - timestampStart);
                        return DateTime.TryParse(timestampStr, out timestamp);
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return false;
        }

        private async Task CountLogLevelsAsync(LogFileStatistics stats)
        {
            try
            {
                // Read recent entries to count log levels
                var recentEntries = await ReadLogEntriesAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, 1000);
                
                foreach (var entry in recentEntries)
                {
                    if (entry.Contains("\"level\":\"Error\"")) stats.ErrorCount++;
                    else if (entry.Contains("\"level\":\"Warning\"")) stats.WarningCount++;
                    else if (entry.Contains("\"level\":\"Information\"")) stats.InfoCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to count log levels");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _fileLock?.Dispose();
            _configLock?.Dispose();
            _disposed = true;
        }
    }
}
