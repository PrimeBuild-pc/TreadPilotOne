using System;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing application settings
    /// </summary>
    public interface IApplicationSettingsService
    {
        /// <summary>
        /// Event fired when settings are changed
        /// </summary>
        event EventHandler<ApplicationSettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// Gets the current application settings
        /// </summary>
        ApplicationSettingsModel Settings { get; }

        /// <summary>
        /// Loads settings from storage
        /// </summary>
        Task LoadSettingsAsync();

        /// <summary>
        /// Saves current settings to storage
        /// </summary>
        Task SaveSettingsAsync();

        /// <summary>
        /// Updates settings and saves them
        /// </summary>
        Task UpdateSettingsAsync(ApplicationSettingsModel newSettings);

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        Task ResetToDefaultsAsync();

        /// <summary>
        /// Gets the settings file path
        /// </summary>
        string GetSettingsFilePath();

        /// <summary>
        /// Validates settings and fixes any invalid values
        /// </summary>
        void ValidateAndFixSettings();

        /// <summary>
        /// Exports settings to a file
        /// </summary>
        Task ExportSettingsAsync(string filePath);

        /// <summary>
        /// Imports settings from a file
        /// </summary>
        Task ImportSettingsAsync(string filePath);
    }

    /// <summary>
    /// Event args for settings changed event
    /// </summary>
    public class ApplicationSettingsChangedEventArgs : EventArgs
    {
        public ApplicationSettingsModel OldSettings { get; }
        public ApplicationSettingsModel NewSettings { get; }
        public string[] ChangedProperties { get; }

        public ApplicationSettingsChangedEventArgs(
            ApplicationSettingsModel oldSettings, 
            ApplicationSettingsModel newSettings, 
            string[] changedProperties)
        {
            OldSettings = oldSettings;
            NewSettings = newSettings;
            ChangedProperties = changedProperties;
        }
    }
}
