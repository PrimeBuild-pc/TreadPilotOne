# ThreadPilot Developer Guide

## Quick Start for New Developers

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension
- Windows 10/11 (for WMI and power plan features)

### Building the Project
```bash
git clone <repository-url>
cd ThreadPilot
dotnet restore
dotnet build
```

### Running the Application
```bash
dotnet run --project ThreadPilot
```

## Key Architectural Concepts

### 1. Service-Oriented Architecture
ThreadPilot uses a layered service architecture with dependency injection:

- **Core Services**: Direct OS interaction (Process, Power Plan, CPU)
- **Business Services**: Application logic (Process Monitoring, Game Boost)
- **Application Services**: UI and configuration (Settings, Notifications)

### 2. MVVM Pattern with Enhanced BaseViewModel
All ViewModels inherit from `BaseViewModel` which provides:
- Centralized error handling with logging
- Async operation helpers with status management
- User action logging for audit trails
- Proper disposal and lifecycle management

### 3. Repository Pattern for Data Access
Data persistence uses the repository pattern with:
- Generic `IRepository<T>` interface for CRUD operations
- JSON file-based implementation with thread-safe operations
- Centralized data access through `IDataAccessService`
- Model validation and integrity checking

## Adding New Features

### 1. Adding a New Service

**Step 1**: Create the interface in appropriate folder:
```csharp
// Services/YourDomain/IYourService.cs
public interface IYourService : ISystemService
{
    Task<bool> DoSomethingAsync();
    event EventHandler<YourEventArgs>? SomethingHappened;
}
```

**Step 2**: Implement the service:
```csharp
// Services/YourDomain/YourService.cs
public class YourService : BaseSystemService, IYourService
{
    public YourService(ILogger<YourService> logger) : base(logger) { }
    
    public async Task<bool> DoSomethingAsync()
    {
        // Implementation
    }
}
```

**Step 3**: Register in `ServiceConfiguration.cs`:
```csharp
services.AddSingleton<IYourService, YourService>();
```

### 2. Adding a New ViewModel

**Step 1**: Create ViewModel inheriting from BaseViewModel:
```csharp
public partial class YourViewModel : BaseViewModel
{
    private readonly IYourService _yourService;
    
    public YourViewModel(
        ILogger<YourViewModel> logger,
        IYourService yourService,
        IEnhancedLoggingService? enhancedLoggingService = null)
        : base(logger, enhancedLoggingService)
    {
        _yourService = yourService;
    }
    
    [RelayCommand]
    private async Task DoSomethingAsync()
    {
        await ExecuteAsync(async () =>
        {
            await _yourService.DoSomethingAsync();
            await LogUserActionAsync("YourAction", "Did something", "User interaction");
        }, "Doing something...", "Operation completed successfully");
    }
}
```

**Step 2**: Register in `ServiceConfiguration.cs`:
```csharp
services.AddTransient<YourViewModel>();
```

### 3. Adding a New Model

**Step 1**: Create model implementing IModel:
```csharp
public partial class YourModel : ObservableObject, IModel
{
    [ObservableProperty]
    private string id = Guid.NewGuid().ToString();
    
    [ObservableProperty]
    private DateTime createdAt = DateTime.UtcNow;
    
    [ObservableProperty]
    private DateTime updatedAt = DateTime.UtcNow;
    
    [ObservableProperty]
    private string name = string.Empty;
    
    // IModel implementation
    public string Id => id;
    public DateTime CreatedAt => createdAt;
    public DateTime UpdatedAt => updatedAt;
    
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors.ToArray());
    }
    
    public IModel Clone()
    {
        return new YourModel
        {
            id = Guid.NewGuid().ToString(),
            Name = this.Name,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow
        };
    }
}
```

## Common Patterns and Best Practices

