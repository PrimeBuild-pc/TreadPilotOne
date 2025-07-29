using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Data
{
    /// <summary>
    /// Implementation of data access service with JSON file repositories
    /// </summary>
    public class DataAccessService : IDataAccessService
    {
        private readonly ILogger<DataAccessService> _logger;
        private readonly string _dataDirectory;

        public IRepository<ProcessPowerPlanAssociation> ProcessAssociations { get; }
        public IRepository<ApplicationSettingsModel> ApplicationSettings { get; }
        public IRepository<ProfileModel> ProcessProfiles { get; }

        public DataAccessService(ILogger<DataAccessService> logger, string? dataDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataDirectory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThreadPilot", "Data");

            // Ensure data directory exists
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }

            // Initialize repositories
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            
            ProcessAssociations = new JsonRepository<ProcessPowerPlanAssociation>(
                Path.Combine(_dataDirectory, "ProcessAssociations.json"),
                loggerFactory.CreateLogger<JsonRepository<ProcessPowerPlanAssociation>>());

            ApplicationSettings = new JsonRepository<ApplicationSettingsModel>(
                Path.Combine(_dataDirectory, "ApplicationSettings.json"),
                loggerFactory.CreateLogger<JsonRepository<ApplicationSettingsModel>>());

            ProcessProfiles = new JsonRepository<ProfileModel>(
                Path.Combine(_dataDirectory, "ProcessProfiles.json"),
                loggerFactory.CreateLogger<JsonRepository<ProfileModel>>());
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing data access service with directory: {DataDirectory}", _dataDirectory);

            try
            {
                // Validate data directory
                if (!Directory.Exists(_dataDirectory))
                {
                    Directory.CreateDirectory(_dataDirectory);
                    _logger.LogInformation("Created data directory: {DataDirectory}", _dataDirectory);
                }

                // Validate data integrity
                var validationResult = await ValidateDataIntegrityAsync();
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Data validation found {IssueCount} issues: {Issues}", 
                        validationResult.Issues.Length, string.Join(", ", validationResult.Issues));
                }

                _logger.LogInformation("Data access service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize data access service");
                throw;
            }
        }

        public async Task BackupDataAsync(string backupDirectory)
        {
            if (string.IsNullOrEmpty(backupDirectory))
                throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));

            try
            {
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(backupDirectory, $"ThreadPilot_Backup_{timestamp}");
                Directory.CreateDirectory(backupPath);

                // Copy all data files
                var dataFiles = Directory.GetFiles(_dataDirectory, "*.json");
                foreach (var file in dataFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(backupPath, fileName);
                    File.Copy(file, destPath, true);
                }

                _logger.LogInformation("Data backed up to: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to backup data to {BackupDirectory}", backupDirectory);
                throw;
            }
        }

        public async Task RestoreDataAsync(string backupDirectory)
        {
            if (string.IsNullOrEmpty(backupDirectory))
                throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));

            if (!Directory.Exists(backupDirectory))
                throw new DirectoryNotFoundException($"Backup directory not found: {backupDirectory}");

            try
            {
                // Copy backup files to data directory
                var backupFiles = Directory.GetFiles(backupDirectory, "*.json");
                foreach (var file in backupFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(_dataDirectory, fileName);
                    File.Copy(file, destPath, true);
                }

                _logger.LogInformation("Data restored from: {BackupDirectory}", backupDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore data from {BackupDirectory}", backupDirectory);
                throw;
            }
        }

        public async Task<DataValidationResult> ValidateDataIntegrityAsync()
        {
            var issues = new List<string>();
            var totalRecords = 0;
            var validRecords = 0;

            try
            {
                // Validate process associations
                var associations = await ProcessAssociations.GetAllAsync();
                totalRecords += associations.Count();
                
                foreach (var association in associations)
                {
                    var validation = association.Validate();
                    if (validation.IsValid)
                    {
                        validRecords++;
                    }
                    else
                    {
                        issues.AddRange(validation.Errors.Select(e => $"ProcessAssociation {association.Id}: {e}"));
                    }
                }

                // Validate application settings
                var settings = await ApplicationSettings.GetAllAsync();
                totalRecords += settings.Count();
                
                foreach (var setting in settings)
                {
                    var validation = setting.Validate();
                    if (validation.IsValid)
                    {
                        validRecords++;
                    }
                    else
                    {
                        issues.AddRange(validation.Errors.Select(e => $"ApplicationSettings {setting.Id}: {e}"));
                    }
                }

                // Validate process profiles
                var profiles = await ProcessProfiles.GetAllAsync();
                totalRecords += profiles.Count();
                
                foreach (var profile in profiles)
                {
                    var validation = profile.Validate();
                    if (validation.IsValid)
                    {
                        validRecords++;
                    }
                    else
                    {
                        issues.AddRange(validation.Errors.Select(e => $"ProcessProfile {profile.Id}: {e}"));
                    }
                }

                var isValid = issues.Count == 0;
                return new DataValidationResult(isValid, totalRecords, validRecords, issues.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate data integrity");
                issues.Add($"Validation failed: {ex.Message}");
                return new DataValidationResult(false, totalRecords, validRecords, issues.ToArray());
            }
        }

        public async Task CleanupDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting data cleanup");

                // Remove invalid associations
                var associations = await ProcessAssociations.GetAllAsync();
                var invalidAssociations = associations.Where(a => !a.Validate().IsValid).ToList();
                
                foreach (var invalid in invalidAssociations)
                {
                    await ProcessAssociations.DeleteAsync(invalid.Id);
                    _logger.LogDebug("Removed invalid association: {AssociationId}", invalid.Id);
                }

                // Remove duplicate profiles
                var profiles = await ProcessProfiles.GetAllAsync();
                var duplicateProfiles = profiles
                    .GroupBy(p => p.Name)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.Skip(1))
                    .ToList();

                foreach (var duplicate in duplicateProfiles)
                {
                    await ProcessProfiles.DeleteAsync(duplicate.Id);
                    _logger.LogDebug("Removed duplicate profile: {ProfileId}", duplicate.Id);
                }

                _logger.LogInformation("Data cleanup completed. Removed {InvalidCount} invalid associations and {DuplicateCount} duplicate profiles",
                    invalidAssociations.Count, duplicateProfiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup data");
                throw;
            }
        }
    }
}
