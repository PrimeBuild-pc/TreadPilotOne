using System;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Data
{
    /// <summary>
    /// Service for coordinating data access operations across all repositories
    /// </summary>
    public interface IDataAccessService
    {
        /// <summary>
        /// Get repository for process-power plan associations
        /// </summary>
        IRepository<ProcessPowerPlanAssociation> ProcessAssociations { get; }

        /// <summary>
        /// Get repository for application settings
        /// </summary>
        IRepository<ApplicationSettingsModel> ApplicationSettings { get; }

        /// <summary>
        /// Get repository for process profiles
        /// </summary>
        IRepository<ProfileModel> ProcessProfiles { get; }

        /// <summary>
        /// Initialize all repositories
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Backup all data to specified directory
        /// </summary>
        Task BackupDataAsync(string backupDirectory);

        /// <summary>
        /// Restore data from backup directory
        /// </summary>
        Task RestoreDataAsync(string backupDirectory);

        /// <summary>
        /// Validate data integrity across all repositories
        /// </summary>
        Task<DataValidationResult> ValidateDataIntegrityAsync();

        /// <summary>
        /// Clean up orphaned or invalid data
        /// </summary>
        Task CleanupDataAsync();
    }

    /// <summary>
    /// Result of data validation operation
    /// </summary>
    public class DataValidationResult
    {
        public bool IsValid { get; }
        public string[] Issues { get; }
        public int TotalRecords { get; }
        public int ValidRecords { get; }

        public DataValidationResult(bool isValid, int totalRecords, int validRecords, params string[] issues)
        {
            IsValid = isValid;
            TotalRecords = totalRecords;
            ValidRecords = validRecords;
            Issues = issues ?? Array.Empty<string>();
        }
    }
}
