# Advanced CPU Core Detection and Affinity Selection

## Overview

ThreadPilot now includes advanced CPU topology detection and dynamic core selection capabilities, supporting modern CPU architectures including multi-socket systems, AMD CCD (Core Complex Die), Intel Hybrid (P-core/E-core), and SMT/HyperThreading.

## Features

### 🔍 CPU Topology Detection
- **Automatic Detection**: Uses WMI (Windows Management Instrumentation) to detect CPU topology
- **Fallback Support**: Graceful degradation when advanced topology information is unavailable
- **Real-time Updates**: Event-driven topology detection with UI updates
- **Architecture Support**: 
  - Intel Hybrid (Performance + Efficiency cores)
  - AMD CCD (Core Complex Die) detection
  - SMT/HyperThreading identification
  - Multi-socket systems

### 🎯 Quick Selection Controls
- **All Cores**: Select all available CPU cores
- **Physical Cores Only**: Select only physical cores (excludes hyperthreaded siblings)
- **Performance Cores**: Select Intel P-cores only (Intel Hybrid)
- **Efficiency Cores**: Select Intel E-cores only (Intel Hybrid)
- **CCD Selection**: Select cores from specific CCDs (AMD processors)

### 🖥️ Dynamic UI
- **Adaptive Interface**: CPU core grid adapts to detected topology
- **Visual Indicators**: Color-coded core types and status
- **Topology Status**: Real-time display of detection success/failure
- **Scrollable Grid**: Supports systems with many CPU cores

### ⚡ Power Plan Integration
- **Combined Application**: Apply both CPU affinity and power plan in one action
- **Quick Apply**: Fast reapplication of settings to selected processes
- **System Tray**: Context menu for quick access without opening main window

## Usage

### Basic Usage
1. **Select a Process**: Choose a process from the process list
2. **View Topology**: CPU topology is automatically detected and displayed
3. **Select Cores**: Use quick selection buttons or manually select individual cores
4. **Apply Settings**: Use "Apply Affinity" or "Quick Apply Affinity & Power Plan"

### Quick Selection Buttons
- **All Cores**: Selects all available CPU cores
- **Physical Only**: Selects only physical cores (useful for avoiding HT conflicts)
- **P-Cores**: Selects Intel Performance cores (Intel 12th gen+)
- **E-Cores**: Selects Intel Efficiency cores (Intel 12th gen+)
- **CCD 0/1/2...**: Selects cores from specific AMD CCDs

### System Tray Integration
- **Quick Apply**: Right-click tray icon → "Quick Apply to [ProcessName]"
- **Show Window**: Double-click tray icon or right-click → "Show ThreadPilot"
- **Minimize to Tray**: Window minimizes to system tray instead of taskbar

## Technical Details

### CPU Topology Models
- **CpuTopologyModel**: Main topology container with architecture detection
- **CpuCoreModel**: Individual core representation with type and topology info
- **CpuAffinityPreset**: Pre-configured affinity selections for quick access

### Services
- **ICpuTopologyService**: Interface for CPU topology detection and management
- **CpuTopologyService**: WMI-based implementation with fallback mechanisms
- **ISystemTrayService**: System tray icon and context menu management

### Architecture Detection
- **Intel Hybrid**: Detects P-cores and E-cores using WMI processor information
- **AMD CCD**: Identifies Core Complex Dies for NUMA-aware core selection
- **SMT/HyperThreading**: Distinguishes physical cores from logical threads
- **Multi-socket**: Supports systems with multiple CPU sockets

## Testing

### Test Mode
Run the application with `--test` parameter to execute CPU topology detection tests:
```
ThreadPilot.exe --test
```

### Manual Testing
1. Open ThreadPilot
2. Navigate to Process Management tab
3. Select any process
4. Verify CPU topology detection status
5. Test quick selection buttons
6. Apply affinity settings and verify they take effect

## Troubleshooting

### Topology Detection Failed
- **Cause**: WMI access restrictions or unsupported hardware
- **Solution**: Run as Administrator or use fallback mode
- **Fallback**: Basic core enumeration without advanced features

### Quick Selection Not Working
- **Cause**: No process selected or topology detection failed
- **Solution**: Select a process and ensure topology detection succeeded
- **Check**: Topology status indicator in the UI

### System Tray Not Visible
- **Cause**: Windows Forms package not available or permissions issue
- **Solution**: Ensure System.Windows.Forms package is installed
- **Alternative**: Use main window controls

## Performance Considerations

- **WMI Queries**: Topology detection uses cached results to minimize overhead
- **UI Updates**: Core selection changes are batched for better performance
- **Memory Usage**: Topology models are lightweight and reused across processes
- **Background Detection**: Topology detection runs asynchronously without blocking UI

## Compatibility

- **Windows Version**: Windows 7 or later (WMI support required)
- **CPU Support**: Intel and AMD processors with standard WMI interfaces
- **Architecture**: x64 and ARM64 (where WMI topology data is available)
- **.NET Version**: .NET 9.0 or later

## Future Enhancements

- **NUMA Awareness**: Automatic NUMA node detection and optimization
- **Performance Monitoring**: Real-time CPU usage per core type
- **Profile Management**: Save/load CPU affinity profiles
- **Scheduler Integration**: Windows scheduler hint optimization
- **Advanced Presets**: Custom affinity patterns for specific workloads
