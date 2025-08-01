using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
        private readonly SystemTweaksViewModel _systemTweaksViewModel;
        private readonly IKeyboardShortcutService _keyboardShortcutService;
        private readonly IServiceProvider _serviceProvider;
        private System.Timers.Timer? _systemTrayUpdateTimer;
        private readonly IElevationService _elevationService;
        private readonly ISecurityService _securityService;

        // Loading overlay management
        private bool _isInitializationComplete = false;
        private readonly List<string> _initializationTasks = new();
        private readonly object _initializationLock = new();
        private System.Timers.Timer? _initializationTimeoutTimer;
        private readonly string _debugLogPath = Path.Combine(Path.GetTempPath(), "ThreadPilot_Debug.log");

        // Flag to skip process monitoring during startup if it causes issues
        private readonly bool _skipProcessMonitoringDuringStartup = false;

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
            SystemTweaksViewModel systemTweaksViewModel,
            IKeyboardShortcutService keyboardShortcutService,
            IServiceProvider serviceProvider,
            IElevationService elevationService,
            ISecurityService securityService)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow constructor starting...");

                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("InitializeComponent completed");

                // Initialize loading overlay
                InitializeLoadingOverlay();
                LogDebug("Loading overlay initialized");
                LogDebug($"Debug log file: {_debugLogPath}");

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
                _systemTweaksViewModel = systemTweaksViewModel;
                _keyboardShortcutService = keyboardShortcutService;
                _serviceProvider = serviceProvider;
                _elevationService = elevationService;
                _securityService = securityService;

                System.Diagnostics.Debug.WriteLine("Dependencies assigned");

                SetDataContexts();
                System.Diagnostics.Debug.WriteLine("DataContexts set");

                // Start async initialization - marshal to UI thread to prevent cross-thread access exceptions
                _ = Dispatcher.InvokeAsync(async () => await InitializeApplicationAsync());
                System.Diagnostics.Debug.WriteLine("Async initialization started");

                SetupTestKeyBinding();
                System.Diagnostics.Debug.WriteLine("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainWindow constructor: {ex}");
                System.Windows.MessageBox.Show($"Error initializing MainWindow:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "MainWindow Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void SetDataContexts()
        {
            // Set DataContext for the main window
            DataContext = _mainWindowViewModel;

            // Set DataContext for the association view
            AssociationView.DataContext = _associationViewModel;

            // Set DataContext for the system tweaks view
            SystemTweaksView.DataContext = _systemTweaksViewModel;
        }

        private void LogDebug(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}";

            try
            {
                System.Diagnostics.Debug.WriteLine(logMessage);
                File.AppendAllText(_debugLogPath, logMessage + Environment.NewLine);
            }
            catch
            {
                // Ignore file write errors
            }
        }

        private void InitializeLoadingOverlay()
        {
            try
            {
                // Load application icon for loading overlay
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ico.ico");
                if (File.Exists(iconPath))
                {
                    var loadingIcon = FindName("LoadingIcon") as System.Windows.Controls.Image;
                    if (loadingIcon != null)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                        bitmap.DecodePixelWidth = 64;
                        bitmap.EndInit();
                        loadingIcon.Source = bitmap;
                    }
                }

                // Start spinner animation
                var spinnerAnimation = FindResource("SpinnerAnimation") as Storyboard;
                spinnerAnimation?.Begin();

                // Start fade-in animation
                var fadeInAnimation = FindResource("FadeInAnimation") as Storyboard;
                fadeInAnimation?.Begin();

                // Set up initialization timeout (15 seconds)
                _initializationTimeoutTimer = new System.Timers.Timer(15000);
                _initializationTimeoutTimer.Elapsed += OnInitializationTimeout;
                _initializationTimeoutTimer.AutoReset = false;
                _initializationTimeoutTimer.Start();

                // Block main content interaction
                var mainContent = FindName("MainContent") as Grid;
                if (mainContent != null)
                {
                    mainContent.IsHitTestVisible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize loading overlay: {ex.Message}");
            }
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                LogDebug("=== Starting InitializeApplicationAsync ===");

                await Dispatcher.InvokeAsync(() => UpdateLoadingStatus("Loading ViewModels..."));
                LogDebug("About to call LoadViewModelsAsync...");
                await LoadViewModelsAsync();
                LogDebug("LoadViewModelsAsync completed successfully");
                CompleteInitializationTask("ViewModels");

                await Dispatcher.InvokeAsync(() => UpdateLoadingStatus("Initializing services..."));
                LogDebug("About to call InitializeServicesAsync...");
                await InitializeServicesAsync();
                LogDebug("InitializeServicesAsync completed successfully");
                CompleteInitializationTask("Services");

                await Dispatcher.InvokeAsync(() => UpdateLoadingStatus("Finalizing startup..."));
                LogDebug("Finalizing startup...");
                await Task.Delay(500); // Brief delay to show final status
                CompleteInitializationTask("Finalization");

                // All initialization complete
                LogDebug("All initialization complete, hiding overlay...");
                await Dispatcher.InvokeAsync(() => HideLoadingOverlay());
                LogDebug("=== InitializeApplicationAsync completed successfully ===");
            }
            catch (Exception ex)
            {
                LogDebug($"=== ERROR in InitializeApplicationAsync: {ex} ===");
                await Dispatcher.InvokeAsync(() => ShowInitializationError(ex));
            }
        }

        private void UpdateLoadingStatus(string status)
        {
            var loadingStatus = FindName("LoadingStatus") as TextBlock;
            if (loadingStatus != null)
            {
                loadingStatus.Text = status;
            }
        }

        private void CompleteInitializationTask(string taskName)
        {
            lock (_initializationLock)
            {
                _initializationTasks.Add(taskName);
                System.Diagnostics.Debug.WriteLine($"Initialization task completed: {taskName}");
            }
        }

        private void HideLoadingOverlay()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Starting HideLoadingOverlay ===");
                _isInitializationComplete = true;
                _initializationTimeoutTimer?.Stop();
                _initializationTimeoutTimer?.Dispose();

                // Stop spinner animation
                var spinnerAnimation = FindResource("SpinnerAnimation") as Storyboard;
                spinnerAnimation?.Stop();
                System.Diagnostics.Debug.WriteLine("Spinner animation stopped");

                // Start fade-out animation
                var fadeOutAnimation = FindResource("FadeOutAnimation") as Storyboard;
                if (fadeOutAnimation != null)
                {
                    System.Diagnostics.Debug.WriteLine("Starting fade-out animation");
                    fadeOutAnimation.Completed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine("Fade-out animation completed, hiding overlay");
                        var loadingOverlay = FindName("LoadingOverlay") as Grid;
                        if (loadingOverlay != null)
                        {
                            loadingOverlay.Visibility = Visibility.Collapsed;
                            System.Diagnostics.Debug.WriteLine("Loading overlay visibility set to Collapsed");
                        }

                        // Re-enable main content interaction
                        var mainContent = FindName("MainContent") as Grid;
                        if (mainContent != null)
                        {
                            mainContent.IsHitTestVisible = true;
                            System.Diagnostics.Debug.WriteLine("Main content interaction re-enabled");
                        }
                        System.Diagnostics.Debug.WriteLine("=== Loading overlay hidden successfully ===");
                    };
                    fadeOutAnimation.Begin();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: FadeOutAnimation not found, hiding overlay immediately");
                    // Fallback: hide overlay immediately if animation fails
                    var loadingOverlay = FindName("LoadingOverlay") as Grid;
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Visibility = Visibility.Collapsed;
                    }

                    // Re-enable main content interaction
                    var mainContent = FindName("MainContent") as Grid;
                    if (mainContent != null)
                    {
                        mainContent.IsHitTestVisible = true;
                    }
                    System.Diagnostics.Debug.WriteLine("=== Loading overlay hidden immediately (fallback) ===");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== ERROR hiding loading overlay: {ex} ===");
                // Emergency fallback: hide overlay without animation
                try
                {
                    var loadingOverlay = FindName("LoadingOverlay") as Grid;
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Visibility = Visibility.Collapsed;
                    }
                    var mainContent = FindName("MainContent") as Grid;
                    if (mainContent != null)
                    {
                        mainContent.IsHitTestVisible = true;
                    }
                    System.Diagnostics.Debug.WriteLine("Emergency fallback: overlay hidden without animation");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Emergency fallback also failed: {fallbackEx}");
                }
            }
        }

        private void OnInitializationTimeout(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (!_isInitializationComplete)
                {
                    ShowInitializationError(new TimeoutException("Application initialization timed out after 15 seconds"));
                }
            });
        }

        private void ShowInitializationError(Exception ex)
        {
            try
            {
                UpdateLoadingStatus("Initialization failed");

                var result = System.Windows.MessageBox.Show(
                    $"ThreadPilot failed to initialize properly:\n\n{ex.Message}\n\nDebug log: {_debugLogPath}\n\nWould you like to retry initialization or close the application?",
                    "Initialization Error",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    // Retry initialization - marshal to UI thread to prevent cross-thread access exceptions
                    _isInitializationComplete = false;
                    _initializationTasks.Clear();
                    UpdateLoadingStatus("Retrying initialization...");
                    LogDebug("=== RETRYING INITIALIZATION ===");
                    _ = Dispatcher.InvokeAsync(async () => await InitializeApplicationAsync());
                }
                else
                {
                    // Close application
                    LogDebug("User chose to close application");
                    System.Windows.Application.Current.Shutdown();
                }
            }
            catch (Exception overlayEx)
            {
                LogDebug($"Error showing initialization error: {overlayEx.Message}");
                System.Windows.Application.Current.Shutdown();
            }
        }

        private async Task LoadViewModelsAsync()
        {
            try
            {
                LogDebug("=== Starting LoadViewModelsAsync ===");

                LogDebug("About to initialize ProcessViewModel (including CPU topology)...");
                try
                {
                    // Use the full initialization method instead of just LoadProcesses
                    var processTask = _processViewModel.InitializeAsync();
                    var processResult = await Task.WhenAny(processTask, Task.Delay(15000)); // 15 second timeout for full initialization
                    if (processResult != processTask)
                    {
                        LogDebug("ProcessViewModel.InitializeAsync() timed out, trying fallback...");
                        // Fallback: just load processes without full initialization
                        await _processViewModel.LoadProcesses();
                        LogDebug($"ProcessViewModel fallback (LoadProcesses only) completed, process count: {_processViewModel.Processes?.Count ?? 0}, filtered count: {_processViewModel.FilteredProcesses?.Count ?? 0}");
                    }
                    else
                    {
                        await processTask; // Ensure we get any exceptions
                        LogDebug($"ProcessViewModel initialized successfully (including CPU topology), process count: {_processViewModel.Processes?.Count ?? 0}, filtered count: {_processViewModel.FilteredProcesses?.Count ?? 0}");
                    }
                }
                catch (Exception processEx)
                {
                    LogDebug($"ProcessViewModel initialization failed: {processEx.Message}, trying fallback...");
                    // Fallback: just load processes without full initialization
                    await _processViewModel.LoadProcesses();
                    LogDebug($"ProcessViewModel fallback (LoadProcesses only) completed after exception, process count: {_processViewModel.Processes?.Count ?? 0}, filtered count: {_processViewModel.FilteredProcesses?.Count ?? 0}");
                }

                LogDebug("About to load PowerPlanViewModel...");
                var powerPlanTask = _powerPlanViewModel.LoadPowerPlans();
                var powerPlanResult = await Task.WhenAny(powerPlanTask, Task.Delay(5000)); // 5 second timeout
                if (powerPlanResult != powerPlanTask)
                {
                    throw new TimeoutException("PowerPlanViewModel.LoadPowerPlans() timed out after 5 seconds");
                }
                await powerPlanTask; // Ensure we get any exceptions
                LogDebug("PowerPlanViewModel loaded successfully");

                LogDebug("About to load SystemTweaksViewModel...");
                var systemTweaksTask = _systemTweaksViewModel.LoadCommand.ExecuteAsync(null);
                var systemTweaksResult = await Task.WhenAny(systemTweaksTask, Task.Delay(5000)); // 5 second timeout
                if (systemTweaksResult != systemTweaksTask)
                {
                    throw new TimeoutException("SystemTweaksViewModel.LoadCommand.ExecuteAsync() timed out after 5 seconds");
                }
                await systemTweaksTask; // Ensure we get any exceptions
                LogDebug("SystemTweaksViewModel loaded successfully");

                // Initialize keyboard shortcuts after window is loaded
                this.Loaded += async (s, e) => await InitializeKeyboardShortcutsAsync();
                LogDebug("Keyboard shortcuts event handler attached");

                // The association view model loads its data automatically in its constructor
                LogDebug("=== LoadViewModelsAsync completed successfully ===");
            }
            catch (Exception ex)
            {
                LogDebug($"=== ERROR in LoadViewModelsAsync: {ex} ===");
                throw; // Re-throw to be handled by initialization error handler
            }
        }

        private async Task InitializeServicesAsync()
        {
            LogDebug("=== Starting InitializeServicesAsync ===");

            LogDebug("About to initialize settings...");
            await InitializeSettingsAsync();
            LogDebug("Settings initialized successfully");

            LogDebug("About to initialize system tray...");
            try
            {
                var systemTrayTask = InitializeSystemTrayAsync();
                var systemTrayResult = await Task.WhenAny(systemTrayTask, Task.Delay(5000)); // 5 second timeout
                if (systemTrayResult != systemTrayTask)
                {
                    LogDebug("System tray initialization timed out, continuing with basic tray setup...");
                    // Initialize basic system tray without context menu updates (Initialize() is idempotent)
                    await InitializeBasicSystemTrayAsync();
                    LogDebug("Basic system tray initialized (without context menu)");
                }
                else
                {
                    await systemTrayTask; // Ensure we get any exceptions
                    LogDebug("System tray initialized successfully");
                }
            }
            catch (Exception systemTrayEx)
            {
                LogDebug($"System tray initialization failed: {systemTrayEx.Message}, using basic tray...");
                // Fallback: basic system tray initialization
                try
                {
                    await InitializeBasicSystemTrayAsync();
                    LogDebug("Fallback system tray initialized");
                }
                catch (Exception fallbackEx)
                {
                    LogDebug($"Even fallback system tray failed: {fallbackEx.Message}");
                }
            }

            LogDebug("About to initialize notifications...");
            InitializeNotifications();
            LogDebug("Notifications initialized successfully");

            LogDebug("About to initialize game boost...");
            InitializeGameBoost();
            LogDebug("Game boost initialized successfully");

            LogDebug("About to initialize monitoring...");
            await InitializeMonitoringAsync();
            LogDebug("Monitoring initialized successfully");

            if (_skipProcessMonitoringDuringStartup)
            {
                LogDebug("Skipping process monitoring manager startup (temporary bypass enabled)");
            }
            else
            {
                LogDebug("About to start process monitoring manager...");
                try
                {
                    var monitoringTask = StartProcessMonitoringManagerAsync();
                    var timeoutTask = Task.Delay(8000); // 8 second timeout
                    var completedTask = await Task.WhenAny(monitoringTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        LogDebug("Process monitoring manager startup timed out after 8 seconds, continuing without monitoring...");
                    }
                    else
                    {
                        try
                        {
                            await monitoringTask; // Ensure we get any exceptions
                            LogDebug("Process monitoring manager started successfully");
                        }
                        catch (Exception taskEx)
                        {
                            LogDebug($"Process monitoring manager task failed: {taskEx.Message}");
                        }
                    }
                }
                catch (Exception monitoringEx)
                {
                    LogDebug($"Process monitoring manager startup failed: {monitoringEx.Message}, continuing without monitoring...");
                }
            }

            LogDebug("=== InitializeServicesAsync completed successfully ===");
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

        private async Task InitializeSystemTrayAsync()
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
                _systemTrayService.PowerPlanChangeRequested += OnPowerPlanChangeRequested;
                _systemTrayService.ProfileApplicationRequested += OnProfileApplicationRequested;
                _systemTrayService.PerformanceDashboardRequested += OnPerformanceDashboardRequested;

                // Update settings and tooltip
                _systemTrayService.UpdateSettings(_settingsService.Settings);
                _systemTrayService.UpdateTooltip("ThreadPilot - Process & Power Plan Manager");

                // Initialize system tray context menu with current data
                await UpdateSystemTrayContextMenuAsync();

                // Start periodic system tray updates
                StartSystemTrayUpdateTimer();
            }
            catch (Exception ex)
            {
                // Log error but don't fail startup
                System.Diagnostics.Debug.WriteLine($"Failed to initialize system tray: {ex.Message}");
            }
        }

        private async Task InitializeBasicSystemTrayAsync()
        {
            try
            {
                LogDebug("Initializing basic system tray (without full context menu)...");

                // Initialize basic tray icon (this is idempotent)
                _systemTrayService.Initialize();
                _systemTrayService.Show();

                // Subscribe to essential tray events only
                _systemTrayService.ShowMainWindowRequested += OnShowMainWindowRequested;
                _systemTrayService.ExitRequested += OnExitRequested;

                // Update basic settings and tooltip
                _systemTrayService.UpdateSettings(_settingsService.Settings);
                _systemTrayService.UpdateTooltip("ThreadPilot - Process & Power Plan Manager (Basic Mode)");

                LogDebug("Basic system tray initialization completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to initialize basic system tray: {ex.Message}");
                throw;
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

        private async void OnPowerPlanChangeRequested(object? sender, PowerPlanChangeRequestedEventArgs e)
        {
            try
            {
                var powerPlanService = _serviceProvider.GetRequiredService<IPowerPlanService>();
                var success = await powerPlanService.SetActivePowerPlanByGuidAsync(e.PowerPlanGuid);

                if (success)
                {
                    _systemTrayService.ShowBalloonTip("ThreadPilot",
                        $"Power plan changed to {e.PowerPlanName}", 2000);
                }
                else
                {
                    _systemTrayService.ShowBalloonTip("ThreadPilot Error",
                        $"Failed to change power plan to {e.PowerPlanName}", 3000);
                }
            }
            catch (Exception ex)
            {
                _systemTrayService.ShowBalloonTip("ThreadPilot Error",
                    $"Error changing power plan: {ex.Message}", 3000);
            }
        }

        private async void OnProfileApplicationRequested(object? sender, ProfileApplicationRequestedEventArgs e)
        {
            try
            {
                var processService = _serviceProvider.GetRequiredService<IProcessService>();
                var selectedProcess = _processViewModel.SelectedProcess;

                if (selectedProcess != null)
                {
                    var success = await processService.LoadProcessProfile(e.ProfileName, selectedProcess);

                    if (success)
                    {
                        _systemTrayService.ShowBalloonTip("ThreadPilot",
                            $"Profile '{e.ProfileName}' applied to {selectedProcess.Name}", 2000);
                    }
                    else
                    {
                        _systemTrayService.ShowBalloonTip("ThreadPilot Error",
                            $"Failed to apply profile '{e.ProfileName}'", 3000);
                    }
                }
                else
                {
                    _systemTrayService.ShowBalloonTip("ThreadPilot",
                        "No process selected for profile application", 2000);
                }
            }
            catch (Exception ex)
            {
                _systemTrayService.ShowBalloonTip("ThreadPilot Error",
                    $"Error applying profile: {ex.Message}", 3000);
            }
        }

        private void OnPerformanceDashboardRequested(object? sender, EventArgs e)
        {
            try
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();

                // Switch to Tweaks tab
                var tabControl = FindName("MainTabControl") as System.Windows.Controls.TabControl;
                if (tabControl != null)
                {
                    // Find the Tweaks tab (index 3 based on MainWindow.xaml)
                    if (tabControl.Items.Count > 3)
                    {
                        tabControl.SelectedIndex = 3;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open system tweaks: {ex.Message}");
            }
        }

        private async Task InitializeKeyboardShortcutsAsync()
        {
            try
            {
                // Set window handle for global hotkey registration
                var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
                var handle = windowInteropHelper.EnsureHandle();

                if (_keyboardShortcutService is KeyboardShortcutService service)
                {
                    service.SetWindowHandle(handle);
                }

                // Subscribe to shortcut activation events
                _keyboardShortcutService.ShortcutActivated += OnShortcutActivated;

                // Load shortcuts from settings - with error handling
                try
                {
                    await _keyboardShortcutService.LoadShortcutsFromSettingsAsync();
                }
                catch (Exception settingsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load shortcuts from settings, using defaults: {settingsEx.Message}");
                    // Continue with default shortcuts if settings loading fails
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize keyboard shortcuts: {ex.Message}");
                // Don't let keyboard shortcut initialization failure prevent the app from starting
            }
        }

        private void OnShortcutActivated(object? sender, ShortcutActivatedEventArgs e)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await HandleShortcutActionAsync(e.ActionName);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling shortcut {e.ActionName}: {ex.Message}");
            }
        }

        private async Task HandleShortcutActionAsync(string actionName)
        {
            switch (actionName)
            {
                case ShortcutActions.ShowMainWindow:
                    if (IsVisible && WindowState != WindowState.Minimized)
                    {
                        Hide();
                    }
                    else
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                    }
                    break;

                case ShortcutActions.ToggleMonitoring:
                    // Toggle monitoring - implementation can be added later
                    await _notificationService.ShowNotificationAsync("Keyboard Shortcut", "Toggle monitoring shortcut activated");
                    break;

                case ShortcutActions.GameBoostToggle:
                    // Toggle game boost mode - implementation can be added later
                    await _notificationService.ShowNotificationAsync("Keyboard Shortcut", "Game Boost toggle shortcut activated");
                    break;

                case ShortcutActions.PowerPlanHighPerformance:
                    // Switch to high performance power plan - implementation can be added later
                    await _notificationService.ShowNotificationAsync("Keyboard Shortcut", "High Performance power plan shortcut activated");
                    break;

                case ShortcutActions.OpenTweaks:
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    var tabControl = FindName("MainTabControl") as System.Windows.Controls.TabControl;
                    if (tabControl != null && tabControl.Items.Count > 3)
                    {
                        tabControl.SelectedIndex = 3; // Tweaks tab
                    }
                    break;

                case ShortcutActions.OpenSettings:
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    var settingsTabControl = FindName("MainTabControl") as System.Windows.Controls.TabControl;
                    if (settingsTabControl != null && settingsTabControl.Items.Count > 4)
                    {
                        settingsTabControl.SelectedIndex = 4; // Settings tab
                    }
                    break;

                case ShortcutActions.RefreshProcessList:
                    // Refresh process list - implementation can be added later
                    await _notificationService.ShowNotificationAsync("Keyboard Shortcut", "Refresh process list shortcut activated");
                    break;

                case ShortcutActions.ExitApplication:
                    Close();
                    break;
            }
        }

        private async Task UpdateSystemTrayContextMenuAsync()
        {
            try
            {
                // Update power plans in system tray
                var powerPlanService = _serviceProvider.GetRequiredService<IPowerPlanService>();
                var powerPlans = await powerPlanService.GetPowerPlansAsync();
                var activePowerPlan = await powerPlanService.GetActivePowerPlan();
                _systemTrayService.UpdatePowerPlans(powerPlans, activePowerPlan);

                // Update profiles in system tray
                var profilesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThreadPilot", "Profiles");
                var profileNames = new List<string>();

                if (Directory.Exists(profilesDirectory))
                {
                    profileNames = Directory.GetFiles(profilesDirectory, "*.json")
                        .Select(Path.GetFileNameWithoutExtension)
                        .ToList();
                }

                _systemTrayService.UpdateProfiles(profileNames);

                // Update system status (with timeout to prevent hanging)
                try
                {
                    var performanceService = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();
                    var metricsTask = performanceService.GetSystemMetricsAsync();
                    var metricsResult = await Task.WhenAny(metricsTask, Task.Delay(2000)); // 2 second timeout

                    if (metricsResult == metricsTask)
                    {
                        var currentMetrics = await metricsTask;
                        _systemTrayService.UpdateSystemStatus(
                            activePowerPlan?.Name ?? "Unknown",
                            currentMetrics?.TotalCpuUsage ?? 0.0,
                            currentMetrics?.MemoryUsagePercentage ?? 0.0);
                    }
                    else
                    {
                        // Timeout - use default values
                        _systemTrayService.UpdateSystemStatus(
                            activePowerPlan?.Name ?? "Unknown",
                            0.0, 0.0);
                    }
                }
                catch (Exception metricsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get performance metrics for tray: {metricsEx.Message}");
                    // Use default values
                    _systemTrayService.UpdateSystemStatus(
                        activePowerPlan?.Name ?? "Unknown",
                        0.0, 0.0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update system tray context menu: {ex.Message}");
            }
        }

        private void StartSystemTrayUpdateTimer()
        {
            try
            {
                _systemTrayUpdateTimer = new System.Timers.Timer(10000); // PERFORMANCE OPTIMIZATION: Update every 10 seconds instead of 5
                // Marshal timer callback to UI thread to avoid cross-thread access issues
                _systemTrayUpdateTimer.Elapsed += async (s, e) =>
                {
                    try
                    {
                        await Dispatcher.InvokeAsync(async () => await UpdateSystemTrayStatusAsync());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in system tray update timer: {ex.Message}");
                    }
                };
                _systemTrayUpdateTimer.AutoReset = true;
                _systemTrayUpdateTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start system tray update timer: {ex.Message}");
            }
        }

        private async Task UpdateSystemTrayStatusAsync()
        {
            try
            {
                var powerPlanService = _serviceProvider.GetRequiredService<IPowerPlanService>();
                var performanceService = _serviceProvider.GetRequiredService<IPerformanceMonitoringService>();

                var activePowerPlan = await powerPlanService.GetActivePowerPlan();
                var currentMetrics = await performanceService.GetSystemMetricsAsync();

                _systemTrayService.UpdateSystemStatus(
                    activePowerPlan?.Name ?? "Unknown",
                    currentMetrics?.TotalCpuUsage ?? 0.0,
                    currentMetrics?.MemoryUsagePercentage ?? 0.0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update system tray status: {ex.Message}");
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
                LogDebug("Subscribing to process monitor manager events...");
                // Subscribe to process monitor manager events
                _processMonitorManagerService.ServiceStatusChanged += OnProcessMonitorManagerStatusChanged;

                LogDebug("Starting process monitoring manager service...");
                // Start the process monitoring manager service with internal timeout
                var startTask = _processMonitorManagerService.StartAsync();
                var timeoutTask = Task.Delay(6000); // 6 second internal timeout
                var completedTask = await Task.WhenAny(startTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    LogDebug("ProcessMonitorManagerService.StartAsync() timed out internally");
                    throw new TimeoutException("Process monitoring manager service startup timed out");
                }

                await startTask; // Get any exceptions
                LogDebug("Process monitoring manager service started, showing notification...");

                await _notificationService.ShowSuccessNotificationAsync(
                    "ThreadPilot Started",
                    "Process monitoring and power plan management is now active");

                LogDebug("Success notification shown");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to start process monitoring manager: {ex.Message}");
                try
                {
                    await _notificationService.ShowErrorNotificationAsync(
                        "Startup Error",
                        "Failed to start process monitoring manager",
                        ex);
                }
                catch (Exception notificationEx)
                {
                    LogDebug($"Failed to show error notification: {notificationEx.Message}");
                }
                throw; // Re-throw to be caught by outer handler
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
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                Dispatcher.InvokeAsync(() =>
                {
                    // Update tray with Game Boost active status
                    _systemTrayService.UpdateGameBoostStatus(true, e.GameProcess.Name);

                    // Update main window status
                    _mainWindowViewModel.UpdateGameBoostStatus(true, e.GameProcess.Name);
                });
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
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                Dispatcher.InvokeAsync(() =>
                {
                    // Update tray with Game Boost inactive status
                    _systemTrayService.UpdateGameBoostStatus(false);

                    // Update main window status
                    _mainWindowViewModel.UpdateGameBoostStatus(false);
                });
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

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _systemTrayUpdateTimer?.Stop();
                _systemTrayUpdateTimer?.Dispose();

                _initializationTimeoutTimer?.Stop();
                _initializationTimeoutTimer?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing timers: {ex.Message}");
            }

            base.OnClosed(e);
        }
    }
}