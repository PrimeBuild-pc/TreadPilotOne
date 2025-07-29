using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing application elevation and administrator privileges
    /// </summary>
    public interface IElevationService
    {
        /// <summary>
        /// Checks if the current process is running with administrator privileges
        /// </summary>
        /// <returns>True if running as administrator, false otherwise</returns>
        bool IsRunningAsAdministrator();

        /// <summary>
        /// Requests elevation if the current process is not running as administrator
        /// </summary>
        /// <returns>True if already elevated or elevation was successful, false if elevation failed or was cancelled</returns>
        Task<bool> RequestElevationIfNeeded();

        /// <summary>
        /// Restarts the application with administrator privileges
        /// </summary>
        /// <param name="arguments">Command line arguments to pass to the elevated process</param>
        /// <returns>True if restart was initiated successfully, false otherwise</returns>
        Task<bool> RestartWithElevation(string[] arguments = null);

        /// <summary>
        /// Validates that the current process has the necessary privileges for the specified operation
        /// </summary>
        /// <param name="operation">The operation that requires validation</param>
        /// <returns>True if the operation can be performed, false otherwise</returns>
        bool ValidateElevationForOperation(string operation);

        /// <summary>
        /// Gets the current elevation status as a user-friendly string
        /// </summary>
        /// <returns>Elevation status description</returns>
        string GetElevationStatus();
    }
}
