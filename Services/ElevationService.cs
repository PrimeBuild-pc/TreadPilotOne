using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing application elevation and administrator privileges
    /// </summary>
    public class ElevationService : IElevationService
    {
        private readonly ILogger<ElevationService> _logger;
        private readonly ISecurityService _securityService;

        public ElevationService(ILogger<ElevationService> logger, ISecurityService securityService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }

        public bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                
                _logger.LogDebug("Administrator privilege check: {IsAdmin}", isAdmin);
                return isAdmin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check administrator privileges");
                return false;
            }
        }

        public async Task<bool> RequestElevationIfNeeded()
        {
            if (IsRunningAsAdministrator())
            {
                _logger.LogDebug("Application is already running with administrator privileges");
                return true;
            }

            _logger.LogInformation("Requesting elevation to administrator privileges");
            
            // Show elevation prompt to user
            var result = System.Windows.MessageBox.Show(
                "ThreadPilot requires administrator privileges to manage process affinity and power plans.\n\n" +
                "Would you like to restart the application with administrator privileges?",
                "Administrator Privileges Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                _logger.LogInformation("User declined elevation request");
                return false;
            }

            return await RestartWithElevation();
        }

        public async Task<bool> RestartWithElevation(string[] arguments = null)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var executablePath = currentProcess.MainModule?.FileName;
                
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Could not determine executable path for elevation");
                    return false;
                }

                // Combine current arguments with any additional arguments
                var currentArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
                var allArgs = arguments != null ? currentArgs.Concat(arguments).ToArray() : currentArgs;
                var argumentString = string.Join(" ", allArgs.Select(arg => $"\"{arg}\""));

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = argumentString,
                    UseShellExecute = true,
                    Verb = "runas", // This triggers UAC elevation
                    WorkingDirectory = Environment.CurrentDirectory
                };

                _logger.LogInformation("Starting elevated process: {FileName} {Arguments}", executablePath, argumentString);
                
                var elevatedProcess = Process.Start(startInfo);
                if (elevatedProcess != null)
                {
                    _logger.LogInformation("Elevated process started successfully. Shutting down current instance.");
                    
                    // Audit the elevation request
                    await _securityService.AuditElevatedAction("ApplicationRestart", "Self", true);
                    
                    // Shutdown current instance
                    await Task.Delay(1000); // Give the new process time to start
                    System.Windows.Application.Current.Shutdown();
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to start elevated process");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart with elevation");
                await _securityService.AuditElevatedAction("ApplicationRestart", "Self", false);
                
                // Show user-friendly error message
                System.Windows.MessageBox.Show(
                    "Failed to restart with administrator privileges. Please manually run ThreadPilot as administrator.",
                    "Elevation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                
                return false;
            }
        }

        public bool ValidateElevationForOperation(string operation)
        {
            var isElevated = IsRunningAsAdministrator();
            var isValidOperation = _securityService.ValidateElevatedOperation(operation);
            
            var canPerform = isElevated && isValidOperation;
            
            _logger.LogDebug("Elevation validation for operation '{Operation}': Elevated={IsElevated}, Valid={IsValid}, CanPerform={CanPerform}",
                operation, isElevated, isValidOperation, canPerform);
            
            return canPerform;
        }

        public string GetElevationStatus()
        {
            return IsRunningAsAdministrator() 
                ? "Running with Administrator privileges" 
                : "Running with limited privileges";
        }
    }
}
