using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for managing Windows autostart functionality
    /// </summary>
    public interface IAutostartService
    {
        /// <summary>
        /// Event fired when autostart status changes
        /// </summary>
        event EventHandler<AutostartStatusChangedEventArgs>? AutostartStatusChanged;

        /// <summary>
        /// Gets whether the application is currently set to autostart with Windows
        /// </summary>
        bool IsAutostartEnabled { get; }

        /// <summary>
        /// Gets the current autostart registry entry path
        /// </summary>
        string? AutostartPath { get; }

        /// <summary>
        /// Enables autostart with Windows
        /// </summary>
        /// <param name="startMinimized">Whether to start the application minimized</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> EnableAutostartAsync(bool startMinimized = true);

        /// <summary>
        /// Disables autostart with Windows
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DisableAutostartAsync();

        /// <summary>
        /// Checks if autostart is currently enabled
        /// </summary>
        /// <returns>True if autostart is enabled, false otherwise</returns>
        Task<bool> CheckAutostartStatusAsync();

        /// <summary>
        /// Updates the autostart entry with new parameters
        /// </summary>
        /// <param name="startMinimized">Whether to start minimized</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateAutostartAsync(bool startMinimized = true);

        /// <summary>
        /// Gets the command line arguments for autostart
        /// </summary>
        /// <param name="startMinimized">Whether to include start minimized flag</param>
        /// <returns>Command line arguments string</returns>
        string GetAutostartArguments(bool startMinimized = true);
    }

    /// <summary>
    /// Event arguments for autostart status changes
    /// </summary>
    public class AutostartStatusChangedEventArgs : EventArgs
    {
        public bool IsEnabled { get; }
        public bool StartMinimized { get; }
        public string? RegistryPath { get; }
        public Exception? Error { get; }

        public AutostartStatusChangedEventArgs(bool isEnabled, bool startMinimized = false, string? registryPath = null, Exception? error = null)
        {
            IsEnabled = isEnabled;
            StartMinimized = startMinimized;
            RegistryPath = registryPath;
            Error = error;
        }
    }
}