### Error Handling
Always use the BaseViewModel's error handling methods:
```csharp
// For async operations with user feedback
await ExecuteAsync(async () =>
{
    // Your operation
}, "Loading...", "Completed successfully");

// For setting errors manually
SetError("Something went wrong", exception);

// For clearing errors
ClearError();
```

### Logging
Use structured logging throughout the application:
```csharp
// In services
Logger.LogInformation("Operation completed for {EntityId}", entityId);

// In ViewModels (for user actions)
await LogUserActionAsync("ActionName", "Description", "Context");
```

### Event Handling
Follow the established event pattern:
```csharp
public event EventHandler<YourEventArgs>? YourEvent;

protected virtual void OnYourEvent(YourEventArgs args)
{
    YourEvent?.Invoke(this, args);
}
```

### Async Operations
Always use proper async/await patterns:
```csharp
// Good
public async Task<bool> DoSomethingAsync()
{
    await SomeAsyncOperation();
    return true;
}

// Avoid blocking calls
public bool DoSomething()
{
    DoSomethingAsync().Wait(); // DON'T DO THIS
    return true;
}
```

## Testing Guidelines

### Unit Testing Services
```csharp
[Test]
public async Task YourService_DoSomething_ReturnsExpectedResult()
{
    // Arrange
    var logger = Mock.Of<ILogger<YourService>>();
    var service = new YourService(logger);
    
    // Act
    var result = await service.DoSomethingAsync();
    
    // Assert
    Assert.IsTrue(result);
}
```

### Testing ViewModels
```csharp
[Test]
public async Task YourViewModel_DoSomething_UpdatesStatusCorrectly()
{
    // Arrange
    var logger = Mock.Of<ILogger<YourViewModel>>();
    var service = Mock.Of<IYourService>();
    var viewModel = new YourViewModel(logger, service);
    
    // Act
    await viewModel.DoSomethingCommand.ExecuteAsync(null);
    
    // Assert
    Assert.IsFalse(viewModel.HasError);
}
```

## Performance Considerations

### Memory Management
- Always dispose of services that implement IDisposable
- Unsubscribe from events in ViewModel disposal
- Use weak event patterns for long-lived subscriptions

### Threading
- UI operations must be on the UI thread
- Use ConfigureAwait(false) for non-UI async operations
- Protect shared resources with appropriate synchronization

### Data Access
- Repository operations are thread-safe
- Use batch operations when possible
- Implement proper caching for frequently accessed data

## Troubleshooting Common Issues

### Service Resolution Errors
Check `ServiceConfiguration.cs` to ensure all dependencies are registered with correct lifetimes.

### ViewModel Constructor Issues
Ensure all ViewModels call the base constructor with required logger parameter.

### Data Persistence Issues
Check file permissions and ensure data directory exists. Use logging to trace repository operations.

### Event Subscription Leaks
Always unsubscribe from events in the ViewModel's OnDispose method.

## Configuration and Settings

### Application Settings
Settings are managed through `IApplicationSettingsService` and persisted automatically:
```csharp
// Access current settings
var settings = _settingsService.Settings;

// Modify settings
settings.EnableNotifications = true;

// Save changes (automatic on property change)
await _settingsService.SaveSettingsAsync();
```

### Logging Configuration
Enhanced logging is configured in `ServiceConfiguration.cs`:
- Console logging for development
- File-based logging with rotation for production
- Structured events for different operation types

### Data Storage Locations
- **Settings**: `%AppData%\ThreadPilot\settings.json`
- **Associations**: `%AppData%\ThreadPilot\Data\ProcessAssociations.json`
- **Profiles**: `%AppData%\ThreadPilot\Data\ProcessProfiles.json`
- **Logs**: `%AppData%\ThreadPilot\Logs\`

## Deployment and Distribution

### Release Build
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

### Installer Considerations
- Register for Windows autostart if enabled
- Create application data directories
- Set appropriate file permissions
- Register WMI event handlers

This guide provides the foundation for extending ThreadPilot while maintaining code quality and architectural consistency.
