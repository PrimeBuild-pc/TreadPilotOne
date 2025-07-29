using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing system tray icon and context menu
    /// </summary>
    public class SystemTrayService : ISystemTrayService
    {
        private readonly ILogger<SystemTrayService> _logger;
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private ToolStripMenuItem? _quickApplyMenuItem;
        private ToolStripMenuItem? _selectedProcessMenuItem;
        private ToolStripMenuItem? _monitoringToggleMenuItem;
        private ToolStripMenuItem? _gameBoostStatusMenuItem;
        private ToolStripMenuItem? _settingsMenuItem;
        private ApplicationSettingsModel _settings;
        private bool _isMonitoring = true;
        private bool _isWmiAvailable = true;
        private bool _isGameBoostActive = false;
        private string? _currentGameName = null;
        private TrayIconState _currentIconState = TrayIconState.Normal;
        private bool _disposed = false;

        public event EventHandler? QuickApplyRequested;
        public event EventHandler? ShowMainWindowRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler<MonitoringToggleEventArgs>? MonitoringToggleRequested;
        public event EventHandler? SettingsRequested;

        public SystemTrayService(ILogger<SystemTrayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = new ApplicationSettingsModel(); // Default settings
        }

        public void Initialize()
        {
            try
            {
                _logger.LogInformation("Initializing system tray service");

                // Create the notify icon
                _notifyIcon = new NotifyIcon
                {
                    Text = "ThreadPilot - Process & Power Plan Manager",
                    Visible = false
                };

                // Try to load icon from resources or use default
                try
                {
                    // For now, use a simple system icon
                    _notifyIcon.Icon = SystemIcons.Application;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load tray icon, using default");
                    _notifyIcon.Icon = SystemIcons.Application;
                }

                // Create context menu
                CreateContextMenu();

                // Set up event handlers
                _notifyIcon.DoubleClick += OnTrayIconDoubleClick;
                _notifyIcon.ContextMenuStrip = _contextMenu;

                // Set initial icon state
                UpdateTrayIcon(TrayIconState.Normal);

                _logger.LogInformation("System tray service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize system tray service");
                throw;
            }
        }

        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            // Selected process info (disabled, for display only)
            _selectedProcessMenuItem = new ToolStripMenuItem("No process selected")
            {
                Enabled = false,
                Font = new Font(_contextMenu.Font, FontStyle.Bold)
            };
            _contextMenu.Items.Add(_selectedProcessMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Quick apply command
            _quickApplyMenuItem = new ToolStripMenuItem("Quick Apply Affinity & Power Plan")
            {
                Enabled = false
            };
            _quickApplyMenuItem.Click += OnQuickApplyClick;
            _contextMenu.Items.Add(_quickApplyMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Monitoring toggle
            _monitoringToggleMenuItem = new ToolStripMenuItem("Disable Process Monitoring");
            _monitoringToggleMenuItem.Click += OnMonitoringToggleClick;
            _contextMenu.Items.Add(_monitoringToggleMenuItem);

            // Game Boost status (disabled, for display only)
            _gameBoostStatusMenuItem = new ToolStripMenuItem("Game Boost: Inactive")
            {
                Enabled = false,
                Font = new Font(_contextMenu.Font, FontStyle.Italic)
            };
            _contextMenu.Items.Add(_gameBoostStatusMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Settings
            _settingsMenuItem = new ToolStripMenuItem("Settings...");
            _settingsMenuItem.Click += OnSettingsClick;
            _contextMenu.Items.Add(_settingsMenuItem);

            // Show main window
            var showMenuItem = new ToolStripMenuItem("Show ThreadPilot");
            showMenuItem.Click += OnShowMainWindowClick;
            _contextMenu.Items.Add(showMenuItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += OnExitClick;
            _contextMenu.Items.Add(exitMenuItem);
        }

        public void Show()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
                _logger.LogDebug("System tray icon shown");
            }
        }

        public void Hide()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _logger.LogDebug("System tray icon hidden");
            }
        }

        public void UpdateTooltip(string tooltip)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip;
            }
        }

        public void ShowBalloonTip(string title, string text, int timeoutMs = 3000)
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(timeoutMs, title, text, ToolTipIcon.Info);
            }
        }

        public void UpdateContextMenu(string? selectedProcessName = null, bool hasSelection = false)
        {
            if (_selectedProcessMenuItem == null || _quickApplyMenuItem == null) return;

            if (hasSelection && !string.IsNullOrEmpty(selectedProcessName))
            {
                _selectedProcessMenuItem.Text = $"Selected: {selectedProcessName}";
                _quickApplyMenuItem.Enabled = true;
                _quickApplyMenuItem.Text = $"Quick Apply to {selectedProcessName}";
            }
            else
            {
                _selectedProcessMenuItem.Text = "No process selected";
                _quickApplyMenuItem.Enabled = false;
                _quickApplyMenuItem.Text = "Quick Apply Affinity & Power Plan";
            }
        }

        private void OnTrayIconDoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnQuickApplyClick(object? sender, EventArgs e)
        {
            QuickApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnShowMainWindowClick(object? sender, EventArgs e)
        {
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnMonitoringToggleClick(object? sender, EventArgs e)
        {
            _isMonitoring = !_isMonitoring;
            MonitoringToggleRequested?.Invoke(this, new MonitoringToggleEventArgs(_isMonitoring));
            UpdateMonitoringStatus(_isMonitoring, _isWmiAvailable);
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateMonitoringStatus(bool isMonitoring, bool isWmiAvailable = true)
        {
            _isMonitoring = isMonitoring;
            _isWmiAvailable = isWmiAvailable;

            if (_monitoringToggleMenuItem != null)
            {
                _monitoringToggleMenuItem.Text = isMonitoring ? "Disable Process Monitoring" : "Enable Process Monitoring";
                _monitoringToggleMenuItem.Enabled = isWmiAvailable;
            }

            // Update tray icon state
            var iconState = !isWmiAvailable ? TrayIconState.Error :
                           isMonitoring ? TrayIconState.Monitoring : TrayIconState.Disabled;
            UpdateTrayIcon(iconState);

            // Update tooltip
            var status = !isWmiAvailable ? "WMI Error" :
                        isMonitoring ? "Monitoring Active" : "Monitoring Disabled";
            UpdateTooltip($"ThreadPilot - {status}");
        }

        public void UpdateTrayIcon(TrayIconState state)
        {
            if (_notifyIcon == null) return;

            _currentIconState = state;

            try
            {
                // For now, use different system icons to represent states
                // In a real implementation, you might want custom icons
                _notifyIcon.Icon = state switch
                {
                    TrayIconState.Normal => SystemIcons.Application,
                    TrayIconState.Monitoring => SystemIcons.Information,
                    TrayIconState.Error => SystemIcons.Error,
                    TrayIconState.Disabled => SystemIcons.Warning,
                    TrayIconState.GameBoost => SystemIcons.Shield, // Use shield icon for Game Boost
                    _ => SystemIcons.Application
                };

                _logger.LogDebug("Tray icon updated to state: {State}", state);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update tray icon");
            }
        }

        public void ShowTrayNotification(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000)
        {
            if (_notifyIcon == null || !_settings.EnableBalloonNotifications) return;

            try
            {
                var balloonIcon = type switch
                {
                    NotificationType.Error => ToolTipIcon.Error,
                    NotificationType.Warning => ToolTipIcon.Warning,
                    NotificationType.Success => ToolTipIcon.Info,
                    _ => ToolTipIcon.Info
                };

                _notifyIcon.ShowBalloonTip(timeoutMs, title, message, balloonIcon);
                _logger.LogDebug("Balloon tip shown: {Title}", title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing balloon tip");
            }
        }

        public void UpdateSettings(ApplicationSettingsModel settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Update tray icon visibility
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = settings.ShowTrayIcon;
            }

            _logger.LogDebug("Tray service settings updated");
        }

        public void UpdateGameBoostStatus(bool isGameBoostActive, string? currentGameName = null)
        {
            _isGameBoostActive = isGameBoostActive;
            _currentGameName = currentGameName;

            // Update Game Boost status menu item
            if (_gameBoostStatusMenuItem != null)
            {
                if (isGameBoostActive && !string.IsNullOrEmpty(currentGameName))
                {
                    _gameBoostStatusMenuItem.Text = $"Game Boost: Active ({currentGameName})";
                    _gameBoostStatusMenuItem.Font = new Font(_gameBoostStatusMenuItem.Font, FontStyle.Bold);
                }
                else if (isGameBoostActive)
                {
                    _gameBoostStatusMenuItem.Text = "Game Boost: Active";
                    _gameBoostStatusMenuItem.Font = new Font(_gameBoostStatusMenuItem.Font, FontStyle.Bold);
                }
                else
                {
                    _gameBoostStatusMenuItem.Text = "Game Boost: Inactive";
                    _gameBoostStatusMenuItem.Font = new Font(_gameBoostStatusMenuItem.Font, FontStyle.Italic);
                }
            }

            // Update tray icon state - Game Boost takes priority over monitoring state
            var iconState = !_isWmiAvailable ? TrayIconState.Error :
                           isGameBoostActive ? TrayIconState.GameBoost :
                           _isMonitoring ? TrayIconState.Monitoring : TrayIconState.Disabled;
            UpdateTrayIcon(iconState);

            // Update tooltip
            var status = !_isWmiAvailable ? "WMI Error" :
                        isGameBoostActive ? $"Game Boost Active{(!string.IsNullOrEmpty(currentGameName) ? $" - {currentGameName}" : "")}" :
                        _isMonitoring ? "Monitoring Active" : "Monitoring Disabled";
            UpdateTooltip($"ThreadPilot - {status}");

            _logger.LogDebug("Game Boost status updated: Active={IsActive}, Game={GameName}", isGameBoostActive, currentGameName);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _logger.LogInformation("Disposing system tray service");

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }

                if (_contextMenu != null)
                {
                    _contextMenu.Dispose();
                    _contextMenu = null;
                }

                _disposed = true;
                _logger.LogInformation("System tray service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing system tray service");
            }
        }
    }
}
