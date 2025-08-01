using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using ThreadPilot.Models;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    public partial class MainWindowViewModel : BaseViewModel
    {
        private readonly IGameBoostService? _gameBoostService;
        private readonly IProcessMonitorManagerService? _processMonitorManagerService;
        private readonly INotificationService? _notificationService;
        private readonly IElevationService? _elevationService;
        private readonly ISecurityService? _securityService;

        [ObservableProperty]
        private bool isGameBoostActive = false;

        [ObservableProperty]
        private string gameBoostStatusText = "Game Boost: Inactive";

        [ObservableProperty]
        private string? currentGameName = null;

        [ObservableProperty]
        private bool isProcessMonitoringActive = false;

        [ObservableProperty]
        private string processMonitoringStatusText = "Process Monitoring: Inactive";

        [ObservableProperty]
        private bool isRunningAsAdministrator = false;

        [ObservableProperty]
        private string elevationStatusText = "Checking elevation status...";

        [ObservableProperty]
        private bool showElevationPrompt = false;

        public MainWindowViewModel(
            ILogger<MainWindowViewModel> logger,
            IEnhancedLoggingService? enhancedLoggingService = null,
            IGameBoostService? gameBoostService = null,
            IProcessMonitorManagerService? processMonitorManagerService = null,
            INotificationService? notificationService = null,
            IElevationService? elevationService = null,
            ISecurityService? securityService = null)
            : base(logger, enhancedLoggingService)
        {
            _gameBoostService = gameBoostService;
            _processMonitorManagerService = processMonitorManagerService;
            _notificationService = notificationService;
            _elevationService = elevationService;
            _securityService = securityService;
        }

        public override async Task InitializeAsync()
        {
            await ExecuteAsync(async () =>
            {
                // Subscribe to service events
                if (_gameBoostService != null)
                {
                    _gameBoostService.GameBoostActivated += OnGameBoostActivated;
                    _gameBoostService.GameBoostDeactivated += OnGameBoostDeactivated;
                }

                if (_processMonitorManagerService != null)
                {
                    _processMonitorManagerService.ServiceStatusChanged += OnServiceStatusChanged;
                }

                // Initialize status
                await UpdateStatusAsync();
                UpdateElevationStatus();

                await LogUserActionAsync("MainWindow", "Initialized main window", "Application startup");
            }, "Initializing main window...");
        }

        [RelayCommand]
        private async Task ToggleProcessMonitoringAsync()
        {
            if (_processMonitorManagerService == null) return;

            await ExecuteAsync(async () =>
            {
                if (IsProcessMonitoringActive)
                {
                    await _processMonitorManagerService.StopAsync();
                    await LogUserActionAsync("ProcessMonitoring", "Stopped process monitoring", "User action");
                }
                else
                {
                    await _processMonitorManagerService.StartAsync();
                    await LogUserActionAsync("ProcessMonitoring", "Started process monitoring", "User action");
                }
            }, IsProcessMonitoringActive ? "Stopping monitoring..." : "Starting monitoring...");
        }

        [RelayCommand]
        private async Task ToggleGameBoostAsync()
        {
            if (_gameBoostService == null) return;

            await ExecuteAsync(async () =>
            {
                if (IsGameBoostActive)
                {
                    await _gameBoostService.DeactivateGameBoostAsync();
                    await LogUserActionAsync("GameBoost", "Deactivated game boost", "User action");
                }
                else
                {
                    // Game boost is typically activated automatically, but we can force it
                    await LogUserActionAsync("GameBoost", "Attempted to activate game boost", "User action");
                }
            }, "Toggling game boost...");
        }

        [RelayCommand]
        private async Task RequestElevationAsync()
        {
            if (_elevationService == null) return;

            await ExecuteAsync(async () =>
            {
                var success = await _elevationService.RequestElevationIfNeeded();
                if (success)
                {
                    await LogUserActionAsync("Elevation", "Requested elevation", "User action");
                }
                else
                {
                    await LogUserActionAsync("Elevation", "Elevation request failed or cancelled", "User action");
                }
            }, "Requesting elevation...");
        }

        private async Task UpdateStatusAsync()
        {
            try
            {
                // Update game boost status
                if (_gameBoostService != null)
                {
                    IsGameBoostActive = _gameBoostService.IsGameBoostActive;
                    CurrentGameName = _gameBoostService.CurrentGameProcess?.Name;
                    UpdateGameBoostStatusText();
                }

                // Update process monitoring status
                if (_processMonitorManagerService != null)
                {
                    IsProcessMonitoringActive = _processMonitorManagerService.IsRunning;
                    ProcessMonitoringStatusText = IsProcessMonitoringActive
                        ? "Process Monitoring: Active"
                        : "Process Monitoring: Inactive";
                }

                // Update elevation status
                UpdateElevationStatus();
            }
            catch (Exception ex)
            {
                SetError("Failed to update status", ex);
            }
        }

        private void UpdateElevationStatus()
        {
            if (_elevationService == null)
            {
                IsRunningAsAdministrator = false;
                ElevationStatusText = "Elevation service not available";
                ShowElevationPrompt = false;
                return;
            }

            IsRunningAsAdministrator = _elevationService.IsRunningAsAdministrator();
            ElevationStatusText = _elevationService.GetElevationStatus();
            ShowElevationPrompt = !IsRunningAsAdministrator;
        }

        private void UpdateGameBoostStatusText()
        {
            if (IsGameBoostActive && !string.IsNullOrEmpty(CurrentGameName))
            {
                GameBoostStatusText = $"Game Boost: Active ({CurrentGameName})";
            }
            else if (IsGameBoostActive)
            {
                GameBoostStatusText = "Game Boost: Active";
            }
            else
            {
                GameBoostStatusText = "Game Boost: Inactive";
            }
        }

        public void UpdateGameBoostStatus(bool isActive, string? gameName = null)
        {
            IsGameBoostActive = isActive;
            CurrentGameName = gameName;
            UpdateGameBoostStatusText();
        }

        private async void OnGameBoostActivated(object? sender, GameBoostActivatedEventArgs e)
        {
            // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsGameBoostActive = true;
                CurrentGameName = e.GameProcess?.Name;
                UpdateGameBoostStatusText();
            });

            if (_notificationService != null)
            {
                await _notificationService.ShowNotificationAsync(
                    "Game Boost Activated",
                    $"Performance boost enabled for {CurrentGameName}",
                    NotificationType.Information);
            }
        }

        private async void OnGameBoostDeactivated(object? sender, GameBoostDeactivatedEventArgs e)
        {
            // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsGameBoostActive = false;
                CurrentGameName = null;
                UpdateGameBoostStatusText();
            });

            if (_notificationService != null)
            {
                await _notificationService.ShowNotificationAsync(
                    "Game Boost Deactivated",
                    "Performance boost disabled",
                    NotificationType.Information);
            }
        }

        private void OnServiceStatusChanged(object? sender, ServiceStatusEventArgs e)
        {
            // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsProcessMonitoringActive = e.IsRunning;
                ProcessMonitoringStatusText = $"Process Monitoring: {e.Status}";
            });
        }

        public void UpdateProcessMonitoringStatus(bool isActive, string status)
        {
            IsProcessMonitoringActive = isActive;
            ProcessMonitoringStatusText = $"Process Monitoring: {status}";
        }

        protected override void OnDispose()
        {
            // Unsubscribe from events
            if (_gameBoostService != null)
            {
                _gameBoostService.GameBoostActivated -= OnGameBoostActivated;
                _gameBoostService.GameBoostDeactivated -= OnGameBoostDeactivated;
            }

            if (_processMonitorManagerService != null)
            {
                _processMonitorManagerService.ServiceStatusChanged -= OnServiceStatusChanged;
            }

            base.OnDispose();
        }
    }
}