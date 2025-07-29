using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing Windows system tweaks and optimizations
    /// </summary>
    public class SystemTweaksService : ISystemTweaksService
    {
        private readonly ILogger<SystemTweaksService> _logger;
        private readonly IElevationService _elevationService;

        public event EventHandler<TweakStatusChangedEventArgs>? TweakStatusChanged;

        public SystemTweaksService(
            ILogger<SystemTweaksService> logger,
            IElevationService elevationService)
        {
            _logger = logger;
            _elevationService = elevationService;
        }

        public async Task<TweakStatus> GetCoreParkingStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583");
                if (key == null)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Core Parking registry key not found" };
                }

                var attributes = key.GetValue("Attributes");
                var isEnabled = attributes?.ToString() != "1"; // 1 = hidden (disabled), 2 = visible (enabled)

                return new TweakStatus 
                { 
                    IsEnabled = isEnabled, 
                    IsAvailable = true,
                    Description = "Controls CPU core parking for power management"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Core Parking status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetCoreParkingAsync(bool enabled)
        {
            try
            {
                if (!_elevationService.IsRunningAsAdministrator())
                {
                    _logger.LogWarning("Administrator privileges required to modify Core Parking");
                    return false;
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583", true);
                if (key == null)
                {
                    _logger.LogError("Core Parking registry key not found");
                    return false;
                }

                // Set attributes: 1 = hidden (disabled), 2 = visible (enabled)
                key.SetValue("Attributes", enabled ? 2 : 1, RegistryValueKind.DWord);

                var status = await GetCoreParkingStatusAsync();
                TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("CoreParking", status));

                _logger.LogInformation("Core Parking {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Core Parking to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetCStatesStatusAsync()
        {
            try
            {
                // Check C-States via registry
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Processor");
                if (key == null)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Processor registry key not found" };
                }

                var cStateDisable = key.GetValue("CStateDisable");
                var isEnabled = cStateDisable?.ToString() != "1"; // 1 = disabled, 0 or null = enabled

                return new TweakStatus 
                { 
                    IsEnabled = isEnabled, 
                    IsAvailable = true,
                    Description = "Controls CPU C-States for power management"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting C-States status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetCStatesAsync(bool enabled)
        {
            try
            {
                if (!_elevationService.IsRunningAsAdministrator())
                {
                    _logger.LogWarning("Administrator privileges required to modify C-States");
                    return false;
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Processor", true);
                if (key == null)
                {
                    _logger.LogError("Processor registry key not found");
                    return false;
                }

                // Set CStateDisable: 1 = disabled, 0 = enabled
                key.SetValue("CStateDisable", enabled ? 0 : 1, RegistryValueKind.DWord);

                var status = await GetCStatesStatusAsync();
                TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("CStates", status));

                _logger.LogInformation("C-States {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting C-States to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetSysMainStatusAsync()
        {
            try
            {
                using var serviceController = new ServiceController("SysMain");
                var isEnabled = serviceController.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                var isAvailable = true;

                return new TweakStatus 
                { 
                    IsEnabled = isEnabled, 
                    IsAvailable = isAvailable,
                    Description = "Windows Superfetch/SysMain service for memory management"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SysMain status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetSysMainAsync(bool enabled)
        {
            try
            {
                if (!_elevationService.IsRunningAsAdministrator())
                {
                    _logger.LogWarning("Administrator privileges required to modify SysMain service");
                    return false;
                }

                using var serviceController = new ServiceController("SysMain");
                
                if (enabled && serviceController.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                {
                    serviceController.Start();
                    serviceController.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                else if (!enabled && serviceController.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                {
                    serviceController.Stop();
                    serviceController.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }

                var status = await GetSysMainStatusAsync();
                TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("SysMain", status));

                _logger.LogInformation("SysMain service {Status}", enabled ? "started" : "stopped");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting SysMain service to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetPrefetchStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters");
                if (key == null)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Prefetch registry key not found" };
                }

                var enablePrefetcher = key.GetValue("EnablePrefetcher");
                var isEnabled = enablePrefetcher?.ToString() != "0"; // 0 = disabled, 1-3 = enabled

                return new TweakStatus 
                { 
                    IsEnabled = isEnabled, 
                    IsAvailable = true,
                    Description = "Windows Prefetch feature for faster application loading"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Prefetch status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetPrefetchAsync(bool enabled)
        {
            try
            {
                if (!_elevationService.IsRunningAsAdministrator())
                {
                    _logger.LogWarning("Administrator privileges required to modify Prefetch");
                    return false;
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", true);
                if (key == null)
                {
                    _logger.LogError("Prefetch registry key not found");
                    return false;
                }

                // Set EnablePrefetcher: 0 = disabled, 3 = enabled for both applications and boot
                key.SetValue("EnablePrefetcher", enabled ? 3 : 0, RegistryValueKind.DWord);

                var status = await GetPrefetchStatusAsync();
                TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("Prefetch", status));

                _logger.LogInformation("Prefetch {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Prefetch to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetPowerThrottlingStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling");
                if (key == null)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Power Throttling not available on this system" };
                }

                var powerThrottlingOff = key.GetValue("PowerThrottlingOff");
                var isEnabled = powerThrottlingOff?.ToString() != "1"; // 1 = disabled, 0 or null = enabled

                return new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "Windows Power Throttling for energy efficiency"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Power Throttling status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetPowerThrottlingAsync(bool enabled)
        {
            try
            {
                if (!_elevationService.IsRunningAsAdministrator())
                {
                    _logger.LogWarning("Administrator privileges required to modify Power Throttling");
                    return false;
                }

                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling");
                if (key == null)
                {
                    _logger.LogError("Could not create Power Throttling registry key");
                    return false;
                }

                // Set PowerThrottlingOff: 1 = disabled, 0 = enabled
                key.SetValue("PowerThrottlingOff", enabled ? 0 : 1, RegistryValueKind.DWord);

                var status = await GetPowerThrottlingStatusAsync();
                TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("PowerThrottling", status));

                _logger.LogInformation("Power Throttling {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Power Throttling to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetHpetStatusAsync()
        {
            try
            {
                // Check HPET status via bcdedit
                var processInfo = new ProcessStartInfo
                {
                    FileName = "bcdedit",
                    Arguments = "/enum",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Could not start bcdedit process" };
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var isEnabled = !output.Contains("useplatformclock") || output.Contains("useplatformclock        Yes");

                return new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "High Precision Event Timer for system timing"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HPET status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetHpetAsync(bool enabled)
        {
            try
            {
                if (!_elevationService.IsRunningAsAdministrator())
                {
                    _logger.LogWarning("Administrator privileges required to modify HPET");
                    return false;
                }

                var arguments = enabled ? "/set useplatformclock true" : "/set useplatformclock false";
                var processInfo = new ProcessStartInfo
                {
                    FileName = "bcdedit",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    _logger.LogError("Could not start bcdedit process");
                    return false;
                }

                await process.WaitForExitAsync();
                var success = process.ExitCode == 0;

                if (success)
                {
                    var status = await GetHpetStatusAsync();
                    TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("Hpet", status));
                    _logger.LogInformation("HPET {Status}", enabled ? "enabled" : "disabled");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting HPET to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetHighSchedulingCategoryStatusAsync()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games");
                if (key == null)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Games scheduling registry key not found" };
                }

                var priority = key.GetValue("Priority");
                var isEnabled = priority?.ToString() == "6"; // 6 = High priority

                return new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "High scheduling priority for gaming applications"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting High Scheduling Category status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetHighSchedulingCategoryAsync(bool enabled)
        {
            try
            {
                if (!_elevationService.IsRunningAsAdministrator())
                {
                    _logger.LogWarning("Administrator privileges required to modify High Scheduling Category");
                    return false;
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true);
                if (key == null)
                {
                    _logger.LogError("Games scheduling registry key not found");
                    return false;
                }

                // Set Priority: 6 = High, 2 = Normal
                key.SetValue("Priority", enabled ? 6 : 2, RegistryValueKind.DWord);

                var status = await GetHighSchedulingCategoryStatusAsync();
                TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("HighSchedulingCategory", status));

                _logger.LogInformation("High Scheduling Category {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting High Scheduling Category to {Enabled}", enabled);
                return false;
            }
        }

        public async Task<TweakStatus> GetMenuShowDelayStatusAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key == null)
                {
                    return new TweakStatus { IsAvailable = false, ErrorMessage = "Desktop registry key not found" };
                }

                var menuShowDelay = key.GetValue("MenuShowDelay");
                var isEnabled = menuShowDelay?.ToString() != "0"; // 0 = no delay, >0 = delay enabled

                return new TweakStatus
                {
                    IsEnabled = isEnabled,
                    IsAvailable = true,
                    Description = "Delay before showing context menus"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Menu Show Delay status");
                return new TweakStatus { IsAvailable = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> SetMenuShowDelayAsync(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (key == null)
                {
                    _logger.LogError("Desktop registry key not found");
                    return false;
                }

                // Set MenuShowDelay: 0 = no delay, 400 = default delay
                key.SetValue("MenuShowDelay", enabled ? "400" : "0", RegistryValueKind.String);

                var status = await GetMenuShowDelayStatusAsync();
                TweakStatusChanged?.Invoke(this, new TweakStatusChangedEventArgs("MenuShowDelay", status));

                _logger.LogInformation("Menu Show Delay {Status}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Menu Show Delay to {Enabled}", enabled);
                return false;
            }
        }

        public async Task RefreshAllStatusesAsync()
        {
            try
            {
                _logger.LogInformation("Refreshing all system tweak statuses");

                var tasks = new[]
                {
                    GetCoreParkingStatusAsync(),
                    GetCStatesStatusAsync(),
                    GetSysMainStatusAsync(),
                    GetPrefetchStatusAsync(),
                    GetPowerThrottlingStatusAsync(),
                    GetHpetStatusAsync(),
                    GetHighSchedulingCategoryStatusAsync(),
                    GetMenuShowDelayStatusAsync()
                };

                await Task.WhenAll(tasks);
                _logger.LogInformation("All system tweak statuses refreshed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing system tweak statuses");
            }
        }
    }
}
