# ThreadPilot Architecture Guide

## Overview
This document describes the modular architecture of ThreadPilot, designed for maintainability, testability, and future extensibility.

## Architecture Principles

### 1. Separation of Concerns
- **Core Services**: Direct OS interaction (Process, Power Plan, CPU Topology)
- **Process Management**: Business logic for process monitoring and management
- **Application Services**: UI and application-level functionality
- **Presentation Layer**: ViewModels and Views with MVVM pattern

### 2. Dependency Injection
- Centralized service configuration in `ServiceConfiguration.cs`
- Service factory pattern for advanced service management
- Proper lifecycle management for all services

### 3. Event-Driven Architecture
- Services communicate through well-defined events
- Loose coupling between components
- Reactive UI updates through observable patterns

## Project Structure

```
ThreadPilot/
├── Services/
│   ├── Core/                          # Base interfaces and implementations
│   │   ├── ISystemService.cs          # Base interface for system services
│   │   └── BaseSystemService.cs       # Base implementation with common functionality
│   ├── ProcessManagement/             # Process-related services
│   │   └── IProcessManagementService.cs
│   ├── ServiceConfiguration.cs        # Centralized DI configuration
│   └── ServiceFactory.cs             # Service factory for advanced management
├── Models/
│   ├── Core/                          # Base model interfaces and implementations
│   │   └── IModel.cs                  # Base model interface and validation
│   ├── Process/                       # Process-related models
│   ├── PowerPlan/                     # Power plan models
│   └── Configuration/                 # Configuration models
├── ViewModels/
│   ├── BaseViewModel.cs               # Enhanced base ViewModel with error handling
│   └── ViewModelFactory.cs           # Factory for ViewModel creation and management
├── Views/                             # XAML views and code-behind
├── Converters/                        # Value converters for data binding
├── Helpers/                           # Utility classes and extension methods
└── Tests/                             # Unit and integration tests
```

## Service Layer Architecture

### Core Services (`Services/Core/`)
Base interfaces and implementations for all services:

- **ISystemService**: Base interface with availability tracking and lifecycle management
- **BaseSystemService**: Common functionality for initialization, disposal, and error handling

### Service Categories

#### 1. Core System Services
Direct interaction with the operating system:
- `IProcessService`: Process enumeration and manipulation
- `IPowerPlanService`: Windows power plan management
- `ICpuTopologyService`: CPU topology detection and affinity management

#### 2. Process Management Services
Business logic for process monitoring:
- `IProcessMonitorService`: WMI-based process event monitoring
- `IProcessPowerPlanAssociationService`: Process-to-power plan associations
- `IProcessMonitorManagerService`: Orchestrates process monitoring
- `IGameBoostService`: Game detection and performance optimization

#### 3. Application Services
Application-level functionality:
- `IApplicationSettingsService`: Configuration persistence
- `INotificationService`: User notifications and system tray
- `IAutostartService`: Windows startup integration
- `IEnhancedLoggingService`: Structured logging with file persistence

### Service Factory Pattern
The `ServiceFactory` class provides:
- Centralized service creation with dependency resolution
- Lifecycle management for `ISystemService` implementations
- Automatic initialization and disposal of managed services
- Error handling and logging for service operations

## Presentation Layer Architecture

### Enhanced BaseViewModel
The `BaseViewModel` class provides:
- **Error Handling**: Centralized error management with logging
- **Status Management**: Busy states and status messages
- **Async Operations**: Helper methods for async operations with error handling
- **Logging Integration**: Automatic user action logging
- **Lifecycle Management**: Proper initialization and disposal

### ViewModel Factory
The `ViewModelFactory` class provides:
- Dependency injection for ViewModels
- Automatic initialization of ViewModels
- Lifecycle management and disposal
- Error handling during creation and initialization

## Model Layer Architecture

### Base Model Interface
The `IModel` interface provides:
- **Identity**: Unique ID and timestamps
- **Validation**: Built-in validation framework
- **Change Tracking**: Property change notifications
- **Cloning**: Deep copy support

### Domain-Specific Models
Models are organized by domain:
- **Process Models**: Process information and metadata
- **Power Plan Models**: Power plan configurations
- **Configuration Models**: Application settings and associations

## Configuration and Dependency Injection

### ServiceConfiguration Class
Centralized configuration with methods for each service category:
- `ConfigureServiceInfrastructure()`: Logging and factories
- `ConfigureCoreSystemServices()`: OS interaction services
- `ConfigureProcessManagementServices()`: Business logic services
- `ConfigureApplicationLevelServices()`: UI and application services
- `ConfigurePresentationLayer()`: ViewModels and Views

### Service Validation
Automatic validation of service configuration at startup:
- Ensures all required services can be resolved
- Validates service dependencies
- Provides clear error messages for configuration issues

## Error Handling and Logging

### Structured Logging
- **Enhanced Logging Service**: File-based logging with rotation
- **Structured Events**: Predefined event types for different operations
- **User Action Logging**: Audit trail for user interactions
- **Error Correlation**: Consistent error tracking across services

### Error Handling Patterns
- **Service Level**: Try-catch with logging and graceful degradation
- **ViewModel Level**: User-friendly error messages with technical logging
- **Application Level**: Global exception handling with recovery

## Testing Strategy

### Unit Testing
- Service interfaces enable easy mocking
- BaseViewModel provides testable error handling
- Model validation can be tested independently

### Integration Testing
- Service factory enables testing of service interactions
- Event-driven architecture allows testing of component communication
- Configuration validation ensures proper setup

## Future Extensibility

### Adding New Services
1. Implement appropriate base interface (`ISystemService` for system services)
2. Add to relevant service category in `ServiceConfiguration`
3. Follow established patterns for error handling and logging

### Adding New Features
1. Create domain-specific models with validation
2. Implement services following the established patterns
3. Create ViewModels inheriting from `BaseViewModel`
4. Use dependency injection for all dependencies

### Performance Optimization
- Service factory enables lazy loading of services
- Event-driven architecture allows for efficient updates
- Structured logging provides performance monitoring data

## Best Practices

### Service Development
- Always implement proper disposal for resources
- Use structured logging for all operations
- Implement graceful degradation for optional features
- Follow async/await patterns consistently

### ViewModel Development
- Inherit from `BaseViewModel` for consistent functionality
- Use `ExecuteAsync` methods for error handling
- Implement proper disposal for event subscriptions
- Log user actions for audit purposes

### Model Development
- Implement validation for all business rules
- Use property change notifications for UI binding
- Provide meaningful error messages
- Support cloning for undo/redo functionality

This architecture provides a solid foundation for maintainable, testable, and extensible code while following established patterns and best practices.
