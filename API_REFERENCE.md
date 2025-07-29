# ThreadPilot API Reference

## Core Interfaces

### ISystemService
Base interface for all system services providing lifecycle management.

```csharp
public interface ISystemService
{
    bool IsAvailable { get; }
    event EventHandler<ServiceAvailabilityChangedEventArgs>? AvailabilityChanged;
    Task InitializeAsync();
    Task DisposeAsync();
}
```

**Key Methods:**
- `InitializeAsync()`: Initialize service resources
- `DisposeAsync()`: Clean up service resources
- `IsAvailable`: Indicates if service is operational

### IRepository<T>
Generic repository interface for data access operations.

```csharp
public interface IRepository<T> : IRepository<T, string> where T : class
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
    Task<bool> ExistsAsync(string id);
    Task<int> CountAsync();
}
```

## Core Services

### IProcessService
Manages process enumeration and manipulation.

```csharp
public interface IProcessService
{
    Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync();
    Task<bool> SetProcessPriorityAsync(int processId, ProcessPriorityClass priority);
    Task<bool> SetProcessAffinityAsync(int processId, long affinityMask);
    Task<ProcessModel?> GetProcessByIdAsync(int processId);
    bool IsProcessRunning(string processName);
}
```

**Key Features:**
- Async process enumeration
- Priority and affinity management
- Process existence checking

### IPowerPlanService
Manages Windows power plans.

```csharp
public interface IPowerPlanService
{
    Task<IEnumerable<PowerPlanModel>> GetAvailablePowerPlansAsync();
    Task<PowerPlanModel?> GetActivePowerPlanAsync();
    Task<bool> SetActivePowerPlanAsync(string powerPlanGuid);
    Task<PowerPlanModel?> GetPowerPlanByGuidAsync(string guid);
    event EventHandler<PowerPlanChangedEventArgs>? PowerPlanChanged;
}
```

**Key Features:**
- Power plan enumeration and activation
- Change event notifications
- GUID-based plan identification

### ICpuTopologyService
Provides CPU topology detection and affinity management.

```csharp
public interface ICpuTopologyService
{
    Task<CpuTopologyModel> DetectTopologyAsync();
    Task<bool> SetProcessAffinityAsync(int processId, IEnumerable<int> coreIds);
    Task<long> GetProcessAffinityAsync(int processId);
    event EventHandler<CpuTopologyDetectedEventArgs>? TopologyDetected;
}
```

**Key Features:**
- Modern CPU architecture detection
- Core affinity management
- Support for hybrid architectures

## Business Logic Services

### IProcessMonitorManagerService
Orchestrates process monitoring and power plan switching.

```csharp
public interface IProcessMonitorManagerService
{
    bool IsMonitoringActive { get; }
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
    event EventHandler<ProcessEventArgs>? ProcessStarted;
    event EventHandler<ProcessEventArgs>? ProcessStopped;
    event EventHandler? MonitoringStarted;
    event EventHandler? MonitoringStopped;
}
```

**Key Features:**
- WMI-based process monitoring
- Automatic power plan switching
- Fallback polling mechanism

### IGameBoostService
Manages game detection and performance optimization.

```csharp
public interface IGameBoostService
{
    bool IsGameBoostActive { get; }
    string? CurrentGameName { get; }
    Task<bool> ActivateGameBoostAsync(ProcessModel gameProcess);
    Task DeactivateGameBoostAsync();
    Task<bool> AddKnownGameAsync(string executableName);
    Task<bool> RemoveKnownGameAsync(string executableName);
    event EventHandler<GameBoostEventArgs>? GameBoostActivated;
    event EventHandler<GameBoostEventArgs>? GameBoostDeactivated;
}
```

**Key Features:**
- Automatic game detection
- Performance optimization
- Configurable game database

## Application Services

### IApplicationSettingsService
Manages application configuration and persistence.

```csharp
public interface IApplicationSettingsService
{
    ApplicationSettingsModel Settings { get; }
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    Task ResetToDefaultsAsync();
    Task<bool> ExportSettingsAsync(string filePath);
    Task<bool> ImportSettingsAsync(string filePath);
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
}
```

**Key Features:**
- Automatic settings persistence
- Import/export functionality
- Change notifications

### INotificationService
Handles user notifications and system tray integration.

```csharp
public interface INotificationService
{
    Task ShowNotificationAsync(string title, string message, NotificationType type);
    Task ShowBalloonNotificationAsync(string title, string message, int duration);
    Task ShowToastNotificationAsync(string title, string message);
    Task ClearAllNotificationsAsync();
}
```

**Key Features:**
- Multiple notification types
- System tray balloon tips
- Windows toast notifications

### IEnhancedLoggingService
Provides structured logging with file persistence.

```csharp
public interface IEnhancedLoggingService
{
    Task LogPowerPlanChangeAsync(string fromPlan, string toPlan, string reason);
    Task LogProcessEventAsync(string processName, string eventType, string details);
    Task LogGameBoostEventAsync(string gameName, string action, string details);
    Task LogUserActionAsync(string action, string details, string? context = null);
    Task LogErrorAsync(string source, string error, string? stackTrace = null);
    Task<IEnumerable<LogEntry>> GetLogEntriesAsync(DateTime? fromDate = null, DateTime? toDate = null);
}
```

**Key Features:**
- Structured event logging
- File rotation and management
- Query capabilities

## Data Models

### ProcessModel
Represents a system process with extended information.

```csharp
public partial class ProcessModel : ObservableObject
{
    public int ProcessId { get; set; }
    public string Name { get; set; }
    public string ExecutablePath { get; set; }
    public ProcessPriorityClass Priority { get; set; }
    public long ProcessorAffinity { get; set; }
    public double CpuUsage { get; set; }
    public long WorkingSet { get; set; }
}
```

### PowerPlanModel
Represents a Windows power plan.

```csharp
public partial class PowerPlanModel : ObservableObject
{
    public string Guid { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
}
```

### CpuTopologyModel
Represents CPU topology information.

```csharp
public partial class CpuTopologyModel : ObservableObject
{
    public int TotalCores { get; set; }
    public int PhysicalCores { get; set; }
    public int LogicalCores { get; set; }
    public bool HasHybridArchitecture { get; set; }
    public ObservableCollection<CpuCoreModel> Cores { get; set; }
}
```

## Event Arguments

### ProcessEventArgs
Provides data for process-related events.

```csharp
public class ProcessEventArgs : EventArgs
{
    public ProcessModel Process { get; }
    public DateTime Timestamp { get; }
    public string EventType { get; }
}
```

### PowerPlanChangedEventArgs
Provides data for power plan change events.

```csharp
public class PowerPlanChangedEventArgs : EventArgs
{
    public PowerPlanModel? PreviousPlan { get; }
    public PowerPlanModel CurrentPlan { get; }
    public string Reason { get; }
    public DateTime Timestamp { get; }
}
```

### GameBoostEventArgs
Provides data for game boost events.

```csharp
public class GameBoostEventArgs : EventArgs
{
    public ProcessModel? GameProcess { get; }
    public bool IsActivated { get; }
    public DateTime Timestamp { get; }
}
```

## Configuration Enums

### NotificationType
Defines notification types for the notification service.

```csharp
public enum NotificationType
{
    Information,
    Warning,
    Error,
    Success
}
```

### ProcessPriorityClass
Standard .NET process priority enumeration.

```csharp
public enum ProcessPriorityClass
{
    Idle,
    BelowNormal,
    Normal,
    AboveNormal,
    High,
    RealTime
}
```

This API reference provides comprehensive documentation for all public interfaces and key classes in ThreadPilot.
