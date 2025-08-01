using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the System Tweaks tab
    /// </summary>
    public partial class SystemTweaksViewModel : BaseViewModel
    {
        private readonly ISystemTweaksService _systemTweaksService;
        private readonly INotificationService _notificationService;

        [ObservableProperty]
        private ObservableCollection<SystemTweakItem> tweakItems = new();

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string refreshStatusText = "Ready";

        public SystemTweaksViewModel(
            ISystemTweaksService systemTweaksService,
            INotificationService notificationService,
            ILogger<SystemTweaksViewModel> logger) : base(logger, null)
        {
            _systemTweaksService = systemTweaksService;
            _notificationService = notificationService;

            // Subscribe to tweak status changes
            _systemTweaksService.TweakStatusChanged += OnTweakStatusChanged;

            InitializeTweakItems();
        }

        private void InitializeTweakItems()
        {
            TweakItems = new ObservableCollection<SystemTweakItem>
            {
                new SystemTweakItem
                {
                    Name = "Core Parking",
                    Description = "Controls CPU core parking for power management",
                    TweakType = SystemTweak.CoreParking,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                },
                new SystemTweakItem
                {
                    Name = "C-States",
                    Description = "Controls CPU C-States for power management",
                    TweakType = SystemTweak.CStates,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                },
                new SystemTweakItem
                {
                    Name = "SysMain Service",
                    Description = "Windows Superfetch/SysMain service for memory management",
                    TweakType = SystemTweak.SysMain,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                },
                new SystemTweakItem
                {
                    Name = "Prefetch",
                    Description = "Windows Prefetch feature for faster application loading",
                    TweakType = SystemTweak.Prefetch,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                },
                new SystemTweakItem
                {
                    Name = "Power Throttling",
                    Description = "Windows Power Throttling for energy efficiency",
                    TweakType = SystemTweak.PowerThrottling,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                },
                new SystemTweakItem
                {
                    Name = "HPET",
                    Description = "High Precision Event Timer for system timing",
                    TweakType = SystemTweak.Hpet,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                },
                new SystemTweakItem
                {
                    Name = "High Scheduling Category",
                    Description = "High scheduling priority for gaming applications",
                    TweakType = SystemTweak.HighSchedulingCategory,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                },
                new SystemTweakItem
                {
                    Name = "Menu Show Delay",
                    Description = "Delay before showing context menus",
                    TweakType = SystemTweak.MenuShowDelay,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(ToggleTweakAsync)
                }
            };
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await ExecuteAsync(async () =>
            {
                await RefreshAllTweaksAsync();
            }, "Loading system tweaks...", "System tweaks loaded successfully");
        }

        [RelayCommand]
        public async Task RefreshAllTweaksAsync()
        {
            try
            {
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsRefreshing = true;
                    RefreshStatusText = "Refreshing system tweaks...";
                });

                await _systemTweaksService.RefreshAllStatusesAsync();

                // Update each tweak item with current status
                foreach (var item in TweakItems)
                {
                    await UpdateTweakItemStatusAsync(item);
                }

                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    RefreshStatusText = $"Last refreshed: {DateTime.Now:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetError("Failed to refresh system tweaks", ex);
                    RefreshStatusText = "Refresh failed";
                });
            }
            finally
            {
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsRefreshing = false;
                });
            }
        }

        private async Task UpdateTweakItemStatusAsync(SystemTweakItem item)
        {
            try
            {
                TweakStatus status = item.TweakType switch
                {
                    SystemTweak.CoreParking => await _systemTweaksService.GetCoreParkingStatusAsync(),
                    SystemTweak.CStates => await _systemTweaksService.GetCStatesStatusAsync(),
                    SystemTweak.SysMain => await _systemTweaksService.GetSysMainStatusAsync(),
                    SystemTweak.Prefetch => await _systemTweaksService.GetPrefetchStatusAsync(),
                    SystemTweak.PowerThrottling => await _systemTweaksService.GetPowerThrottlingStatusAsync(),
                    SystemTweak.Hpet => await _systemTweaksService.GetHpetStatusAsync(),
                    SystemTweak.HighSchedulingCategory => await _systemTweaksService.GetHighSchedulingCategoryStatusAsync(),
                    SystemTweak.MenuShowDelay => await _systemTweaksService.GetMenuShowDelayStatusAsync(),
                    _ => new TweakStatus { IsAvailable = false, ErrorMessage = "Unknown tweak type" }
                };

                item.IsEnabled = status.IsEnabled;
                item.IsAvailable = status.IsAvailable;
                item.ErrorMessage = status.ErrorMessage;
                if (!string.IsNullOrEmpty(status.Description))
                {
                    item.Description = status.Description;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating status for tweak {TweakName}", item.Name);
                item.IsAvailable = false;
                item.ErrorMessage = ex.Message;
            }
        }

        private async Task ToggleTweakAsync(SystemTweakItem? item)
        {
            if (item == null) return;

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetStatus($"Toggling {item.Name}...");
                });

                var newState = !item.IsEnabled;
                bool success = item.TweakType switch
                {
                    SystemTweak.CoreParking => await _systemTweaksService.SetCoreParkingAsync(newState),
                    SystemTweak.CStates => await _systemTweaksService.SetCStatesAsync(newState),
                    SystemTweak.SysMain => await _systemTweaksService.SetSysMainAsync(newState),
                    SystemTweak.Prefetch => await _systemTweaksService.SetPrefetchAsync(newState),
                    SystemTweak.PowerThrottling => await _systemTweaksService.SetPowerThrottlingAsync(newState),
                    SystemTweak.Hpet => await _systemTweaksService.SetHpetAsync(newState),
                    SystemTweak.HighSchedulingCategory => await _systemTweaksService.SetHighSchedulingCategoryAsync(newState),
                    SystemTweak.MenuShowDelay => await _systemTweaksService.SetMenuShowDelayAsync(newState),
                    _ => false
                };

                if (success)
                {
                    await UpdateTweakItemStatusAsync(item);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetStatus($"{item.Name} {(newState ? "enabled" : "disabled")} successfully");
                    });

                    await _notificationService.ShowSuccessNotificationAsync(
                        "System Tweak Updated",
                        $"{item.Name} has been {(newState ? "enabled" : "disabled")}");
                }
                else
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetError($"Failed to toggle {item.Name}", null);
                    });

                    await _notificationService.ShowErrorNotificationAsync(
                        "System Tweak Failed",
                        $"Failed to {(newState ? "enable" : "disable")} {item.Name}");
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SetError($"Error toggling {item.Name}", ex);
                });
                Logger.LogError(ex, "Error toggling tweak {TweakName}", item.Name);
            }
        }

        private void OnTweakStatusChanged(object? sender, TweakStatusChangedEventArgs e)
        {
            try
            {
                var item = TweakItems.FirstOrDefault(t => t.TweakType.ToString() == e.TweakName);
                if (item != null)
                {
                    item.IsEnabled = e.Status.IsEnabled;
                    item.IsAvailable = e.Status.IsAvailable;
                    item.ErrorMessage = e.Status.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling tweak status change for {TweakName}", e.TweakName);
            }
        }

        protected override void OnDispose()
        {
            _systemTweaksService.TweakStatusChanged -= OnTweakStatusChanged;
            base.OnDispose();
        }
    }

    /// <summary>
    /// Represents a system tweak item in the UI
    /// </summary>
    public partial class SystemTweakItem : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private SystemTweak tweakType;

        [ObservableProperty]
        private bool isEnabled;

        [ObservableProperty]
        private bool isAvailable = true;

        [ObservableProperty]
        private string? errorMessage;

        public IAsyncRelayCommand<SystemTweakItem>? ToggleCommand { get; set; }
    }
}
