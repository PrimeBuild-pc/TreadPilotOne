using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ThreadPilot.Models;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot.ViewModels
{
    public partial class ProcessPowerPlanAssociationViewModel : BaseViewModel
    {
        private readonly IProcessPowerPlanAssociationService _associationService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly IProcessService _processService;
        private readonly IProcessMonitorManagerService _monitorManagerService;

        [ObservableProperty]
        private ObservableCollection<ProcessPowerPlanAssociation> associations = new();

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> availablePowerPlans = new();

        [ObservableProperty]
        private ObservableCollection<ProcessModel> runningProcesses = new();

        [ObservableProperty]
        private ProcessPowerPlanAssociation? selectedAssociation;

        [ObservableProperty]
        private PowerPlanModel? selectedPowerPlan;

        [ObservableProperty]
        private ProcessModel? selectedProcess;

        [ObservableProperty]
        private string newExecutableName = string.Empty;

        [ObservableProperty]
        private string newExecutablePath = string.Empty;

        // Properties for the selected executable (read-only display)
        [ObservableProperty]
        private string selectedExecutableDisplayName = "No executable selected";

        [ObservableProperty]
        private string selectedExecutableFullPath = string.Empty;

        [ObservableProperty]
        private bool hasSelectedExecutable = false;

        [ObservableProperty]
        private bool matchByPath = false;

        [ObservableProperty]
        private int priority = 0;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private PowerPlanModel? defaultPowerPlan;

        [ObservableProperty]
        private bool isMonitoringEnabled = true;

        [ObservableProperty]
        private bool isEventBasedMonitoringEnabled = true;

        [ObservableProperty]
        private bool isFallbackPollingEnabled = true;

        [ObservableProperty]
        private int pollingIntervalSeconds = 5;

        [ObservableProperty]
        private bool preventDuplicatePowerPlanChanges = true;

        [ObservableProperty]
        private int powerPlanChangeDelayMs = 1000;

        [ObservableProperty]
        private string serviceStatus = "Stopped";

        [ObservableProperty]
        private bool isServiceRunning = false;

        public ProcessPowerPlanAssociationViewModel(
            ILogger<ProcessPowerPlanAssociationViewModel> logger,
            IProcessPowerPlanAssociationService associationService,
            IPowerPlanService powerPlanService,
            IProcessService processService,
            IProcessMonitorManagerService monitorManagerService,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            _associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _monitorManagerService = monitorManagerService ?? throw new ArgumentNullException(nameof(monitorManagerService));

            // Subscribe to events
            _associationService.ConfigurationChanged += OnConfigurationChanged;
            _monitorManagerService.ServiceStatusChanged += OnServiceStatusChanged;
            _monitorManagerService.ProcessPowerPlanChanged += OnProcessPowerPlanChanged;

            // Initialize
            _ = InitializeAsync();
        }

        public override async Task InitializeAsync()
        {
            await LoadDataAsync();
            UpdateServiceStatus();
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                SetStatus("Loading data...");

                // Load associations
                var associationsData = await _associationService.GetAssociationsAsync();
                Associations = new ObservableCollection<ProcessPowerPlanAssociation>(associationsData);

                // Load power plans
                var powerPlans = await _powerPlanService.GetPowerPlansAsync();
                AvailablePowerPlans = powerPlans;

                // Load running processes
                var processes = await _processService.GetProcessesAsync();
                RunningProcesses = processes;

                // Load configuration settings
                var config = _associationService.Configuration;
                IsEventBasedMonitoringEnabled = config.IsEventBasedMonitoringEnabled;
                IsFallbackPollingEnabled = config.IsFallbackPollingEnabled;
                PollingIntervalSeconds = config.PollingIntervalSeconds;
                PreventDuplicatePowerPlanChanges = config.PreventDuplicatePowerPlanChanges;
                PowerPlanChangeDelayMs = config.PowerPlanChangeDelayMs;

                // Load default power plan
                var (defaultGuid, defaultName) = await _associationService.GetDefaultPowerPlanAsync();
                DefaultPowerPlan = AvailablePowerPlans.FirstOrDefault(p => p.Guid == defaultGuid);

                ClearStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading data: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task AddAssociationAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NewExecutableName) || SelectedPowerPlan == null)
                {
                    SetStatus("Please select an executable and a power plan", false);
                    return;
                }

                SetStatus("Adding association...");

                var association = new ProcessPowerPlanAssociation
                {
                    ExecutableName = NewExecutableName.Trim(),
                    ExecutablePath = NewExecutablePath.Trim(),
                    PowerPlanGuid = SelectedPowerPlan.Guid,
                    PowerPlanName = SelectedPowerPlan.Name,
                    MatchByPath = MatchByPath,
                    Priority = Priority,
                    Description = Description.Trim(),
                    IsEnabled = true
                };

                var success = await _associationService.AddAssociationAsync(association);
                if (success)
                {
                    // Clear form
                    NewExecutableName = string.Empty;
                    NewExecutablePath = string.Empty;
                    SelectedPowerPlan = null;
                    MatchByPath = false;
                    Priority = 0;
                    Description = string.Empty;

                    await LoadDataAsync();
                    SetStatus("Association added successfully");
                }
                else
                {
                    SetStatus("Failed to add association - it may already exist", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error adding association: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task UpdateAssociationAsync()
        {
            try
            {
                if (SelectedAssociation == null)
                {
                    SetStatus("Please select an association to update", false);
                    return;
                }

                SetStatus("Updating association...");

                var success = await _associationService.UpdateAssociationAsync(SelectedAssociation);
                if (success)
                {
                    await LoadDataAsync();
                    SetStatus("Association updated successfully");
                }
                else
                {
                    SetStatus("Failed to update association", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error updating association: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task RemoveAssociationAsync()
        {
            try
            {
                if (SelectedAssociation == null)
                {
                    SetStatus("Please select an association to remove", false);
                    return;
                }

                SetStatus("Removing association...");

                var success = await _associationService.RemoveAssociationAsync(SelectedAssociation.Id);
                if (success)
                {
                    await LoadDataAsync();
                    SetStatus("Association removed successfully");
                }
                else
                {
                    SetStatus("Failed to remove association", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error removing association: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task SetDefaultPowerPlanAsync()
        {
            try
            {
                if (DefaultPowerPlan == null)
                {
                    SetStatus("Please select a default power plan", false);
                    return;
                }

                SetStatus("Setting default power plan...");

                var success = await _associationService.SetDefaultPowerPlanAsync(DefaultPowerPlan.Guid, DefaultPowerPlan.Name);
                if (success)
                {
                    SetStatus("Default power plan set successfully");
                }
                else
                {
                    SetStatus("Failed to set default power plan", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting default power plan: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task StartMonitoringAsync()
        {
            try
            {
                SetStatus("Starting monitoring service...");
                await _monitorManagerService.StartAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Error starting monitoring: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task StopMonitoringAsync()
        {
            try
            {
                SetStatus("Stopping monitoring service...");
                await _monitorManagerService.StopAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Error stopping monitoring: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task SaveConfigurationAsync()
        {
            try
            {
                SetStatus("Saving configuration...");

                // Update configuration with current settings
                var config = _associationService.Configuration;
                config.IsEventBasedMonitoringEnabled = IsEventBasedMonitoringEnabled;
                config.IsFallbackPollingEnabled = IsFallbackPollingEnabled;
                config.PollingIntervalSeconds = PollingIntervalSeconds;
                config.PreventDuplicatePowerPlanChanges = PreventDuplicatePowerPlanChanges;
                config.PowerPlanChangeDelayMs = PowerPlanChangeDelayMs;

                var success = await _associationService.SaveConfigurationAsync();
                if (success)
                {
                    SetStatus("Configuration saved successfully");
                }
                else
                {
                    SetStatus("Failed to save configuration", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error saving configuration: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public void UseSelectedProcessForAssociation()
        {
            if (SelectedProcess != null)
            {
                NewExecutableName = SelectedProcess.Name;
                NewExecutablePath = SelectedProcess.ExecutablePath;

                // Update the selected executable display
                UpdateSelectedExecutableDisplay(SelectedProcess.ExecutablePath, SelectedProcess.Name);
            }
        }

        [RelayCommand]
        public void BrowseExecutable()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Executable File",
                    Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                    FilterIndex = 1,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var selectedFilePath = openFileDialog.FileName;

                    // Validate that it's an executable file
                    if (!IsValidExecutable(selectedFilePath))
                    {
                        SetStatus("Selected file is not a valid executable", false);
                        return;
                    }

                    // Extract executable name from the full path
                    var executableName = Path.GetFileName(selectedFilePath);

                    // Auto-populate the fields
                    NewExecutableName = executableName;
                    NewExecutablePath = selectedFilePath;

                    // Update the display
                    UpdateSelectedExecutableDisplay(selectedFilePath, executableName);

                    SetStatus($"Selected executable: {executableName}");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error selecting executable: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public void ClearSelectedExecutable()
        {
            NewExecutableName = string.Empty;
            NewExecutablePath = string.Empty;
            SelectedExecutableDisplayName = "No executable selected";
            SelectedExecutableFullPath = string.Empty;
            HasSelectedExecutable = false;
            SetStatus("Executable selection cleared");
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            // Reload data when configuration changes
            _ = Task.Run(LoadDataAsync);
        }

        private void OnServiceStatusChanged(object? sender, ServiceStatusEventArgs e)
        {
            ServiceStatus = e.Status;
            IsServiceRunning = e.IsRunning;
        }

        private bool IsValidExecutable(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return false;

                var extension = Path.GetExtension(filePath);
                return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateSelectedExecutableDisplay(string fullPath, string executableName)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                SelectedExecutableDisplayName = "No executable selected";
                SelectedExecutableFullPath = string.Empty;
                HasSelectedExecutable = false;
            }
            else
            {
                SelectedExecutableDisplayName = executableName;
                SelectedExecutableFullPath = fullPath;
                HasSelectedExecutable = true;
            }
        }

        private void OnProcessPowerPlanChanged(object? sender, ProcessPowerPlanChangeEventArgs e)
        {
            // Update status when power plan changes occur
            SetStatus($"Power plan changed: {e.NewPowerPlan?.Name} for {e.Process.Name}");
        }

        private void UpdateServiceStatus()
        {
            ServiceStatus = _monitorManagerService.Status;
            IsServiceRunning = _monitorManagerService.IsRunning;
        }
    }
}
