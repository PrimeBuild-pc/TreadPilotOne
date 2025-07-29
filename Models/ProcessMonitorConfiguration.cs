using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Configuration model for process monitoring and power plan associations
    /// </summary>
    public partial class ProcessMonitorConfiguration : ObservableObject
    {
        [ObservableProperty]
        private string defaultPowerPlanGuid = string.Empty;

        [ObservableProperty]
        private string defaultPowerPlanName = string.Empty;

        [ObservableProperty]
        private bool isEventBasedMonitoringEnabled = true;

        [ObservableProperty]
        private bool isFallbackPollingEnabled = true;

        [ObservableProperty]
        private int pollingIntervalSeconds = 5;

        [ObservableProperty]
        private bool preventDuplicatePowerPlanChanges = true;

        [ObservableProperty]
        private int powerPlanChangeDelayMs = 1000;

        [ObservableProperty]
        private bool enableLogging = true;

        [ObservableProperty]
        private List<ProcessPowerPlanAssociation> associations = new();

        [ObservableProperty]
        private DateTime lastSavedDate = DateTime.Now;

        [ObservableProperty]
        private string configurationVersion = "1.0";

        public ProcessMonitorConfiguration()
        {
            Associations = new List<ProcessPowerPlanAssociation>();
        }

        /// <summary>
        /// Gets all enabled associations sorted by priority (descending)
        /// </summary>
        public IEnumerable<ProcessPowerPlanAssociation> GetEnabledAssociations()
        {
            return Associations
                .Where(a => a.IsEnabled)
                .OrderByDescending(a => a.Priority)
                .ThenBy(a => a.ExecutableName);
        }

        /// <summary>
        /// Finds the best matching association for a process
        /// </summary>
        public ProcessPowerPlanAssociation? FindMatchingAssociation(ProcessModel process)
        {
            return GetEnabledAssociations()
                .FirstOrDefault(a => a.MatchesProcess(process));
        }

        /// <summary>
        /// Finds association by executable name
        /// </summary>
        public ProcessPowerPlanAssociation? FindAssociationByExecutable(string executableName)
        {
            return Associations
                .FirstOrDefault(a => a.MatchesExecutable(executableName));
        }

        /// <summary>
        /// Adds or updates an association
        /// </summary>
        public void AddOrUpdateAssociation(ProcessPowerPlanAssociation association)
        {
            var existing = Associations.FirstOrDefault(a => a.Id == association.Id);
            if (existing != null)
            {
                var index = Associations.IndexOf(existing);
                Associations[index] = association;
            }
            else
            {
                Associations.Add(association);
            }
            LastSavedDate = DateTime.Now;
        }

        /// <summary>
        /// Removes an association
        /// </summary>
        public bool RemoveAssociation(string associationId)
        {
            var association = Associations.FirstOrDefault(a => a.Id == associationId);
            if (association != null)
            {
                Associations.Remove(association);
                LastSavedDate = DateTime.Now;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (PollingIntervalSeconds < 1)
                errors.Add("Polling interval must be at least 1 second");

            if (PowerPlanChangeDelayMs < 0)
                errors.Add("Power plan change delay cannot be negative");

            // Check for duplicate associations
            var duplicates = Associations
                .GroupBy(a => new { a.ExecutableName, a.MatchByPath })
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.ExecutableName);

            foreach (var duplicate in duplicates)
            {
                errors.Add($"Duplicate association found for executable: {duplicate}");
            }

            return errors;
        }
    }
}
