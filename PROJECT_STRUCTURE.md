# ThreadPilot Project Structure

## Overview
This document describes the organized project structure of ThreadPilot, designed for maintainability, scalability, and clear separation of concerns.

## Root Directory Structure

```
ThreadPilot/
├── Services/                          # Service layer with business logic
│   ├── Core/                          # Base interfaces and implementations
│   ├── ProcessManagement/             # Process monitoring and management
│   ├── ServiceConfiguration.cs        # Centralized DI configuration
│   └── ServiceFactory.cs             # Service factory pattern
├── ViewModels/                        # MVVM ViewModels
│   ├── BaseViewModel.cs               # Enhanced base ViewModel
│   └── ViewModelFactory.cs           # ViewModel factory
├── Views/                             # XAML views and code-behind
├── Models/                            # Data models and domain objects
│   └── Core/                          # Base model interfaces
├── Data/                              # Data access layer
│   ├── IRepository.cs                 # Repository interfaces
│   ├── JsonRepository.cs              # JSON file implementation
│   └── DataAccessService.cs          # Centralized data access
├── Converters/                        # Value converters for data binding
├── Helpers/                           # Utility classes and extensions
├── Tests/                             # Unit and integration tests
├── Documentation/                     # Project documentation
│   ├── ARCHITECTURE_GUIDE.md          # Architecture overview
│   ├── DEVELOPER_GUIDE.md             # Development guidelines
│   ├── API_REFERENCE.md               # API documentation
│   └── PROJECT_STRUCTURE.md           # This file
└── Configuration Files
    ├── App.xaml                       # Application configuration
    ├── App.xaml.cs                    # Application startup
    └── ThreadPilot.csproj             # Project file
```

## Service Layer Organization

### Core Services (`Services/Core/`)
**Purpose**: Base interfaces and common functionality for all services.

- `ISystemService.cs` - Base interface with lifecycle management
- `BaseSystemService.cs` - Common service implementation
- `ServiceAvailabilityChangedEventArgs.cs` - Service availability events

### Process Management (`Services/ProcessManagement/`)
**Purpose**: Process monitoring, detection, and management services.

- `IProcessManagementService.cs` - Unified process management interface
- `IProcessMonitorService.cs` - WMI-based process monitoring
- `IProcessMonitorManagerService.cs` - Process monitoring orchestration
- `IGameBoostService.cs` - Game detection and optimization

### Service Infrastructure
- `ServiceConfiguration.cs` - Centralized dependency injection configuration
- `ServiceFactory.cs` - Advanced service creation and lifecycle management

## Data Layer Organization

### Repository Pattern (`Data/`)
**Purpose**: Abstracted data access with consistent patterns.

- `IRepository.cs` - Generic repository interfaces
- `JsonRepository.cs` - JSON file-based implementation
- `IDataAccessService.cs` - Centralized data coordination
- `DataAccessService.cs` - Implementation with validation and backup

### Model Organization (`Models/`)
**Purpose**: Domain models with validation and change tracking.

- `Core/IModel.cs` - Base model interface with validation
- Domain-specific models implementing IModel interface
- Observable properties for UI binding
- Built-in validation and cloning support

## Presentation Layer Organization

### ViewModels (`ViewModels/`)
**Purpose**: MVVM ViewModels with consistent patterns.

- `BaseViewModel.cs` - Enhanced base with error handling and logging
- `ViewModelFactory.cs` - Centralized ViewModel creation and management
- Domain-specific ViewModels inheriting from BaseViewModel
- Proper dependency injection and lifecycle management

### Views (`Views/`)
**Purpose**: XAML views with code-behind.

- Organized by functional area
- Consistent naming conventions
- Proper data binding to ViewModels

## Utility and Support

### Converters (`Converters/`)
**Purpose**: Value converters for data binding.

- Type conversion for UI binding
- Formatting and display logic
- Reusable across multiple views

### Helpers (`Helpers/`)
**Purpose**: Utility classes and extension methods.

- Common functionality
- Extension methods
- Static utility classes

## Configuration and Startup

### Application Configuration
- `App.xaml` - Application-level resources and configuration
- `App.xaml.cs` - Dependency injection setup and application lifecycle
- `ServiceConfiguration.cs` - Centralized service registration

### Project Configuration
- `ThreadPilot.csproj` - Project dependencies and build configuration
- Package references for modern .NET development
- Target framework and platform specifications

## Naming Conventions

### Namespaces
- `ThreadPilot` - Root namespace
- `ThreadPilot.Services` - Service layer
- `ThreadPilot.Services.Core` - Core service infrastructure
- `ThreadPilot.Services.ProcessManagement` - Process-related services
- `ThreadPilot.ViewModels` - Presentation layer ViewModels
- `ThreadPilot.Models` - Data models
- `ThreadPilot.Models.Core` - Base model infrastructure
- `ThreadPilot.Data` - Data access layer

### File Naming
- Interfaces: `I{Name}.cs` (e.g., `IProcessService.cs`)
- Implementations: `{Name}.cs` (e.g., `ProcessService.cs`)
- ViewModels: `{Name}ViewModel.cs` (e.g., `ProcessViewModel.cs`)
- Models: `{Name}Model.cs` (e.g., `ProcessModel.cs`)
- Event Args: `{Name}EventArgs.cs` (e.g., `ProcessEventArgs.cs`)

### Class Organization
- Public interfaces first
- Public classes
- Internal classes
- Private nested classes

## Dependency Flow

### Service Dependencies
```
Application Services → Business Services → Core Services → OS APIs
```

### Presentation Dependencies
```
Views → ViewModels → Services → Models → Data Layer
```

### Data Flow
```
UI Events → ViewModels → Services → Repository → File System
```

## Best Practices Enforced

### Service Layer
- Interface-based design for testability
- Dependency injection for loose coupling
- Async/await patterns throughout
- Comprehensive error handling and logging
- Event-driven communication between services

### Presentation Layer
- MVVM pattern with proper separation
- Command pattern for user interactions
- Observable properties for data binding
- Centralized error handling in BaseViewModel
- Proper disposal and lifecycle management

### Data Layer
- Repository pattern for data abstraction
- Model validation and integrity checking
- Thread-safe operations
- Backup and restore capabilities
- Consistent error handling

This structure provides a solid foundation for maintainable, testable, and extensible code while following established architectural patterns and best practices.
