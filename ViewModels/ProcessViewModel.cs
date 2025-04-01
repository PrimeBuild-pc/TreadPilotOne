using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public partial class ProcessViewModel : BaseViewModel
    {
        private readonly IProcessService _processService;
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

        // CPU Affinity checkbox properties
        [ObservableProperty]
        private bool isCpu0Selected;
        [ObservableProperty]
        private bool isCpu1Selected;
        [ObservableProperty]
        private bool isCpu2Selected;
        [ObservableProperty]
        private bool isCpu3Selected;
        [ObservableProperty]
        private bool isCpu4Selected;
        [ObservableProperty]
        private bool isCpu5Selected;
        [ObservableProperty]
        private bool isCpu6Selected;
        [ObservableProperty]
        private bool isCpu7Selected;
        [ObservableProperty]
        private bool isCpu8Selected;
        [ObservableProperty]
        private bool isCpu9Selected;
        [ObservableProperty]
        private bool isCpu10Selected;
        [ObservableProperty]
        private bool isCpu11Selected;
        [ObservableProperty]
        private bool isCpu12Selected;
        [ObservableProperty]
        private bool isCpu13Selected;
        [ObservableProperty]
        private bool isCpu14Selected;
        [ObservableProperty]
        private bool isCpu15Selected;

        public ProcessViewModel(IProcessService processService)
        {
            _processService = processService;
            SetupRefreshTimer();
        }

        partial void OnSelectedProcessChanged(ProcessModel? value)
        {
            if (value != null)
            {
                UpdateAffinityCheckboxes(value.ProcessorAffinity);
            }
        }

        private void UpdateAffinityCheckboxes(long affinityMask)
        {
            IsCpu0Selected = (affinityMask & 1L) != 0;
            IsCpu1Selected = (affinityMask & 2L) != 0;
            IsCpu2Selected = (affinityMask & 4L) != 0;
            IsCpu3Selected = (affinityMask & 8L) != 0;
            IsCpu4Selected = (affinityMask & 16L) != 0;
            IsCpu5Selected = (affinityMask & 32L) != 0;
            IsCpu6Selected = (affinityMask & 64L) != 0;
            IsCpu7Selected = (affinityMask & 128L) != 0;
            IsCpu8Selected = (affinityMask & 256L) != 0;
            IsCpu9Selected = (affinityMask & 512L) != 0;
            IsCpu10Selected = (affinityMask & 1024L) != 0;
            IsCpu11Selected = (affinityMask & 2048L) != 0;
            IsCpu12Selected = (affinityMask & 4096L) != 0;
            IsCpu13Selected = (affinityMask & 8192L) != 0;
            IsCpu14Selected = (affinityMask & 16384L) != 0;
            IsCpu15Selected = (affinityMask & 32768L) != 0;
        }

        private long CalculateAffinityMask()
        {
            long mask = 0;
            if (IsCpu0Selected) mask |= 1L;
            if (IsCpu1Selected) mask |= 2L;
            if (IsCpu2Selected) mask |= 4L;
            if (IsCpu3Selected) mask |= 8L;
            if (IsCpu4Selected) mask |= 16L;
            if (IsCpu5Selected) mask |= 32L;
            if (IsCpu6Selected) mask |= 64L;
            if (IsCpu7Selected) mask |= 128L;
            if (IsCpu8Selected) mask |= 256L;
            if (IsCpu9Selected) mask |= 512L;
            if (IsCpu10Selected) mask |= 1024L;
            if (IsCpu11Selected) mask |= 2048L;
            if (IsCpu12Selected) mask |= 4096L;
            if (IsCpu13Selected) mask |= 8192L;
            if (IsCpu14Selected) mask |= 16384L;
            if (IsCpu15Selected) mask |= 32768L;
            return mask;
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
                SetStatus($"Setting affinity for {SelectedProcess.Name}...");
                await _processService.SetProcessorAffinity(SelectedProcess, affinityMask);
                ClearStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting affinity: {ex.Message}", false);
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
            _refreshTimer.Start();
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