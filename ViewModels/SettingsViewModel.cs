using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for application settings
    /// </summary>
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IApplicationSettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly IAutostartService _autostartService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly IGameBoostService _gameBoostService;
        private readonly IProcessMonitorManagerService _processMonitorManagerService;

        [ObservableProperty]
        private ApplicationSettingsModel settings;

        [ObservableProperty]
        private bool hasUnsavedChanges = false;

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> availablePowerPlans = new();

        [ObservableProperty]
        private ObservableCollection<string> knownGameExecutables = new();

        [ObservableProperty]
        private string newGameExecutableName = string.Empty;

        [ObservableProperty]
        private string? selectedKnownGame;

        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }
        public ICommand ExportSettingsCommand { get; }
        public ICommand ImportSettingsCommand { get; }
        public ICommand TestNotificationCommand { get; }
        public ICommand RefreshPowerPlansCommand { get; }
        public ICommand AddKnownGameCommand { get; }
        public ICommand RemoveKnownGameCommand { get; }

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            IApplicationSettingsService settingsService,
            INotificationService notificationService,
            IAutostartService autostartService,
            IPowerPlanService powerPlanService,
            IGameBoostService gameBoostService,
            IProcessMonitorManagerService processMonitorManagerService,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _autostartService = autostartService ?? throw new ArgumentNullException(nameof(autostartService));
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _gameBoostService = gameBoostService ?? throw new ArgumentNullException(nameof(gameBoostService));
            _processMonitorManagerService = processMonitorManagerService ?? throw new ArgumentNullException(nameof(processMonitorManagerService));

            // Initialize with current settings
            settings = (ApplicationSettingsModel)_settingsService.Settings.Clone();

            // Initialize commands
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            ResetToDefaultsCommand = new AsyncRelayCommand(ResetToDefaultsAsync);
            ExportSettingsCommand = new AsyncRelayCommand(ExportSettingsAsync);
            ImportSettingsCommand = new AsyncRelayCommand(ImportSettingsAsync);
            TestNotificationCommand = new AsyncRelayCommand(TestNotificationAsync);
            RefreshPowerPlansCommand = new AsyncRelayCommand(RefreshPowerPlansAsync);
            AddKnownGameCommand = new AsyncRelayCommand(AddKnownGameAsync, CanAddKnownGame);
            RemoveKnownGameCommand = new AsyncRelayCommand(RemoveKnownGameAsync, CanRemoveKnownGame);

            // Subscribe to property changes to track unsaved changes
            Settings.PropertyChanged += OnSettingsPropertyChanged;
            PropertyChanged += OnViewModelPropertyChanged;

            // Initialize data - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshPowerPlansAsync());
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await LoadKnownGamesAsync());

            Logger.LogInformation("Settings ViewModel initialized");
        }

        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            HasUnsavedChanges = true;
            StatusMessage = "Settings have been modified";
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NewGameExecutableName))
            {
                ((AsyncRelayCommand)AddKnownGameCommand).NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(SelectedKnownGame))
            {
                ((AsyncRelayCommand)RemoveKnownGameCommand).NotifyCanExecuteChanged();
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Saving settings...";

                // Handle autostart setting
                if (Settings.AutostartWithWindows != _settingsService.Settings.AutostartWithWindows)
                {
                    if (Settings.AutostartWithWindows)
                    {
                        await _autostartService.EnableAutostartAsync(Settings.StartMinimized);
                    }
                    else
                    {
                        await _autostartService.DisableAutostartAsync();
                    }
                }

                await _settingsService.UpdateSettingsAsync(Settings);

                // Update monitoring services with new settings
                _processMonitorManagerService.UpdateSettings();

                HasUnsavedChanges = false;
                StatusMessage = "Settings saved successfully";

                await _notificationService.ShowSuccessNotificationAsync(
                    "Settings Saved", 
                    "Application settings have been saved successfully");

                Logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
                Logger.LogError(ex, "Error saving settings");

                await _notificationService.ShowErrorNotificationAsync(
                    "Settings Error", 
                    "Failed to save settings", 
                    ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResetToDefaultsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Resetting to defaults...";

                var defaultSettings = new ApplicationSettingsModel();
                Settings.CopyFrom(defaultSettings);

                HasUnsavedChanges = true;
                StatusMessage = "Settings reset to defaults (not saved yet)";

                Logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error resetting settings: {ex.Message}";
                Logger.LogError(ex, "Error resetting settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Exporting settings...";

                // In a real implementation, you would show a file dialog
                var exportPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"ThreadPilot_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                await _settingsService.ExportSettingsAsync(exportPath);

                StatusMessage = $"Settings exported to: {exportPath}";

                await _notificationService.ShowSuccessNotificationAsync(
                    "Settings Exported", 
                    $"Settings exported to {System.IO.Path.GetFileName(exportPath)}");

                Logger.LogInformation("Settings exported to {Path}", exportPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting settings: {ex.Message}";
                Logger.LogError(ex, "Error exporting settings");

                await _notificationService.ShowErrorNotificationAsync(
                    "Export Error", 
                    "Failed to export settings", 
                    ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Importing settings...";

                // In a real implementation, you would show a file dialog
                // For now, we'll just show a message
                StatusMessage = "Import feature requires file dialog implementation";

                Logger.LogInformation("Import settings requested");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing settings: {ex.Message}";
                Logger.LogError(ex, "Error importing settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task TestNotificationAsync()
        {
            try
            {
                await _notificationService.ShowNotificationAsync(
                    "Test Notification", 
                    "This is a test notification to verify your settings are working correctly.",
                    NotificationType.Information);

                StatusMessage = "Test notification sent";
                Logger.LogInformation("Test notification sent");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sending test notification: {ex.Message}";
                Logger.LogError(ex, "Error sending test notification");
            }
        }

        /// <summary>
        /// Refreshes settings from the service
        /// </summary>
        public async Task RefreshSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading settings...";

                await _settingsService.LoadSettingsAsync();
                Settings.CopyFrom(_settingsService.Settings);

                HasUnsavedChanges = false;
                StatusMessage = "Settings loaded";

                Logger.LogInformation("Settings refreshed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
                Logger.LogError(ex, "Error loading settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks if there are unsaved changes
        /// </summary>
        public bool CanClose()
        {
            return !HasUnsavedChanges;
        }

        private async Task RefreshPowerPlansAsync()
        {
            try
            {
                var powerPlans = await _powerPlanService.GetPowerPlansAsync();

                AvailablePowerPlans.Clear();
                foreach (var plan in powerPlans)
                {
                    AvailablePowerPlans.Add(plan);
                }

                Logger.LogDebug("Refreshed {Count} power plans", AvailablePowerPlans.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh power plans");
            }
        }

        /// <summary>
        /// Loads known games from the GameBoostService
        /// </summary>
        private async Task LoadKnownGamesAsync()
        {
            try
            {
                var knownGames = _gameBoostService.GetKnownGameExecutables();
                KnownGameExecutables.Clear();
                foreach (var game in knownGames)
                {
                    KnownGameExecutables.Add(game);
                }
                Logger.LogDebug("Loaded {Count} known games", KnownGameExecutables.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading known games");
            }
        }

        /// <summary>
        /// Adds a new known game executable
        /// </summary>
        private async Task AddKnownGameAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NewGameExecutableName))
                    return;

                var success = await _gameBoostService.AddKnownGameAsync(NewGameExecutableName);
                if (success)
                {
                    await LoadKnownGamesAsync();
                    var gameName = NewGameExecutableName;
                    NewGameExecutableName = string.Empty;
                    StatusMessage = $"Added game: {gameName}";
                    Logger.LogInformation("Added known game: {GameName}", gameName);
                }
                else
                {
                    StatusMessage = "Game already exists or invalid name";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding game: {ex.Message}";
                Logger.LogError(ex, "Error adding known game: {GameName}", NewGameExecutableName);
            }
        }

        /// <summary>
        /// Removes the selected known game executable
        /// </summary>
        private async Task RemoveKnownGameAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SelectedKnownGame))
                    return;

                var gameName = SelectedKnownGame;
                var success = await _gameBoostService.RemoveKnownGameAsync(gameName);
                if (success)
                {
                    await LoadKnownGamesAsync();
                    SelectedKnownGame = null;
                    StatusMessage = $"Removed game: {gameName}";
                    Logger.LogInformation("Removed known game: {GameName}", gameName);
                }
                else
                {
                    StatusMessage = "Game not found";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing game: {ex.Message}";
                Logger.LogError(ex, "Error removing known game: {GameName}", SelectedKnownGame);
            }
        }

        /// <summary>
        /// Determines if a new game can be added
        /// </summary>
        private bool CanAddKnownGame()
        {
            return !string.IsNullOrWhiteSpace(NewGameExecutableName);
        }

        /// <summary>
        /// Determines if the selected game can be removed
        /// </summary>
        private bool CanRemoveKnownGame()
        {
            return !string.IsNullOrWhiteSpace(SelectedKnownGame);
        }
    }
}
