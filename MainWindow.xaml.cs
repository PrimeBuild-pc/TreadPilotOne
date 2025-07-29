using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ThreadPilot.ViewModels;
using ThreadPilot.Services;
using ThreadPilot.Views;
using ThreadPilot.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace ThreadPilot
{
    public partial class MainWindow : Window
    {
        private readonly ProcessViewModel _processViewModel;
        private readonly PowerPlanViewModel _powerPlanViewModel;
        private readonly ProcessPowerPlanAssociationViewModel _associationViewModel;
        private readonly ISystemTrayService _systemTrayService;
        private readonly IApplicationSettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly IProcessMonitorService _processMonitorService;
        private readonly IProcessMonitorManagerService _processMonitorManagerService;
        private readonly IGameBoostService _gameBoostService;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly MainWindowViewModel _mainWindowViewModel;
        private readonly IServiceProvider _serviceProvider;

        public MainWindow(
            ProcessViewModel processViewModel,
            PowerPlanViewModel powerPlanViewModel,
            ProcessPowerPlanAssociationViewModel associationViewModel,
            ISystemTrayService systemTrayService,
            IApplicationSettingsService settingsService,
            INotificationService notificationService,
            IProcessMonitorService processMonitorService,
            IProcessMonitorManagerService processMonitorManagerService,
            IGameBoostService gameBoostService,
            SettingsViewModel settingsViewModel,
            MainWindowViewModel mainWindowViewModel,
            IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _processViewModel = processViewModel;
            _powerPlanViewModel = powerPlanViewModel;
            _associationViewModel = associationViewModel;
            _systemTrayService = systemTrayService;
            _settingsService = settingsService;
            _notificationService = notificationService;
            _processMonitorService = processMonitorService;
            _processMonitorManagerService = processMonitorManagerService;
            _gameBoostService = gameBoostService;
            _settingsViewModel = settingsViewModel;
            _mainWindowViewModel = mainWindowViewModel;
            _serviceProvider = serviceProvider;

            SetDataContexts();
            LoadViewModels();
            InitializeServices();
            SetupTestKeyBinding();
        }

        private void SetDataContexts()
        {
            // Set DataContext for the main window
            DataContext = _mainWindowViewModel;

            // Set DataContext for the association view
            AssociationView.DataContext = _associationViewModel;
        }

        private async void LoadViewModels()
        {
            await _processViewModel.LoadProcesses();
            await _powerPlanViewModel.LoadPowerPlans();
            // The association view model loads its data automatically in its constructor
        }

        private async void InitializeServices()
        {
            await InitializeSettingsAsync();
            InitializeSystemTray();
            InitializeNotifications();
            InitializeGameBoost();
            await InitializeMonitoringAsync();
            await StartProcessMonitoringManagerAsync();
        }

        private async Task InitializeSettingsAsync()
        {
            try
            {
                await _settingsService.LoadSettingsAsync();

                // Apply initial settings
                var settings = _settingsService.Settings;
                if (settings.StartMinimized)
                {
                    WindowState = WindowState.Minimized;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }

        private void InitializeSystemTray()
        {
            try
            {
                _systemTrayService.Initialize();
                _systemTrayService.Show();

                // Subscribe to tray events
                _systemTrayService.ShowMainWindowRequested += OnShowMainWindowRequested;
                _systemTrayService.ExitRequested += OnExitRequested;
                _systemTrayService.MonitoringToggleRequested += OnMonitoringToggleRequested;
                _systemTrayService.SettingsRequested += OnSettingsRequested;

                // Update settings and tooltip
                _systemTrayService.UpdateSettings(_settingsService.Settings);
                _systemTrayService.UpdateTooltip("ThreadPilot - Process & Power Plan Manager");
            }
            catch (Exception ex)
            {
                // Log error but don't fail startup
                System.Diagnostics.Debug.WriteLine($"Failed to initialize system tray: {ex.Message}");
            }
        }

        private void OnShowMainWindowRequested(object? sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private async void OnMonitoringToggleRequested(object? sender, MonitoringToggleEventArgs e)
        {
            try
            {
                if (e.EnableMonitoring)
                {
                    await _processMonitorManagerService.StartAsync();
                    await _notificationService.ShowSuccessNotificationAsync(
                        "Monitoring Enabled",
                        "Process monitoring and power plan management has been enabled");
                }
                else
                {
                    await _processMonitorManagerService.StopAsync();
                    await _notificationService.ShowNotificationAsync(
                        "Monitoring Disabled",
                        "Process monitoring and power plan management has been disabled",
                        Models.NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                await _notificationService.ShowErrorNotificationAsync(
                    "Monitoring Error",
                    "Failed to toggle process monitoring",
                    ex);
            }
        }

        private void OnSettingsRequested(object? sender, EventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow(_settingsViewModel);
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open settings: {ex.Message}");
            }
        }

        private void InitializeNotifications()
        {
            try
            {
                // Subscribe to settings changes to update notification service
                _settingsService.SettingsChanged += OnSettingsChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize notifications: {ex.Message}");
            }
        }

        private void InitializeGameBoost()
        {
            try
            {
                // Subscribe to Game Boost events
                _gameBoostService.GameBoostActivated += OnGameBoostActivated;
                _gameBoostService.GameBoostDeactivated += OnGameBoostDeactivated;

                // Update initial Game Boost status in tray and main window
                _systemTrayService.UpdateGameBoostStatus(
                    _gameBoostService.IsGameBoostActive,
                    _gameBoostService.CurrentGameProcess?.Name);
                _mainWindowViewModel.UpdateGameBoostStatus(
                    _gameBoostService.IsGameBoostActive,
                    _gameBoostService.CurrentGameProcess?.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize Game Boost: {ex.Message}");
            }
        }

        private async Task InitializeMonitoringAsync()
        {
            try
            {
                // Subscribe to monitoring status changes
                _processMonitorService.MonitoringStatusChanged += OnMonitoringStatusChanged;

                // Update tray with initial monitoring status
                _systemTrayService.UpdateMonitoringStatus(
                    _processMonitorService.IsMonitoring,
                    _processMonitorService.IsWmiAvailable);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize monitoring: {ex.Message}");
            }
        }

        private async Task StartProcessMonitoringManagerAsync()
        {
            try
            {
                // Subscribe to process monitor manager events
                _processMonitorManagerService.ServiceStatusChanged += OnProcessMonitorManagerStatusChanged;

                // Start the process monitoring manager service
                await _processMonitorManagerService.StartAsync();

                await _notificationService.ShowSuccessNotificationAsync(
                    "ThreadPilot Started",
                    "Process monitoring and power plan management is now active");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start process monitoring manager: {ex.Message}");
                await _notificationService.ShowErrorNotificationAsync(
                    "Startup Error",
                    "Failed to start process monitoring manager",
                    ex);
            }
        }

        private void OnSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            // Update tray service with new settings
            _systemTrayService.UpdateSettings(e.NewSettings);
        }

        private void OnMonitoringStatusChanged(object? sender, MonitoringStatusEventArgs e)
        {
            // Update tray icon and status
            _systemTrayService.UpdateMonitoringStatus(e.IsMonitoring, e.IsWmiAvailable);

            // Show notification if there's an error
            if (e.Error != null && _settingsService.Settings.EnableErrorNotifications)
            {
                _notificationService.ShowErrorNotificationAsync(
                    "Monitoring Error",
                    e.StatusMessage ?? "An error occurred with process monitoring",
                    e.Error);
            }
        }

        private void OnProcessMonitorManagerStatusChanged(object? sender, ServiceStatusEventArgs e)
        {
            // Update main window status
            _mainWindowViewModel.UpdateProcessMonitoringStatus(e.IsRunning, e.Status);

            // Show notification for critical status changes
            if (!e.IsRunning && e.Error != null && _settingsService.Settings.EnableErrorNotifications)
            {
                _notificationService.ShowErrorNotificationAsync(
                    "Process Monitoring Error",
                    e.Details ?? "Process monitoring manager encountered an error",
                    e.Error);
            }
        }

        private void OnGameBoostActivated(object? sender, GameBoostActivatedEventArgs e)
        {
            try
            {
                // Update tray with Game Boost active status
                _systemTrayService.UpdateGameBoostStatus(true, e.GameProcess.Name);

                // Update main window status
                _mainWindowViewModel.UpdateGameBoostStatus(true, e.GameProcess.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling Game Boost activation: {ex.Message}");
            }
        }

        private void OnGameBoostDeactivated(object? sender, GameBoostDeactivatedEventArgs e)
        {
            try
            {
                // Update tray with Game Boost inactive status
                _systemTrayService.UpdateGameBoostStatus(false);

                // Update main window status
                _mainWindowViewModel.UpdateGameBoostStatus(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling Game Boost deactivation: {ex.Message}");
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            try
            {
                if (WindowState == WindowState.Minimized && _settingsService.Settings.MinimizeToTray)
                {
                    Hide();
                    _systemTrayService.Show();

                    // Pause process refresh when minimized to reduce resource usage
                    if (_processViewModel != null)
                    {
                        _processViewModel.PauseRefresh();
                    }
                }
                else if (WindowState == WindowState.Normal)
                {
                    // Resume process refresh when restored
                    if (_processViewModel != null)
                    {
                        _processViewModel.ResumeRefresh();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling window state change: {ex.Message}");
            }

            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Check settings for close behavior
            if (_settingsService.Settings.CloseToTray)
            {
                // Minimize to tray instead of closing
                e.Cancel = true;
                WindowState = WindowState.Minimized;
            }
            else
            {
                // Actually close the application
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void SetupTestKeyBinding()
        {
            // Add Ctrl+Shift+T to run Game Boost tests
            var testCommand = new RoutedCommand();
            var testBinding = new KeyBinding(testCommand, Key.T, ModifierKeys.Control | ModifierKeys.Shift);
            InputBindings.Add(testBinding);
            CommandBindings.Add(new CommandBinding(testCommand, RunGameBoostTests));
        }

        private async void RunGameBoostTests(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                _mainWindowViewModel.StatusMessage = "Running Game Boost integration tests...";

                // Validate service configuration first
                if (!ThreadPilot.Tests.TestRunner.ValidateServiceConfiguration(_serviceProvider))
                {
                    _mainWindowViewModel.StatusMessage = "Service configuration validation failed";
                    await _notificationService.ShowErrorNotificationAsync(
                        "Test Failed",
                        "Service configuration validation failed");
                    return;
                }

                // Run integration tests
                var testResult = await ThreadPilot.Tests.TestRunner.RunGameBoostTestsAsync(_serviceProvider);

                if (testResult)
                {
                    _mainWindowViewModel.StatusMessage = "All Game Boost tests passed successfully!";
                    await _notificationService.ShowSuccessNotificationAsync(
                        "Tests Passed",
                        "All Game Boost integration tests completed successfully");
                }
                else
                {
                    _mainWindowViewModel.StatusMessage = "Game Boost tests failed - check logs for details";
                    await _notificationService.ShowErrorNotificationAsync(
                        "Tests Failed",
                        "One or more Game Boost tests failed - check logs for details");
                }
            }
            catch (Exception ex)
            {
                _mainWindowViewModel.StatusMessage = $"Test execution failed: {ex.Message}";
                await _notificationService.ShowErrorNotificationAsync(
                    "Test Error",
                    "Failed to execute Game Boost tests",
                    ex);
            }
        }
    }
}