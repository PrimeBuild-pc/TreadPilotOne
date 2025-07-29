using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public class PowerPlanService : IPowerPlanService
    {
        private const string PowerPlansPath = @"C:\Users\Administrator\Desktop\Project\ThreadPilot_1\Powerplans";
        private readonly object _lockObject = new();
        private readonly ILogger<PowerPlanService> _logger;
        private readonly IEnhancedLoggingService _enhancedLogger;
        private string? _lastActivePowerPlanGuid;

        public event EventHandler<PowerPlanChangedEventArgs>? PowerPlanChanged;

        public PowerPlanService(ILogger<PowerPlanService> logger, IEnhancedLoggingService enhancedLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _enhancedLogger = enhancedLogger ?? throw new ArgumentNullException(nameof(enhancedLogger));
        }

        public async Task<ObservableCollection<PowerPlanModel>> GetPowerPlansAsync()
        {
            return await Task.Run(async () =>
            {
                var powerPlans = new ObservableCollection<PowerPlanModel>();
                var activePlan = await GetActivePowerPlan();

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "/list",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var regex = new Regex(@"Power Scheme GUID: (.*?)  \((.*?)\)", RegexOptions.Multiline);
                var matches = regex.Matches(output);

                foreach (Match match in matches)
                {
                    var guid = match.Groups[1].Value.Trim();
                    var name = match.Groups[2].Value.Trim();

                    var plan = new PowerPlanModel
                    {
                        Guid = guid,
                        Name = name,
                        IsActive = guid == activePlan?.Guid,
                        IsCustomPlan = false
                    };

                    powerPlans.Add(plan);
                }

                return powerPlans;
            });
        }

        public async Task<ObservableCollection<PowerPlanModel>> GetCustomPowerPlansAsync()
        {
            return await Task.Run(() =>
            {
                var customPlans = new ObservableCollection<PowerPlanModel>();
                if (!Directory.Exists(PowerPlansPath))
                    return customPlans;

                foreach (var file in Directory.GetFiles(PowerPlansPath, "*.pow"))
                {
                    customPlans.Add(new PowerPlanModel
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        IsCustomPlan = true
                    });
                }

                return customPlans;
            });
        }

        public async Task<bool> SetActivePowerPlan(PowerPlanModel powerPlan)
        {
            return await SetActivePowerPlanByGuidAsync(powerPlan.Guid, false);
        }

        public async Task<bool> SetActivePowerPlanByGuidAsync(string powerPlanGuid, bool preventDuplicateChanges = true)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // Check if change is needed when duplicate prevention is enabled
                    if (preventDuplicateChanges)
                    {
                        var isChangeNeeded = await IsPowerPlanChangeNeededAsync(powerPlanGuid);
                        if (!isChangeNeeded)
                        {
                            _logger.LogDebug("Power plan change skipped - already active: {PowerPlanGuid}", powerPlanGuid);
                            return true; // No change needed, consider it successful
                        }
                    }

                    var previousPowerPlan = await GetActivePowerPlan();
                    var targetPowerPlan = await GetPowerPlanByGuidAsync(powerPlanGuid);

                    _logger.LogInformation("Attempting to change power plan from '{FromPlan}' to '{ToPlan}'",
                        previousPowerPlan?.Name ?? "Unknown", targetPowerPlan?.Name ?? "Unknown");

                    await _enhancedLogger.LogPowerPlanChangeAsync(
                        previousPowerPlan?.Name ?? "Unknown",
                        targetPowerPlan?.Name ?? "Unknown",
                        "Manual power plan change requested");

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = $"/setactive {powerPlanGuid}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            Verb = "runas" // Run with elevated privileges
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    var success = process.ExitCode == 0;

                    if (success)
                    {
                        lock (_lockObject)
                        {
                            _lastActivePowerPlanGuid = powerPlanGuid;
                        }

                        var newPowerPlan = await GetPowerPlanByGuidAsync(powerPlanGuid);

                        _logger.LogInformation("Power plan successfully changed to '{PowerPlan}'", newPowerPlan?.Name ?? "Unknown");

                        await _enhancedLogger.LogPowerPlanChangeAsync(
                            previousPowerPlan?.Name ?? "Unknown",
                            newPowerPlan?.Name ?? "Unknown",
                            "Manual power plan change completed");

                        PowerPlanChanged?.Invoke(this, new PowerPlanChangedEventArgs(
                            previousPowerPlan, newPowerPlan, "Manual power plan change"));
                    }
                    else
                    {
                        _logger.LogWarning("Failed to change power plan to '{PowerPlanGuid}' - powercfg exit code: {ExitCode}",
                            powerPlanGuid, process.ExitCode);

                        await _enhancedLogger.LogSystemEventAsync(LogEventTypes.PowerPlan.ChangeFailed,
                            $"Failed to change power plan to '{targetPowerPlan?.Name ?? powerPlanGuid}' - Exit code: {process.ExitCode}",
                            Microsoft.Extensions.Logging.LogLevel.Warning);
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred while changing power plan to '{PowerPlanGuid}'", powerPlanGuid);

                    await _enhancedLogger.LogErrorAsync(ex, "PowerPlanService.SetActivePowerPlanByGuidAsync",
                        new Dictionary<string, object>
                        {
                            ["PowerPlanGuid"] = powerPlanGuid,
                            ["PreventDuplicateChanges"] = preventDuplicateChanges
                        });

                    return false;
                }
            });
        }

        public async Task<PowerPlanModel?> GetActivePowerPlan()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = "/getactivescheme",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    var regex = new Regex(@"Power Scheme GUID: (.*?)  \((.*?)\)", RegexOptions.Multiline);
                    var match = regex.Match(output);

                    if (match.Success)
                    {
                        return new PowerPlanModel
                        {
                            Guid = match.Groups[1].Value.Trim(),
                            Name = match.Groups[2].Value.Trim(),
                            IsActive = true
                        };
                    }

                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        public async Task<bool> ImportCustomPowerPlan(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = $"/import \"{filePath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            Verb = "runas" // Run with elevated privileges
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    return process.ExitCode == 0;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public async Task<string?> GetActivePowerPlanGuidAsync()
        {
            var activePlan = await GetActivePowerPlan();
            return activePlan?.Guid;
        }

        public async Task<bool> PowerPlanExistsAsync(string powerPlanGuid)
        {
            var powerPlans = await GetPowerPlansAsync();
            return powerPlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<PowerPlanModel?> GetPowerPlanByGuidAsync(string powerPlanGuid)
        {
            var powerPlans = await GetPowerPlansAsync();
            return powerPlans.FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> IsPowerPlanChangeNeededAsync(string targetPowerPlanGuid)
        {
            try
            {
                var currentGuid = await GetActivePowerPlanGuidAsync();

                // Check if the target power plan is already active
                if (string.Equals(currentGuid, targetPowerPlanGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // No change needed
                }

                // Check if we recently set this power plan (to prevent rapid switching)
                lock (_lockObject)
                {
                    if (string.Equals(_lastActivePowerPlanGuid, targetPowerPlanGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // We recently set this plan
                    }
                }

                return true; // Change is needed
            }
            catch
            {
                return true; // If we can't determine, assume change is needed
            }
        }
    }
}