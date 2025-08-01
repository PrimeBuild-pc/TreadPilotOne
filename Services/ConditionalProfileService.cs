using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of conditional process profile service
    /// </summary>
    public class ConditionalProfileService : IConditionalProfileService, IDisposable
    {
        private readonly ILogger<ConditionalProfileService> _logger;
        private readonly IProcessService _processService;
        private readonly IRetryPolicyService _retryPolicy;
        private readonly List<ConditionalProcessProfile> _profiles = new();
        private readonly System.Threading.Timer _monitoringTimer;
        private readonly SemaphoreSlim _profileLock = new(1, 1);
        
        private SystemState _lastSystemState = new();
        private bool _isMonitoring;
        private bool _disposed;

        public bool IsMonitoring => _isMonitoring;

        public event EventHandler<ProfileApplicationEventArgs>? ProfileApplied;
        public event EventHandler<ProfileConflictEventArgs>? ProfileConflictResolved;
        public event EventHandler<SystemState>? SystemStateChanged;

        public ConditionalProfileService(
            ILogger<ConditionalProfileService> logger,
            IProcessService processService,
            IRetryPolicyService retryPolicy)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

            // Set up monitoring timer (check every 10 seconds)
            _monitoringTimer = new System.Threading.Timer(MonitoringCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing ConditionalProfileService");
            
            // Load initial system state
            _lastSystemState = await GetSystemStateAsync();
            
            // Create some default profiles for demonstration
            await CreateDefaultProfilesAsync();
        }

        public async Task AddProfileAsync(ConditionalProcessProfile profile)
        {
            await _profileLock.WaitAsync();
            try
            {
                var (isValid, errors) = await ValidateProfileAsync(profile);
                if (!isValid)
                {
                    throw new ArgumentException($"Invalid profile: {string.Join(", ", errors)}");
                }

                _profiles.Add(profile);
                _logger.LogInformation("Added conditional profile: {ProfileName} for process {ProcessName}", 
                    profile.Name, profile.ProcessName);
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task RemoveProfileAsync(string profileId)
        {
            await _profileLock.WaitAsync();
            try
            {
                var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile != null)
                {
                    _profiles.Remove(profile);
                    _logger.LogInformation("Removed conditional profile: {ProfileName}", profile.Name);
                }
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task UpdateProfileAsync(ConditionalProcessProfile profile)
        {
            await _profileLock.WaitAsync();
            try
            {
                var existingProfile = _profiles.FirstOrDefault(p => p.Id == profile.Id);
                if (existingProfile != null)
                {
                    var index = _profiles.IndexOf(existingProfile);
                    _profiles[index] = profile;
                    _logger.LogInformation("Updated conditional profile: {ProfileName}", profile.Name);
                }
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task<List<ConditionalProcessProfile>> GetAllProfilesAsync()
        {
            await _profileLock.WaitAsync();
            try
            {
                return _profiles.ToList();
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task<List<ConditionalProcessProfile>> GetProfilesForProcessAsync(string processName)
        {
            await _profileLock.WaitAsync();
            try
            {
                return _profiles
                    .Where(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task<List<ConditionalProcessProfile>> EvaluateProfilesAsync(ProcessModel process)
        {
            var systemState = await GetSystemStateAsync();
            var applicableProfiles = new List<ConditionalProcessProfile>();

            await _profileLock.WaitAsync();
            try
            {
                var processProfiles = _profiles
                    .Where(p => p.ProcessName.Equals(process.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var profile in processProfiles)
                {
                    if (profile.ShouldApply(process, systemState) && profile.CanApplyNow())
                    {
                        applicableProfiles.Add(profile);
                    }
                }

                // Sort by priority (higher priority first)
                applicableProfiles.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            finally
            {
                _profileLock.Release();
            }

            return applicableProfiles;
        }

        public async Task<bool> ApplyBestProfileAsync(ProcessModel process)
        {
            try
            {
                var applicableProfiles = await EvaluateProfilesAsync(process);
                
                if (!applicableProfiles.Any())
                {
                    return false;
                }

                ConditionalProcessProfile selectedProfile;
                
                if (applicableProfiles.Count == 1)
                {
                    selectedProfile = applicableProfiles[0];
                }
                else
                {
                    // Handle conflicts
                    selectedProfile = ResolveProfileConflict(applicableProfiles, process);
                    
                    ProfileConflictResolved?.Invoke(this, new ProfileConflictEventArgs
                    {
                        ConflictingProfiles = applicableProfiles,
                        Process = process,
                        SelectedProfile = selectedProfile,
                        Resolution = "Priority-based selection"
                    });
                }

                // Apply the profile (simplified - would use actual process service)
                var success = await ApplyProfileToProcessAsync(process, selectedProfile);
                
                if (success)
                {
                    selectedProfile.MarkAsApplied();
                    
                    ProfileApplied?.Invoke(this, new ProfileApplicationEventArgs
                    {
                        Profile = selectedProfile,
                        Process = process,
                        SystemState = await GetSystemStateAsync(),
                        WasApplied = true,
                        Reason = "Conditions satisfied"
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying profile for process {ProcessName}", process.Name);
                return false;
            }
        }

        public async Task<SystemState> GetSystemStateAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var systemState = new SystemState
                {
                    CurrentTime = DateTime.Now,
                    CpuUsage = await GetCpuUsageAsync(),
                    MemoryUsage = await GetMemoryUsageAsync(),
                    ProcessCount = await GetProcessCountAsync(),
                    IsOnBattery = GetBatteryStatus(),
                    BatteryLevel = GetBatteryLevel(),
                    IsUserIdle = GetUserIdleStatus(),
                    UserIdleTime = GetUserIdleTime(),
                    NetworkActivity = await GetNetworkActivityAsync()
                };

                // Check if system state changed significantly
                if (HasSystemStateChangedSignificantly(systemState, _lastSystemState))
                {
                    SystemStateChanged?.Invoke(this, systemState);
                    _lastSystemState = systemState;
                }

                return systemState;
            }, _retryPolicy.CreateProcessOperationPolicy());
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring) return;

            _logger.LogInformation("Starting conditional profile monitoring");
            _isMonitoring = true;
            
            // Start monitoring timer (check every 10 seconds)
            _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring) return;

            _logger.LogInformation("Stopping conditional profile monitoring");
            _isMonitoring = false;
            
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public ConditionalProcessProfile ResolveProfileConflict(List<ConditionalProcessProfile> conflictingProfiles, ProcessModel process)
        {
            // Simple resolution: highest priority wins
            return conflictingProfiles.OrderByDescending(p => p.Priority).First();
        }

        public ConditionalProcessProfile CreateDefaultProfile(string processName)
        {
            return new ConditionalProcessProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Default Profile for {processName}",
                ProcessName = processName,
                Priority = 0,
                AutoApplyDelay = TimeSpan.FromSeconds(5),
                IsAutoApplyEnabled = true,
                ConditionGroups = new List<ConditionGroup>
                {
                    new ConditionGroup
                    {
                        Name = "Default Conditions",
                        LogicalOperator = LogicalOperator.And,
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition
                            {
                                Name = "High CPU Usage",
                                ConditionType = ProfileConditionType.SystemLoad,
                                ComparisonOperator = ComparisonOperator.GreaterThan,
                                Value = 50.0,
                                Description = "Apply when system CPU usage is above 50%"
                            }
                        }
                    }
                }
            };
        }

        public async Task<(bool IsValid, List<string> Errors)> ValidateProfileAsync(ConditionalProcessProfile profile)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(profile.Name))
                errors.Add("Profile name is required");

            if (string.IsNullOrWhiteSpace(profile.ProcessName))
                errors.Add("Process name is required");

            if (profile.AutoApplyDelay < TimeSpan.Zero)
                errors.Add("Auto-apply delay cannot be negative");

            // Validate condition groups
            foreach (var group in profile.ConditionGroups)
            {
                if (!group.Conditions.Any() && !group.SubGroups.Any())
                    errors.Add($"Condition group '{group.Name}' must have at least one condition or sub-group");
            }

            return (errors.Count == 0, errors);
        }

        public async Task<string> ExportProfilesToJsonAsync()
        {
            await _profileLock.WaitAsync();
            try
            {
                return JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            }
            finally
            {
                _profileLock.Release();
            }
        }

        public async Task<int> ImportProfilesFromJsonAsync(string json)
        {
            try
            {
                var importedProfiles = JsonSerializer.Deserialize<List<ConditionalProcessProfile>>(json);
                if (importedProfiles == null) return 0;

                await _profileLock.WaitAsync();
                try
                {
                    var validProfiles = 0;
                    foreach (var profile in importedProfiles)
                    {
                        var (isValid, _) = await ValidateProfileAsync(profile);
                        if (isValid)
                        {
                            _profiles.Add(profile);
                            validProfiles++;
                        }
                    }

                    _logger.LogInformation("Imported {ValidProfiles} valid profiles out of {TotalProfiles}", 
                        validProfiles, importedProfiles.Count);
                    
                    return validProfiles;
                }
                finally
                {
                    _profileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing profiles from JSON");
                return 0;
            }
        }

        private async void MonitoringCallback(object? state)
        {
            if (!_isMonitoring) return;

            try
            {
                var processes = await _processService.GetProcessesAsync();
                foreach (var process in processes)
                {
                    await ApplyBestProfileAsync(process);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during profile monitoring cycle");
            }
        }

        private async Task<bool> ApplyProfileToProcessAsync(ProcessModel process, ConditionalProcessProfile profile)
        {
            try
            {
                // Simplified profile application - would use actual process service methods
                _logger.LogInformation("Applying profile {ProfileName} to process {ProcessName}",
                    profile.Name, process.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying profile {ProfileName} to process {ProcessName}",
                    profile.Name, process.Name);
                return false;
            }
        }

        private async Task CreateDefaultProfilesAsync()
        {
            // Create some example conditional profiles
            var gameProfile = new ConditionalProcessProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "High Performance Gaming",
                ProcessName = "*", // Wildcard for any process
                Priority = 10,
                AutoApplyDelay = TimeSpan.FromSeconds(3),
                ConditionGroups = new List<ConditionGroup>
                {
                    new ConditionGroup
                    {
                        Name = "Gaming Conditions",
                        LogicalOperator = LogicalOperator.And,
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition
                            {
                                Name = "High CPU Usage",
                                ConditionType = ProfileConditionType.SystemLoad,
                                ComparisonOperator = ComparisonOperator.GreaterThan,
                                Value = 70.0
                            },
                            new ProfileCondition
                            {
                                Name = "Evening Hours",
                                ConditionType = ProfileConditionType.TimeOfDay,
                                ComparisonOperator = ComparisonOperator.Between,
                                Value = 18.0, // 6 PM
                                SecondaryValue = 23.0 // 11 PM
                            }
                        }
                    }
                }
            };

            await AddProfileAsync(gameProfile);
        }

        private async Task<double> GetCpuUsageAsync()
        {
            // Simplified CPU usage calculation
            return Environment.ProcessorCount * 10.0; // Placeholder
        }

        private async Task<double> GetMemoryUsageAsync()
        {
            var totalMemory = GC.GetTotalMemory(false);
            return totalMemory / (1024.0 * 1024.0); // MB
        }

        private async Task<int> GetProcessCountAsync()
        {
            var processes = await _processService.GetProcessesAsync();
            return processes.Count;
        }

        private bool GetBatteryStatus()
        {
            return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        }

        private int GetBatteryLevel()
        {
            return (int)(SystemInformation.PowerStatus.BatteryLifePercent * 100);
        }

        private bool GetUserIdleStatus()
        {
            return GetUserIdleTime() > TimeSpan.FromMinutes(5);
        }

        private TimeSpan GetUserIdleTime()
        {
            // Simplified - would use Windows API to get actual idle time
            return TimeSpan.FromMinutes(1);
        }

        private async Task<double> GetNetworkActivityAsync()
        {
            // Simplified network activity measurement
            return 0.0; // Placeholder
        }

        private bool HasSystemStateChangedSignificantly(SystemState current, SystemState previous)
        {
            const double cpuThreshold = 10.0;
            const double memoryThreshold = 100.0; // MB

            return Math.Abs(current.CpuUsage - previous.CpuUsage) > cpuThreshold ||
                   Math.Abs(current.MemoryUsage - previous.MemoryUsage) > memoryThreshold ||
                   current.IsOnBattery != previous.IsOnBattery;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _monitoringTimer?.Dispose();
                    _profileLock?.Dispose();
                    _logger.LogInformation("ConditionalProfileService disposed");
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
