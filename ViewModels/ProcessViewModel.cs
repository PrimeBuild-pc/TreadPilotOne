using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public partial class ProcessViewModel : BaseViewModel
    {
        private readonly IProcessService _processService;
        private readonly ICpuTopologyService _cpuTopologyService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly ISystemTrayService _systemTrayService;
        private System.Timers.Timer? _refreshTimer;

        [ObservableProperty]
        private ObservableCollection<ProcessModel> processes = new();

        [ObservableProperty]
        private ProcessModel? selectedProcess;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ProcessModel> filteredProcesses = new();

        [ObservableProperty]
        private string profileName = string.Empty;

        // CPU Topology and Affinity
        [ObservableProperty]
        private CpuTopologyModel? cpuTopology;

        [ObservableProperty]
        private ObservableCollection<CpuCoreModel> cpuCores = new();

        [ObservableProperty]
        private ObservableCollection<CpuAffinityPreset> affinityPresets = new();

        [ObservableProperty]
        private bool isTopologyDetectionSuccessful = false;

        [ObservableProperty]
        private string topologyStatus = "Detecting CPU topology...";

        [ObservableProperty]
        private bool areAdvancedFeaturesAvailable = false;

        [ObservableProperty]
        private PowerPlanModel? selectedPowerPlan;

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> powerPlans = new();

        // Note: EnableHyperThreading property removed - now using read-only status indicator

        [ObservableProperty]
        private bool showActiveApplicationsOnly = false;

        // Hyperthreading/SMT Status Properties
        [ObservableProperty]
        private string hyperThreadingStatusText = "Multi-Threading: Unknown";

        [ObservableProperty]
        private bool isHyperThreadingActive = false;

        public ProcessViewModel(
            ILogger<ProcessViewModel> logger,
            IProcessService processService,
            ICpuTopologyService cpuTopologyService,
            IPowerPlanService powerPlanService,
            ISystemTrayService systemTrayService,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));

            // Subscribe to topology detection events
            _cpuTopologyService.TopologyDetected += OnTopologyDetected;

            // Subscribe to system tray events
            _systemTrayService.QuickApplyRequested += OnTrayQuickApplyRequested;

            SetupRefreshTimer();
            _ = InitializeAsync();
        }

        public override async Task InitializeAsync()
        {
            try
            {
                SetStatus("Initializing CPU topology and power plans...");

                // Initialize CPU topology
                await _cpuTopologyService.DetectTopologyAsync();

                // Load power plans
                await LoadPowerPlansAsync();

                // Load processes automatically on startup (Bug #8 fix)
                await LoadProcessesCommand.ExecuteAsync(null);

                // Start refresh timer for real-time updates
                _refreshTimer?.Start();
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to initialize: {ex.Message}", false);
            }
        }

        private async Task LoadPowerPlansAsync()
        {
            try
            {
                var plans = await _powerPlanService.GetPowerPlansAsync();
                PowerPlans.Clear();
                foreach (var plan in plans)
                {
                    PowerPlans.Add(plan);
                }

                // Set current active power plan as selected
                var activePlan = await _powerPlanService.GetActivePowerPlan();
                SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load power plans: {ex.Message}", false);
            }
        }

        partial void OnSelectedProcessChanged(ProcessModel? value)
        {
            if (value != null && CpuTopology != null)
            {
                // Immediately fetch and display real-time process information
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // First check if the process is still running
                        bool isStillRunning = await _processService.IsProcessStillRunning(value);
                        if (!isStillRunning)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                SetStatus($"Process {value.Name} (PID: {value.ProcessId}) has terminated", false);
                                SelectedProcess = null;
                                ClearProcessSelection();
                            });
                            return;
                        }

                        // Refresh process info to get current state from OS
                        await _processService.RefreshProcessInfo(value);

                        // Update UI on main thread with fresh data
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Update CPU affinity display
                            UpdateCoreSelections(value.ProcessorAffinity);

                            // Update priority display - trigger property change to refresh ComboBox
                            OnPropertyChanged(nameof(SelectedProcess));

                            // Update status
                            SetStatus($"Selected process: {value.Name} (PID: {value.ProcessId}) - " +
                                    $"Priority: {value.Priority}, Affinity: 0x{value.ProcessorAffinity:X}");
                        });

                        // Load current power plan association if available
                        await LoadProcessPowerPlanAssociation(value);
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("terminated") || ex.Message.Contains("exited") || ex.Message.Contains("no longer exists"))
                    {
                        // Process has terminated
                        Logger.LogInformation("Process {ProcessName} (PID: {ProcessId}) has terminated", value.Name, value.ProcessId);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SetStatus($"Process {value.Name} (PID: {value.ProcessId}) has terminated", false);
                            SelectedProcess = null;
                            ClearProcessSelection();
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to refresh process info for {ProcessName}", value.Name);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SetStatus($"Warning: Could not access process {value.Name} - it may have terminated or require elevated privileges", false);
                        });
                    }
                });
            }
            else if (value == null)
            {
                // Clear selection
                ClearProcessSelection();
            }

            // Update system tray context menu
            _systemTrayService.UpdateContextMenu(value?.Name, value != null);
        }

        private async void OnTrayQuickApplyRequested(object? sender, EventArgs e)
        {
            try
            {
                await QuickApplyAffinityAndPowerPlanCommand.ExecuteAsync(null);
                _systemTrayService.ShowBalloonTip("ThreadPilot",
                    $"Settings applied to {SelectedProcess?.Name ?? "selected process"}", 2000);
            }
            catch (Exception ex)
            {
                _systemTrayService.ShowBalloonTip("ThreadPilot Error",
                    $"Failed to apply settings: {ex.Message}", 3000);
            }
        }

        private void OnTopologyDetected(object? sender, CpuTopologyDetectedEventArgs e)
        {
            CpuTopology = e.Topology;
            IsTopologyDetectionSuccessful = e.DetectionSuccessful;

            if (e.DetectionSuccessful)
            {
                TopologyStatus = $"Detected: {e.Topology.TotalLogicalCores} logical cores, " +
                               $"{e.Topology.TotalPhysicalCores} physical cores";
                AreAdvancedFeaturesAvailable = e.Topology.HasIntelHybrid || e.Topology.HasAmdCcd || e.Topology.HasHyperThreading;
            }
            else
            {
                TopologyStatus = $"Detection failed: {e.ErrorMessage ?? "Unknown error"}";
                AreAdvancedFeaturesAvailable = false;
            }

            UpdateCpuCores();
            UpdateAffinityPresets();
            UpdateHyperThreadingStatus();
        }

        private void UpdateCpuCores()
        {
            if (CpuTopology == null) return;

            CpuCores.Clear();
            foreach (var core in CpuTopology.LogicalCores)
            {
                CpuCores.Add(core);
            }
        }

        private void UpdateHyperThreadingStatus()
        {
            if (CpuTopology == null)
            {
                HyperThreadingStatusText = "Multi-Threading: Unknown";
                IsHyperThreadingActive = false;
                return;
            }

            // Determine if hyperthreading/SMT is present and active
            bool hasMultiThreading = CpuTopology.HasHyperThreading;
            IsHyperThreadingActive = hasMultiThreading;

            // Determine the appropriate technology name based on CPU vendor
            string technologyName = "Multi-Threading";
            if (CpuTopology.CpuBrand.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            {
                technologyName = "Hyper-Threading";
            }
            else if (CpuTopology.CpuBrand.Contains("AMD", StringComparison.OrdinalIgnoreCase))
            {
                technologyName = "SMT";
            }

            // Set the status text
            string status = hasMultiThreading ? "Active" : "Not Available";
            HyperThreadingStatusText = $"{technologyName}: {status}";

            Logger.LogInformation("Updated hyperthreading status: {StatusText} (Active: {IsActive})",
                HyperThreadingStatusText, IsHyperThreadingActive);
        }

        private void UpdateAffinityPresets()
        {
            AffinityPresets.Clear();
            var presets = _cpuTopologyService.GetAffinityPresets();
            foreach (var preset in presets)
            {
                AffinityPresets.Add(preset);
            }
        }

        private void UpdateCoreSelections(long affinityMask)
        {
            if (CpuTopology == null || CpuCores.Count == 0)
            {
                Logger.LogWarning("Cannot update core selections: CpuTopology={CpuTopology}, CpuCores.Count={CpuCoresCount}",
                    CpuTopology != null, CpuCores.Count);
                return;
            }

            Logger.LogDebug("Updating core selections for affinity mask 0x{AffinityMask:X} ({AffinityMaskBinary})",
                affinityMask, Convert.ToString(affinityMask, 2).PadLeft(Environment.ProcessorCount, '0'));

            // Update each core's selection state based on the actual OS affinity mask
            var updatedCores = new List<(int CoreId, bool WasSelected, bool IsSelected)>();

            foreach (var core in CpuCores)
            {
                bool wasSelected = core.IsSelected;
                bool shouldBeSelected = (affinityMask & core.AffinityMask) != 0;

                if (wasSelected != shouldBeSelected)
                {
                    core.IsSelected = shouldBeSelected;
                    updatedCores.Add((core.LogicalCoreId, wasSelected, shouldBeSelected));
                }
            }

            // The UI will automatically update since CpuCoreModel now implements INotifyPropertyChanged
            // No need to force collection refresh as individual property changes will be notified

            // Log the affinity update for debugging
            var selectedCoreIds = CpuCores.Where(c => c.IsSelected).Select(c => c.LogicalCoreId).OrderBy(id => id).ToList();
            var totalCores = CpuCores.Count;
            var selectedCount = selectedCoreIds.Count;

            Logger.LogInformation("Updated core selections for affinity mask 0x{AffinityMask:X}: " +
                                "Selected {SelectedCount}/{TotalCores} cores: [{CoreIds}]",
                affinityMask, selectedCount, totalCores, string.Join(", ", selectedCoreIds));

            if (updatedCores.Count > 0)
            {
                Logger.LogDebug("Core selection changes: {Changes}",
                    string.Join("; ", updatedCores.Select(c => $"Core {c.CoreId}: {c.WasSelected} -> {c.IsSelected}")));
            }
            else
            {
                Logger.LogDebug("No core selection changes needed - UI already matches affinity mask");
            }
        }

        private long CalculateAffinityMask()
        {
            if (CpuTopology == null) return 0;

            var selectedCores = CpuCores.Where(core => core.IsSelected);

            // Note: Removed hyperthreading filtering - user can manually select desired cores
            // All selected cores (including HT siblings) are now included in the affinity mask

            return selectedCores.Aggregate(0L, (mask, core) => mask | core.AffinityMask);
        }

        [RelayCommand]
        public async Task LoadProcesses()
        {
            try
            {
                SetStatus(ShowActiveApplicationsOnly ? "Loading active applications..." : "Loading processes...");

                if (ShowActiveApplicationsOnly)
                {
                    Processes = await _processService.GetActiveApplicationsAsync();
                }
                else
                {
                    Processes = await _processService.GetProcessesAsync();
                }

                FilterProcesses();
                ClearStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading processes: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task RefreshProcesses()
        {
            if (IsBusy) return;

            try
            {
                // Store the currently selected process ID to preserve selection
                var selectedProcessId = SelectedProcess?.ProcessId;

                var currentProcesses = ShowActiveApplicationsOnly
                    ? await _processService.GetActiveApplicationsAsync()
                    : await _processService.GetProcessesAsync();

                // Update existing processes or add new ones
                foreach (var process in currentProcesses)
                {
                    var existingProcess = Processes.FirstOrDefault(p => p.ProcessId == process.ProcessId);
                    if (existingProcess != null)
                    {
                        await _processService.RefreshProcessInfo(existingProcess);
                    }
                    else
                    {
                        Processes.Add(process);
                    }
                }

                // Remove terminated processes
                var terminatedProcesses = Processes
                    .Where(p => !currentProcesses.Any(cp => cp.ProcessId == p.ProcessId))
                    .ToList();

                // Check if selected process was terminated
                bool selectedProcessTerminated = false;
                foreach (var terminated in terminatedProcesses)
                {
                    if (terminated.ProcessId == selectedProcessId)
                    {
                        selectedProcessTerminated = true;
                    }
                    Processes.Remove(terminated);
                }

                FilterProcesses();

                // Restore selection if the process still exists
                if (selectedProcessId.HasValue && !selectedProcessTerminated)
                {
                    var processToSelect = FilteredProcesses.FirstOrDefault(p => p.ProcessId == selectedProcessId.Value);
                    if (processToSelect != null)
                    {
                        SelectedProcess = processToSelect;
                    }
                }
                else if (selectedProcessTerminated)
                {
                    // Clear selection and reset UI if selected process was terminated
                    SelectedProcess = null;
                    ClearProcessSelection();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error refreshing processes: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task SetAffinity()
        {
            if (SelectedProcess == null) return;

            try
            {
                var affinityMask = CalculateAffinityMask();
                if (affinityMask == 0)
                {
                    SetStatus("Please select at least one CPU core", false);
                    return;
                }

                SetStatus($"Setting affinity for {SelectedProcess.Name}...");

                // Apply the affinity change
                await _processService.SetProcessorAffinity(SelectedProcess, affinityMask);

                // Immediately refresh the process to get the actual system state
                await _processService.RefreshProcessInfo(SelectedProcess);

                // Update UI to reflect the actual system affinity (not our calculated one)
                // This ensures we show what the OS actually set, which may differ from our request
                UpdateCoreSelections(SelectedProcess.ProcessorAffinity);

                // Notify UI of all changes
                OnPropertyChanged(nameof(SelectedProcess));

                // Verify the affinity was set correctly
                if (SelectedProcess.ProcessorAffinity == affinityMask)
                {
                    SetStatus($"Affinity set successfully for {SelectedProcess.Name} (0x{affinityMask:X})");
                }
                else
                {
                    SetStatus($"Affinity partially set for {SelectedProcess.Name} - OS adjusted to 0x{SelectedProcess.ProcessorAffinity:X}", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting affinity: {ex.Message}", false);

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await _processService.RefreshProcessInfo(SelectedProcess);
                    UpdateCoreSelections(SelectedProcess.ProcessorAffinity);
                    OnPropertyChanged(nameof(SelectedProcess));
                }
                catch
                {
                    // Process may have terminated
                }
            }
        }

        [RelayCommand]
        private void ApplyAffinityPreset(CpuAffinityPreset preset)
        {
            if (preset == null || !preset.IsAvailable || CpuTopology == null) return;

            // Clear all selections first
            foreach (var core in CpuCores)
            {
                core.IsSelected = false;
            }

            // Apply preset mask
            foreach (var core in CpuCores)
            {
                core.IsSelected = (preset.AffinityMask & core.AffinityMask) != 0;
            }

            // Notify UI of changes
            OnPropertyChanged(nameof(CpuCores));
            SetStatus($"Applied preset: {preset.Name}");
        }

        [RelayCommand]
        private void SelectAllCores()
        {
            foreach (var core in CpuCores)
            {
                core.IsSelected = true;
            }
            OnPropertyChanged(nameof(CpuCores));
            SetStatus("Selected all CPU cores");
        }

        [RelayCommand]
        private void SelectPhysicalCoresOnly()
        {
            if (CpuTopology == null) return;

            foreach (var core in CpuCores)
            {
                core.IsSelected = false;
            }

            foreach (var physicalCore in CpuTopology.PhysicalCores)
            {
                var coreModel = CpuCores.FirstOrDefault(c => c.LogicalCoreId == physicalCore.LogicalCoreId);
                if (coreModel != null)
                {
                    coreModel.IsSelected = true;
                }
            }

            OnPropertyChanged(nameof(CpuCores));
            SetStatus("Selected physical cores only (no HyperThreading)");
        }

        [RelayCommand]
        private void SelectPerformanceCores()
        {
            if (CpuTopology == null || !CpuTopology.HasIntelHybrid) return;

            foreach (var core in CpuCores)
            {
                core.IsSelected = core.CoreType == CpuCoreType.PerformanceCore;
            }

            OnPropertyChanged(nameof(CpuCores));
            SetStatus("Selected Intel Performance cores (P-cores)");
        }

        [RelayCommand]
        private void SelectEfficiencyCores()
        {
            if (CpuTopology == null || !CpuTopology.HasIntelHybrid) return;

            foreach (var core in CpuCores)
            {
                core.IsSelected = core.CoreType == CpuCoreType.EfficiencyCore;
            }

            OnPropertyChanged(nameof(CpuCores));
            SetStatus("Selected Intel Efficiency cores (E-cores)");
        }

        [RelayCommand]
        private void SelectCcdCores(int ccdId)
        {
            if (CpuTopology == null || !CpuTopology.HasAmdCcd) return;

            foreach (var core in CpuCores)
            {
                core.IsSelected = core.CcdId == ccdId;
            }

            OnPropertyChanged(nameof(CpuCores));
            SetStatus($"Selected AMD CCD {ccdId} cores");
        }

        [RelayCommand]
        private void ClearCoreSelection()
        {
            foreach (var core in CpuCores)
            {
                core.IsSelected = false;
            }
            OnPropertyChanged(nameof(CpuCores));
            SetStatus("Cleared CPU core selection");
        }

        [RelayCommand]
        private async Task QuickApplyAffinityAndPowerPlan()
        {
            if (SelectedProcess == null) return;

            try
            {
                SetStatus($"Applying settings to {SelectedProcess.Name}...");

                // Apply CPU affinity
                var affinityMask = CalculateAffinityMask();
                if (affinityMask > 0)
                {
                    await _processService.SetProcessorAffinity(SelectedProcess, affinityMask);
                }

                // Apply power plan if selected
                if (SelectedPowerPlan != null)
                {
                    await _powerPlanService.SetActivePowerPlan(SelectedPowerPlan);
                }

                SetStatus($"Settings applied successfully to {SelectedProcess.Name}");
            }
            catch (Exception ex)
            {
                SetStatus($"Error applying settings: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task RefreshTopology()
        {
            try
            {
                SetStatus("Refreshing CPU topology...");
                await _cpuTopologyService.RefreshTopologyAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Error refreshing topology: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task SetPowerPlan()
        {
            if (SelectedPowerPlan == null) return;

            try
            {
                SetStatus($"Setting power plan to {SelectedPowerPlan.Name}...");

                // Apply the power plan change
                await _powerPlanService.SetActivePowerPlan(SelectedPowerPlan);

                // Verify the power plan was set by getting the current active plan
                var activePlan = await _powerPlanService.GetActivePowerPlan();

                // Update UI to reflect the actual system state
                SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid) ?? SelectedPowerPlan;

                if (activePlan?.Guid == SelectedPowerPlan.Guid)
                {
                    SetStatus($"Power plan set successfully to {SelectedPowerPlan.Name}");
                }
                else
                {
                    SetStatus($"Power plan change attempted - current plan: {activePlan?.Name ?? "Unknown"}", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting power plan: {ex.Message}", false);

                // Try to refresh the current power plan even if setting failed
                try
                {
                    var activePlan = await _powerPlanService.GetActivePowerPlan();
                    SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid);
                }
                catch
                {
                    // Ignore refresh errors
                }
            }
        }

        [RelayCommand]
        private async Task SetPriority(ProcessPriorityClass priority)
        {
            if (SelectedProcess == null) return;

            try
            {
                SetStatus($"Setting priority for {SelectedProcess.Name} to {priority}...");

                // Apply the priority change
                await _processService.SetProcessPriority(SelectedProcess, priority);

                // Immediately refresh the process to get the actual system state
                await _processService.RefreshProcessInfo(SelectedProcess);

                // Notify UI that the process properties have changed
                OnPropertyChanged(nameof(SelectedProcess));

                // Verify the priority was set correctly
                if (SelectedProcess.Priority == priority)
                {
                    SetStatus($"Priority set successfully for {SelectedProcess.Name} to {priority}");
                }
                else
                {
                    SetStatus($"Priority set for {SelectedProcess.Name} - OS adjusted to {SelectedProcess.Priority}", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting priority: {ex.Message}", false);

                // Try to refresh process info even if setting failed, to show current state
                try
                {
                    await _processService.RefreshProcessInfo(SelectedProcess);
                    OnPropertyChanged(nameof(SelectedProcess));
                }
                catch
                {
                    // Process may have terminated
                }
            }
        }

        [RelayCommand]
        private async Task SaveProfile()
        {
            if (SelectedProcess == null || string.IsNullOrWhiteSpace(ProfileName)) return;

            try
            {
                SetStatus($"Saving profile {ProfileName}...");
                await _processService.SaveProcessProfile(ProfileName, SelectedProcess);
                ClearStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error saving profile: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task LoadProfile()
        {
            if (SelectedProcess == null || string.IsNullOrWhiteSpace(ProfileName)) return;

            try
            {
                SetStatus($"Loading profile {ProfileName}...");
                await _processService.LoadProcessProfile(ProfileName, SelectedProcess);
                ClearStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading profile: {ex.Message}", false);
            }
        }

        private void SetupRefreshTimer()
        {
            _refreshTimer = new System.Timers.Timer(2000); // 2 second refresh
            _refreshTimer.Elapsed += async (s, e) => await RefreshProcessesCommand.ExecuteAsync(null);
            // Don't start automatically - only start when needed
        }

        public void PauseRefresh()
        {
            _refreshTimer?.Stop();
            SetStatus("Process monitoring paused (minimized)");
        }

        public void ResumeRefresh()
        {
            _refreshTimer?.Start();
            SetStatus("Process monitoring resumed");
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterProcesses();
        }

        partial void OnShowActiveApplicationsOnlyChanged(bool value)
        {
            _ = Task.Run(async () => await LoadProcessesCommand.ExecuteAsync(null));
        }

        private void FilterProcesses()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredProcesses = new ObservableCollection<ProcessModel>(Processes);
            }
            else
            {
                FilteredProcesses = new ObservableCollection<ProcessModel>(
                    Processes.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                );
            }
        }

        private async Task LoadProcessPowerPlanAssociation(ProcessModel process)
        {
            try
            {
                // For now, just show the current active power plan
                // In a full implementation, this could check for process-specific power plan associations
                var activePlan = await _powerPlanService.GetActivePowerPlan();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid);
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load power plan association for process {ProcessName}", process.Name);
            }
        }

        private void ClearProcessSelection()
        {
            // Clear CPU core selections
            foreach (var core in CpuCores)
            {
                core.IsSelected = false;
            }

            // Reset power plan to current system default
            _ = Task.Run(async () =>
            {
                try
                {
                    var activePlan = await _powerPlanService.GetActivePowerPlan();
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid);
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to reset power plan selection");
                }
            });

            // Notify UI of changes
            OnPropertyChanged(nameof(CpuCores));
            SetStatus("Process selection cleared");
        }
    }
}