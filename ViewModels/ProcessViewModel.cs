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

        [ObservableProperty]
        private bool enableHyperThreading = true;

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
                // Refresh process info to get current affinity
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _processService.RefreshProcessInfo(value);
                        // Update UI on main thread
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateCoreSelections(value.ProcessorAffinity);
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to refresh process info for {ProcessName}", value.Name);
                    }
                });
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
            if (CpuTopology == null) return;

            foreach (var core in CpuCores)
            {
                core.IsSelected = (affinityMask & core.AffinityMask) != 0;
            }
        }

        private long CalculateAffinityMask()
        {
            if (CpuTopology == null) return 0;

            var selectedCores = CpuCores.Where(core => core.IsSelected);

            // If HyperThreading is disabled, exclude HT siblings
            if (!EnableHyperThreading && CpuTopology.HasHyperThreading)
            {
                selectedCores = selectedCores.Where(core => !core.IsHyperThreaded || core.HyperThreadSibling == null ||
                    core.LogicalCoreId < core.HyperThreadSibling);
            }

            return selectedCores.Aggregate(0L, (mask, core) => mask | core.AffinityMask);
        }

        [RelayCommand]
        public async Task LoadProcesses()
        {
            try
            {
                SetStatus("Loading processes...");
                Processes = await _processService.GetProcessesAsync();
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
                var currentProcesses = await _processService.GetProcessesAsync();
                
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

                foreach (var terminated in terminatedProcesses)
                {
                    Processes.Remove(terminated);
                }

                FilterProcesses();
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
                await _processService.SetProcessorAffinity(SelectedProcess, affinityMask);

                // Refresh the process to get updated affinity from system
                await _processService.RefreshProcessInfo(SelectedProcess);

                // Update UI to reflect the actual system affinity
                UpdateCoreSelections(SelectedProcess.ProcessorAffinity);

                // Notify UI of changes
                OnPropertyChanged(nameof(SelectedProcess));
                OnPropertyChanged(nameof(CpuCores));

                SetStatus($"Affinity set successfully for {SelectedProcess.Name}");
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting affinity: {ex.Message}", false);
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
                await _powerPlanService.SetActivePowerPlan(SelectedPowerPlan);
                SetStatus($"Power plan set to {SelectedPowerPlan.Name}");
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting power plan: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task SetPriority(ProcessPriorityClass priority)
        {
            if (SelectedProcess == null) return;

            try
            {
                SetStatus($"Setting priority for {SelectedProcess.Name}...");
                await _processService.SetProcessPriority(SelectedProcess, priority);
                ClearStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting priority: {ex.Message}", false);
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
    }
}