# ThreadPilot UI Style Guide

## Overview
This document defines the UI standards, terminology, and style guidelines for the ThreadPilot application to ensure consistency across all components and future development.

## Application Structure

### Main Window Layout
- **Title**: "ThreadPilot - Process & Power Plan Manager"
- **Layout**: Grid with TabControl for main content and StatusBar at bottom
- **Tabs**: Organized with emoji icons for visual clarity
  - 🔧 Process Management
  - ⚡ Power Plans  
  - 🔗 Process Associations
  - 📋 Activity Logs
  - ⚙️ Settings

### Status Bar
- Left: General status messages
- Right: Game Boost status with visual indicators (🛡️ when active)

## UI Component Standards

### Buttons
- **Standard Padding**: `10,5` for main action buttons
- **Small Buttons**: `5,2` for utility buttons with `FontSize="10"`
- **Icons**: Use emoji prefixes for visual clarity (🔄 Refresh, ⚙️ Settings)
- **Colors**: 
  - Update/Save: `#007ACC` background, white foreground
  - Remove/Delete: `#D13438` background, white foreground
  - Default: System colors

### GroupBox Headers
- Use emoji prefixes for visual organization
- Examples:
  - 🔍 Process Search & Control
  - ⚡ Available Power Plans
  - 📋 Current Associations
  - ⚙️ Configuration

### Text Input Controls
- **Search boxes**: Width="200" with descriptive tooltips
- **Background**: `#3C3C3C` for dark theme areas
- **Foreground**: White for dark theme areas
- **Border**: `#404040` for dark theme areas

### Data Grids
- **Selection**: Single selection mode
- **Headers**: Column headers visible
- **Grid Lines**: Horizontal only
- **Resizing**: Allow column resizing and sorting

## Color Scheme

### Light Theme (Default)
- **Background**: System default (white/light gray)
- **Foreground**: System default (black/dark gray)
- **Accent**: `#007ACC` (blue)
- **Error**: `#D13438` (red)
- **Success**: System green

### Dark Theme Areas
- **Background**: `#1E1E1E` (main), `#3C3C3C` (controls)
- **Foreground**: White, `#CCCCCC` (secondary text)
- **Border**: `#404040`

## Typography

### Font Sizes
- **Default**: System default
- **Small Controls**: `FontSize="10"` for utility buttons
- **Secondary Text**: `FontSize="12"` for descriptions
- **Headers**: Default with bold weight when active

### Font Weights
- **Active Items**: Bold (using BoolToFontWeightConverter)
- **Normal Items**: Normal weight
- **Secondary Text**: Normal weight

## Icons and Visual Indicators

### Emoji Usage
- 🔧 Process/System Management
- ⚡ Power/Energy related
- 🔗 Connections/Associations  
- 📋 Lists/Logs/Data
- ⚙️ Settings/Configuration
- 🔍 Search functionality
- 🔄 Refresh/Reload
- 🛡️ Game Boost/Protection
- ✅ Success/Enabled
- ❌ Error/Disabled

### Status Indicators
- **Game Boost Active**: Bold text + 🛡️ icon
- **Monitoring Active**: System tray icon changes
- **Error States**: Red text/background
- **Success States**: Green text/background

## Tooltips and Help Text

### Tooltip Standards
- **Buttons**: Describe the action clearly
  - "Refresh the process list"
  - "Apply the selected CPU core affinity to the process"
- **Input Fields**: Describe expected input
  - "Search processes by name"
  - "Enter the executable name (e.g., game.exe)"
- **Checkboxes**: Explain the behavior
  - "Match processes by full path instead of just executable name"

### Help Text Format
- Use clear, concise language
- Avoid technical jargon where possible
- Provide examples when helpful
- Use consistent terminology (see Terminology section)

## Layout Guidelines

### Spacing and Margins
- **Main Container**: `Margin="10"`
- **GroupBox Content**: `Margin="5"`
- **Control Spacing**: `Margin="0,0,10,0"` for horizontal spacing
- **Section Spacing**: `Margin="0,0,0,10"` for vertical spacing

### Grid Organization
- Use GroupBox for logical sections
- Separate related controls visually
- Maintain consistent spacing between elements
- Use separators in context menus

### Responsive Design
- Allow column resizing in data grids
- Use appropriate width constraints
- Ensure controls scale properly

## Terminology Standards

### Process Management
- **Process**: Running application/executable
- **Executable**: The .exe file name
- **Process Name**: Display name of the process
- **CPU Affinity**: Which CPU cores a process can use
- **Priority**: Process execution priority level

### Power Management
- **Power Plan**: Windows power configuration scheme
- **Active Power Plan**: Currently selected power plan
- **Default Power Plan**: Fallback power plan when no processes are running
- **Power Plan Association**: Link between process and power plan

### Game Boost
- **Game Boost**: High-performance mode for games
- **Game Detection**: Automatic identification of game processes
- **Known Games**: Pre-configured list of game executables

### Monitoring
- **Event-based Monitoring**: Real-time WMI process monitoring
- **Fallback Polling**: Backup monitoring method
- **Process Association**: Configured process-to-power plan mapping

## Accessibility Guidelines

### Keyboard Navigation
- Ensure all controls are keyboard accessible
- Provide logical tab order
- Support standard keyboard shortcuts

### Screen Reader Support
- Use descriptive control names
- Provide appropriate ARIA labels where needed
- Ensure status information is announced

### Visual Accessibility
- Maintain sufficient color contrast
- Don't rely solely on color for information
- Provide text alternatives for visual indicators

## Future Development Guidelines

### Adding New Features
1. Follow existing naming conventions
2. Use consistent spacing and layout patterns
3. Add appropriate tooltips and help text
4. Update this style guide with new patterns
5. Test with both light and dark theme areas

### Code Organization
- Keep XAML clean and well-commented
- Use consistent indentation (4 spaces)
- Group related controls logically
- Use meaningful names for controls that need code-behind access

### Testing Considerations
- Test all UI changes with different screen sizes
- Verify tooltip text is helpful and accurate
- Ensure consistent behavior across all tabs
- Test keyboard navigation paths

## Version History
- **v1.0** (2025-01-28): Initial style guide creation
- Covers current UI implementation with logging, game boost, and process monitoring features
