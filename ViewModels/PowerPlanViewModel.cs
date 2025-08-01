using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot.ViewModels
{
    public partial class PowerPlanViewModel : BaseViewModel
    {
        private readonly IPowerPlanService _powerPlanService;
        private System.Timers.Timer? _refreshTimer;

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> powerPlans = new();

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> customPowerPlans = new();

        [ObservableProperty]
        private PowerPlanModel? selectedPowerPlan;

        [ObservableProperty]
        private PowerPlanModel? selectedCustomPlan;

        [ObservableProperty]
        private PowerPlanModel? activePowerPlan;

        public PowerPlanViewModel(
            ILogger<PowerPlanViewModel> logger,
            IPowerPlanService powerPlanService,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            _powerPlanService = powerPlanService;
            SetupRefreshTimer();
        }

        private void SetupRefreshTimer()
        {
            _refreshTimer = new System.Timers.Timer(10000); // PERFORMANCE OPTIMIZATION: Increased to 10 second refresh - power plans change infrequently
            _refreshTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    // Marshal timer callback to UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await RefreshPowerPlansCommand.ExecuteAsync(null);
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Power plan refresh timer error: {ex.Message}");
                }
            };
            _refreshTimer.Start();
        }

        [RelayCommand]
        public async Task LoadPowerPlans()
        {
            try
            {
                SetStatus("Loading power plans...");
                PowerPlans = await _powerPlanService.GetPowerPlansAsync();
                CustomPowerPlans = await _powerPlanService.GetCustomPowerPlansAsync();
                ActivePowerPlan = await _powerPlanService.GetActivePowerPlan();
                ClearStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading power plans: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task RefreshPowerPlans()
        {
            if (IsBusy) return;

            try
            {
                var currentPlans = await _powerPlanService.GetPowerPlansAsync();
                var currentActive = await _powerPlanService.GetActivePowerPlan();
                var customPlans = await _powerPlanService.GetCustomPowerPlansAsync();

                // Update power plans
                PowerPlans = new ObservableCollection<PowerPlanModel>(currentPlans);
                CustomPowerPlans = new ObservableCollection<PowerPlanModel>(customPlans);
                ActivePowerPlan = currentActive;

                // Update selected plan if it exists
                if (SelectedPowerPlan != null)
                {
                    SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.Guid == SelectedPowerPlan.Guid);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error refreshing power plans: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task SetActivePlan()
        {
            if (SelectedPowerPlan == null) return;

            try
            {
                SetStatus($"Setting active power plan to {SelectedPowerPlan.Name}...");
                var success = await _powerPlanService.SetActivePowerPlan(SelectedPowerPlan);
                
                if (success)
                {
                    ActivePowerPlan = SelectedPowerPlan;
                    await RefreshPowerPlans();
                    ClearStatus();
                }
                else
                {
                    SetStatus($"Failed to set power plan {SelectedPowerPlan.Name}", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error setting power plan: {ex.Message}", false);
            }
        }

        [RelayCommand]
        private async Task ImportCustomPlan()
        {
            if (SelectedCustomPlan == null) return;

            try
            {
                SetStatus($"Importing custom power plan {SelectedCustomPlan.Name}...");
                var success = await _powerPlanService.ImportCustomPowerPlan(SelectedCustomPlan.FilePath);
                
                if (success)
                {
                    await RefreshPowerPlans();
                    ClearStatus();
                }
                else
                {
                    SetStatus($"Failed to import power plan {SelectedCustomPlan.Name}", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error importing power plan: {ex.Message}", false);
            }
        }
    }
}