using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for security validation and auditing of elevated operations
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly ILogger<SecurityService> _logger;
        private readonly IEnhancedLoggingService _enhancedLogger;

        // List of operations that are allowed to be performed with elevated privileges
        private static readonly string[] AllowedElevatedOperations = new[]
        {
            "SetProcessAffinity",
            "SetProcessPriority",
            "ChangePowerPlan",
            "ImportPowerPlan",
            "CreatePowerPlan",
            "DeletePowerPlan",
            "ModifyPowerPlanSettings",
            "ApplicationRestart",
            "CreateScheduledTask",
            "DeleteScheduledTask",
            "ModifyRegistryAutostart"
        };

        // List of critical system processes that should not be modified
        private static readonly string[] ProtectedProcesses = new[]
        {
            "System",
            "csrss",
            "winlogon",
            "services",
            "lsass",
            "svchost",
            "dwm",
            "explorer",
            "wininit",
            "smss"
        };

        public SecurityService(ILogger<SecurityService> logger, IEnhancedLoggingService enhancedLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _enhancedLogger = enhancedLogger ?? throw new ArgumentNullException(nameof(enhancedLogger));
        }

        public bool ValidateElevatedOperation(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                _logger.LogWarning("Attempted to validate null or empty operation");
                return false;
            }

            var isAllowed = AllowedElevatedOperations.Contains(operation, StringComparer.OrdinalIgnoreCase);
            
            if (!isAllowed)
            {
                _logger.LogWarning("Attempted to perform unauthorized elevated operation: {Operation}", operation);
            }
            else
            {
                _logger.LogDebug("Validated elevated operation: {Operation}", operation);
            }

            return isAllowed;
        }

        public async Task AuditElevatedAction(string action, string target, bool success)
        {
            var logLevel = success ? LogLevel.Information : LogLevel.Warning;
            var message = $"Elevated action performed: {action} on {target} - Success: {success}";
            
            _logger.Log(logLevel, message);
            
            // Use enhanced logging for structured audit trail
            await _enhancedLogger.LogSystemEventAsync(
                success ? "ElevatedActionSuccess" : "ElevatedActionFailure",
                $"Security audit: {action} on {target} - Success: {success}",
                logLevel
            );
        }

        public bool ValidateProcessOperation(string processName, string operation)
        {
            if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(operation))
            {
                _logger.LogWarning("Attempted to validate process operation with null or empty parameters");
                return false;
            }

            // Check if the process is in the protected list
            var isProtected = ProtectedProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
            
            if (isProtected)
            {
                _logger.LogWarning("Attempted to perform operation '{Operation}' on protected process '{ProcessName}'", 
                    operation, processName);
                return false;
            }

            // Validate the operation itself
            var isValidOperation = operation switch
            {
                "SetProcessAffinity" => true,
                "SetProcessPriority" => true,
                "TerminateProcess" => false, // We don't allow process termination
                _ => false
            };

            if (!isValidOperation)
            {
                _logger.LogWarning("Attempted to perform invalid process operation '{Operation}' on '{ProcessName}'", 
                    operation, processName);
            }

            return isValidOperation;
        }

        public bool ValidatePowerPlanOperation(string powerPlanId, string operation)
        {
            if (string.IsNullOrWhiteSpace(powerPlanId) || string.IsNullOrWhiteSpace(operation))
            {
                _logger.LogWarning("Attempted to validate power plan operation with null or empty parameters");
                return false;
            }

            // Validate the operation
            var isValidOperation = operation switch
            {
                "ChangePowerPlan" => true,
                "ImportPowerPlan" => true,
                "CreatePowerPlan" => true,
                "DeletePowerPlan" => !IsSystemPowerPlan(powerPlanId), // Don't allow deletion of system power plans
                "ModifyPowerPlanSettings" => true,
                _ => false
            };

            if (!isValidOperation)
            {
                _logger.LogWarning("Attempted to perform invalid power plan operation '{Operation}' on '{PowerPlanId}'", 
                    operation, powerPlanId);
            }

            return isValidOperation;
        }

        public string[] GetAllowedElevatedOperations()
        {
            return AllowedElevatedOperations.ToArray();
        }

        private static bool IsSystemPowerPlan(string powerPlanId)
        {
            // Common system power plan GUIDs
            var systemPowerPlans = new[]
            {
                "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High performance
                "a1841308-3541-4fab-bc81-f71556f20b4a"  // Power saver
            };

            return systemPowerPlans.Contains(powerPlanId, StringComparer.OrdinalIgnoreCase);
        }
    }
}
