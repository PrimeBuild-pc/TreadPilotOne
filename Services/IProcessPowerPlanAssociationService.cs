using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for managing process-power plan associations
    /// </summary>
    public interface IProcessPowerPlanAssociationService
    {
        /// <summary>
        /// Event fired when configuration changes
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        ProcessMonitorConfiguration Configuration { get; }

        /// <summary>
        /// Loads configuration from persistent storage
        /// </summary>
        Task<bool> LoadConfigurationAsync();

        /// <summary>
        /// Saves configuration to persistent storage
        /// </summary>
        Task<bool> SaveConfigurationAsync();

        /// <summary>
        /// Gets all associations
        /// </summary>
        Task<IEnumerable<ProcessPowerPlanAssociation>> GetAssociationsAsync();

        /// <summary>
        /// Gets enabled associations only
        /// </summary>
        Task<IEnumerable<ProcessPowerPlanAssociation>> GetEnabledAssociationsAsync();

        /// <summary>
        /// Adds a new association
        /// </summary>
        Task<bool> AddAssociationAsync(ProcessPowerPlanAssociation association);

        /// <summary>
        /// Updates an existing association
        /// </summary>
        Task<bool> UpdateAssociationAsync(ProcessPowerPlanAssociation association);

        /// <summary>
        /// Removes an association
        /// </summary>
        Task<bool> RemoveAssociationAsync(string associationId);

        /// <summary>
        /// Finds the best matching association for a process
        /// </summary>
        Task<ProcessPowerPlanAssociation?> FindMatchingAssociationAsync(ProcessModel process);

        /// <summary>
        /// Finds association by executable name
        /// </summary>
        Task<ProcessPowerPlanAssociation?> FindAssociationByExecutableAsync(string executableName);

        /// <summary>
        /// Sets the default power plan
        /// </summary>
        Task<bool> SetDefaultPowerPlanAsync(string powerPlanGuid, string powerPlanName);

        /// <summary>
        /// Gets the default power plan
        /// </summary>
        Task<(string Guid, string Name)> GetDefaultPowerPlanAsync();

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        Task<IEnumerable<string>> ValidateConfigurationAsync();

        /// <summary>
        /// Resets configuration to defaults
        /// </summary>
        Task ResetConfigurationAsync();

        /// <summary>
        /// Exports configuration to a file
        /// </summary>
        Task<bool> ExportConfigurationAsync(string filePath);

        /// <summary>
        /// Imports configuration from a file
        /// </summary>
        Task<bool> ImportConfigurationAsync(string filePath);
    }

    /// <summary>
    /// Event arguments for configuration changes
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string ChangeType { get; }
        public ProcessPowerPlanAssociation? Association { get; }
        public string? Details { get; }

        public ConfigurationChangedEventArgs(string changeType, ProcessPowerPlanAssociation? association = null, string? details = null)
        {
            ChangeType = changeType;
            Association = association;
            Details = details;
        }
    }
}
