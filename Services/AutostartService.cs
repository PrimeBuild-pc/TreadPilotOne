using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing Windows autostart functionality using registry
    /// </summary>
    public class AutostartService : IAutostartService
    {
        private const string REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APPLICATION_NAME = "ThreadPilot";
        private const string SCHEDULED_TASK_NAME = "ThreadPilot_Startup";

        private readonly ILogger<AutostartService> _logger;
        private readonly IElevationService _elevationService;
        private bool _isAutostartEnabled;
        private string? _autostartPath;

        public event EventHandler<AutostartStatusChangedEventArgs>? AutostartStatusChanged;

        public bool IsAutostartEnabled => _isAutostartEnabled;
        public string? AutostartPath => _autostartPath;

        public AutostartService(ILogger<AutostartService> logger, IElevationService elevationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _elevationService = elevationService ?? throw new ArgumentNullException(nameof(elevationService));

            // Initialize current status
            _ = Task.Run(CheckAutostartStatusAsync);
        }

        public async Task<bool> EnableAutostartAsync(bool startMinimized = true)
        {
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Could not determine executable path for autostart");
                    return false;
                }

                var arguments = GetAutostartArguments(startMinimized);
                var fullCommand = $"\"{executablePath}\" {arguments}";

                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true);
                if (key == null)
                {
                    _logger.LogError("Could not open registry key for autostart");
                    return false;
                }

                key.SetValue(APPLICATION_NAME, fullCommand, RegistryValueKind.String);

                // For elevated apps, also create a scheduled task as backup
                if (_elevationService.IsRunningAsAdministrator())
                {
                    await CreateElevatedStartupTask(executablePath, arguments);
                }

                _isAutostartEnabled = true;
                _autostartPath = fullCommand;

                _logger.LogInformation("Autostart enabled: {Command}", fullCommand);

                AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(
                    true, startMinimized, fullCommand));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable autostart");
                
                AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(
                    false, startMinimized, null, ex));
                
                return false;
            }
        }

        public async Task<bool> DisableAutostartAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true);
                if (key == null)
                {
                    _logger.LogWarning("Could not open registry key for autostart removal");
                    return false;
                }

                if (key.GetValue(APPLICATION_NAME) != null)
                {
                    key.DeleteValue(APPLICATION_NAME, false);
                    _logger.LogInformation("Autostart disabled");
                }

                // Also remove the scheduled task if it exists
                await RemoveElevatedStartupTask();

                _isAutostartEnabled = false;
                _autostartPath = null;

                AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(false));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable autostart");
                
                AutostartStatusChanged?.Invoke(this, new AutostartStatusChangedEventArgs(
                    false, false, null, ex));
                
                return false;
            }
        }

        public async Task<bool> CheckAutostartStatusAsync()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, false);
                if (key == null)
                {
                    _isAutostartEnabled = false;
                    _autostartPath = null;
                    return false;
                }

                var value = key.GetValue(APPLICATION_NAME) as string;
                _isAutostartEnabled = !string.IsNullOrEmpty(value);
                _autostartPath = value;

                _logger.LogDebug("Autostart status checked: {IsEnabled}, Path: {Path}", 
                    _isAutostartEnabled, _autostartPath);

                return _isAutostartEnabled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check autostart status");
                _isAutostartEnabled = false;
                _autostartPath = null;
                return false;
            }
        }

        public async Task<bool> UpdateAutostartAsync(bool startMinimized = true)
        {
            if (!_isAutostartEnabled)
            {
                return await EnableAutostartAsync(startMinimized);
            }

            // Re-enable with new parameters
            await DisableAutostartAsync();
            return await EnableAutostartAsync(startMinimized);
        }

        public string GetAutostartArguments(bool startMinimized = true)
        {
            var args = new System.Collections.Generic.List<string>();
            
            if (startMinimized)
            {
                args.Add("--start-minimized");
            }
            
            // Add any other startup arguments as needed
            args.Add("--autostart");

            return string.Join(" ", args);
        }

        private string? GetExecutablePath()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var location = assembly.Location;
                
                // Handle .NET Core/5+ scenarios where Location might be empty
                if (string.IsNullOrEmpty(location))
                {
                    location = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                }

                // If we're running from a .dll, try to find the .exe
                if (!string.IsNullOrEmpty(location) && location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var exePath = Path.ChangeExtension(location, ".exe");
                    if (File.Exists(exePath))
                    {
                        location = exePath;
                    }
                }

                return location;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get executable path");
                return null;
            }
        }

        /// <summary>
        /// Creates a scheduled task for elevated startup as a backup to registry autostart
        /// </summary>
        private async Task CreateElevatedStartupTask(string executablePath, string arguments)
        {
            try
            {
                var taskArguments = $"/Create /TN \"{SCHEDULED_TASK_NAME}\" /TR \"\\\"{executablePath}\\\" {arguments}\" " +
                                   "/SC ONLOGON /RL HIGHEST /F /RU \"{Environment.UserName}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = taskArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Created elevated startup task successfully");
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        _logger.LogWarning("Failed to create elevated startup task. Exit code: {ExitCode}, Error: {Error}",
                            process.ExitCode, error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create elevated startup task");
            }
        }

        /// <summary>
        /// Removes the elevated startup scheduled task
        /// </summary>
        private async Task RemoveElevatedStartupTask()
        {
            try
            {
                var taskArguments = $"/Delete /TN \"{SCHEDULED_TASK_NAME}\" /F";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = taskArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Removed elevated startup task successfully");
                    }
                    else
                    {
                        // Task might not exist, which is fine
                        _logger.LogDebug("Scheduled task removal completed with exit code: {ExitCode}", process.ExitCode);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove elevated startup task");
            }
        }
    }
}
