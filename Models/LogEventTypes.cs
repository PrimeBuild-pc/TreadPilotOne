namespace ThreadPilot.Models
{
    /// <summary>
    /// Defines structured log event types and categories for consistent logging
    /// </summary>
    public static class LogEventTypes
    {
        /// <summary>
        /// Power plan related events
        /// </summary>
        public static class PowerPlan
        {
            public const string Changed = "PowerPlanChanged";
            public const string ChangeRequested = "PowerPlanChangeRequested";
            public const string ChangeFailed = "PowerPlanChangeFailed";
            public const string Restored = "PowerPlanRestored";
            public const string DefaultSet = "DefaultPowerPlanSet";
            public const string EnumerationFailed = "PowerPlanEnumerationFailed";
        }

        /// <summary>
        /// Process monitoring events
        /// </summary>
        public static class ProcessMonitoring
        {
            public const string Started = "ProcessStarted";
            public const string Stopped = "ProcessStopped";
            public const string MonitoringStarted = "MonitoringStarted";
            public const string MonitoringStopped = "MonitoringStopped";
            public const string WmiEventReceived = "WmiEventReceived";
            public const string WmiConnectionFailed = "WmiConnectionFailed";
            public const string PollingFallback = "PollingFallbackActivated";
            public const string ProcessDetected = "ProcessDetected";
            public const string ProcessLost = "ProcessLost";
            public const string AssociationTriggered = "AssociationTriggered";
        }

        /// <summary>
        /// Game boost related events
        /// </summary>
        public static class GameBoost
        {
            public const string Activated = "GameBoostActivated";
            public const string Deactivated = "GameBoostDeactivated";
            public const string GameDetected = "GameDetected";
            public const string GameAdded = "GameAdded";
            public const string GameRemoved = "GameRemoved";
            public const string PrioritySet = "ProcessPrioritySet";
            public const string PriorityFailed = "ProcessPriorityFailed";
            public const string AffinitySet = "CpuAffinitySet";
            public const string AffinityFailed = "CpuAffinityFailed";
            public const string ConfigurationChanged = "GameBoostConfigurationChanged";
        }

        /// <summary>
        /// User action events
        /// </summary>
        public static class UserActions
        {
            public const string SettingsChanged = "SettingsChanged";
            public const string ProcessAffinityChanged = "ProcessAffinityChanged";
            public const string ProcessPriorityChanged = "ProcessPriorityChanged";
            public const string AssociationAdded = "AssociationAdded";
            public const string AssociationRemoved = "AssociationRemoved";
            public const string MonitoringToggled = "MonitoringToggled";
            public const string GameBoostToggled = "GameBoostToggled";
            public const string AutostartToggled = "AutostartToggled";
            public const string NotificationSettingsChanged = "NotificationSettingsChanged";
            public const string LogsExported = "LogsExported";
            public const string LogsCleared = "LogsCleared";
        }

        /// <summary>
        /// System events
        /// </summary>
        public static class System
        {
            public const string ApplicationStarted = "ApplicationStarted";
            public const string ApplicationShutdown = "ApplicationShutdown";
            public const string ServiceInitialized = "ServiceInitialized";
            public const string ServiceStarted = "ServiceStarted";
            public const string ServiceStopped = "ServiceStopped";
            public const string ConfigurationLoaded = "ConfigurationLoaded";
            public const string ConfigurationSaved = "ConfigurationSaved";
            public const string CpuTopologyDetected = "CpuTopologyDetected";
            public const string SystemTrayInitialized = "SystemTrayInitialized";
            public const string NotificationSent = "NotificationSent";
            public const string ErrorRecovered = "ErrorRecovered";
        }

        /// <summary>
        /// Error categories
        /// </summary>
        public static class Errors
        {
            public const string ServiceFailure = "ServiceFailure";
            public const string ConfigurationError = "ConfigurationError";
            public const string FileSystemError = "FileSystemError";
            public const string PermissionError = "PermissionError";
            public const string NetworkError = "NetworkError";
            public const string WmiError = "WmiError";
            public const string UnhandledException = "UnhandledException";
            public const string ValidationError = "ValidationError";
        }

        /// <summary>
        /// Performance events
        /// </summary>
        public static class Performance
        {
            public const string HighMemoryUsage = "HighMemoryUsage";
            public const string HighCpuUsage = "HighCpuUsage";
            public const string SlowOperation = "SlowOperation";
            public const string LargeLogFile = "LargeLogFile";
            public const string ProcessCountHigh = "ProcessCountHigh";
        }
    }

    /// <summary>
    /// Log categories for organizing log entries
    /// </summary>
    public static class LogCategories
    {
        public const string PowerPlan = "PowerPlan";
        public const string ProcessMonitoring = "ProcessMonitoring";
        public const string GameBoost = "GameBoost";
        public const string UserAction = "UserAction";
        public const string System = "System";
        public const string Error = "Error";
        public const string Performance = "Performance";
        public const string Security = "Security";
        public const string Configuration = "Configuration";
        public const string Lifecycle = "Lifecycle";
    }

    /// <summary>
    /// Common log properties for structured logging
    /// </summary>
    public static class LogProperties
    {
        public const string ProcessName = "ProcessName";
        public const string ProcessId = "ProcessId";
        public const string PowerPlanId = "PowerPlanId";
        public const string PowerPlanName = "PowerPlanName";
        public const string GameName = "GameName";
        public const string UserId = "UserId";
        public const string SessionId = "SessionId";
        public const string CorrelationId = "CorrelationId";
        public const string Duration = "Duration";
        public const string ErrorCode = "ErrorCode";
        public const string StackTrace = "StackTrace";
        public const string MemoryUsage = "MemoryUsage";
        public const string CpuUsage = "CpuUsage";
        public const string ThreadId = "ThreadId";
        public const string Version = "Version";
        public const string Environment = "Environment";
    }

    /// <summary>
    /// Helper class for creating structured log data
    /// </summary>
    public static class LogDataBuilder
    {
        public static Dictionary<string, object> CreateProcessData(string processName, int processId)
        {
            return new Dictionary<string, object>
            {
                [LogProperties.ProcessName] = processName,
                [LogProperties.ProcessId] = processId
            };
        }

        public static Dictionary<string, object> CreatePowerPlanData(string planId, string planName)
        {
            return new Dictionary<string, object>
            {
                [LogProperties.PowerPlanId] = planId,
                [LogProperties.PowerPlanName] = planName
            };
        }

        public static Dictionary<string, object> CreateGameData(string gameName)
        {
            return new Dictionary<string, object>
            {
                [LogProperties.GameName] = gameName
            };
        }

        public static Dictionary<string, object> CreatePerformanceData(long memoryUsage, double cpuUsage)
        {
            return new Dictionary<string, object>
            {
                [LogProperties.MemoryUsage] = memoryUsage,
                [LogProperties.CpuUsage] = cpuUsage
            };
        }

        public static Dictionary<string, object> CreateErrorData(Exception exception)
        {
            return new Dictionary<string, object>
            {
                [LogProperties.ErrorCode] = exception.HResult,
                [LogProperties.StackTrace] = exception.StackTrace ?? "N/A",
                ["ExceptionType"] = exception.GetType().Name,
                ["InnerException"] = exception.InnerException?.Message ?? "N/A"
            };
        }

        public static Dictionary<string, object> CreateTimingData(TimeSpan duration)
        {
            return new Dictionary<string, object>
            {
                [LogProperties.Duration] = duration.TotalMilliseconds
            };
        }

        public static Dictionary<string, object> CreateSystemData()
        {
            return new Dictionary<string, object>
            {
                [LogProperties.Version] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
                [LogProperties.Environment] = Environment.OSVersion.ToString(),
                [LogProperties.ThreadId] = Thread.CurrentThread.ManagedThreadId,
                ["MachineName"] = Environment.MachineName,
                ["UserName"] = Environment.UserName
            };
        }
    }
}
