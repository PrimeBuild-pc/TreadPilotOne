using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing process-power plan associations with persistence
    /// </summary>
    public class ProcessPowerPlanAssociationService : IProcessPowerPlanAssociationService
    {
        private readonly string _configurationDirectory;
        private readonly string _configurationFilePath;
        private readonly object _lockObject = new();
        
        private ProcessMonitorConfiguration _configuration;

        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ProcessMonitorConfiguration Configuration => _configuration;

        public ProcessPowerPlanAssociationService()
        {
            _configurationDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration");
            _configurationFilePath = Path.Combine(_configurationDirectory, "ProcessPowerPlanAssociations.json");
            _configuration = new ProcessMonitorConfiguration();

            EnsureConfigurationDirectoryExists();
        }

        public async Task<bool> LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configurationFilePath))
                {
                    // Create default configuration
                    _configuration = new ProcessMonitorConfiguration();
                    await SaveConfigurationAsync();
                    return true;
                }

                var json = await File.ReadAllTextAsync(_configurationFilePath);
                var loadedConfig = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(json);
                
                if (loadedConfig != null)
                {
                    lock (_lockObject)
                    {
                        _configuration = loadedConfig;
                    }
                    
                    OnConfigurationChanged("Loaded", null, "Configuration loaded from file");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("LoadError", null, $"Failed to load configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SaveConfigurationAsync()
        {
            try
            {
                ProcessMonitorConfiguration configToSave;
                lock (_lockObject)
                {
                    configToSave = _configuration;
                    configToSave.LastSavedDate = DateTime.Now;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(configToSave, options);
                await File.WriteAllTextAsync(_configurationFilePath, json);
                
                OnConfigurationChanged("Saved", null, "Configuration saved to file");
                return true;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("SaveError", null, $"Failed to save configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<IEnumerable<ProcessPowerPlanAssociation>> GetAssociationsAsync()
        {
            await Task.CompletedTask;
            lock (_lockObject)
            {
                return _configuration.Associations.ToList();
            }
        }

        public async Task<IEnumerable<ProcessPowerPlanAssociation>> GetEnabledAssociationsAsync()
        {
            await Task.CompletedTask;
            lock (_lockObject)
            {
                return _configuration.GetEnabledAssociations().ToList();
            }
        }

        public async Task<bool> AddAssociationAsync(ProcessPowerPlanAssociation association)
        {
            try
            {
                if (association == null) return false;

                lock (_lockObject)
                {
                    // Check for duplicates
                    var existing = _configuration.Associations
                        .FirstOrDefault(a => a.ExecutableName.Equals(association.ExecutableName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existing != null)
                    {
                        return false; // Duplicate found
                    }

                    _configuration.AddOrUpdateAssociation(association);
                }

                await SaveConfigurationAsync();
                OnConfigurationChanged("Added", association, $"Association added for {association.ExecutableName}");
                return true;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("AddError", association, $"Failed to add association: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateAssociationAsync(ProcessPowerPlanAssociation association)
        {
            try
            {
                if (association == null) return false;

                lock (_lockObject)
                {
                    _configuration.AddOrUpdateAssociation(association);
                }

                await SaveConfigurationAsync();
                OnConfigurationChanged("Updated", association, $"Association updated for {association.ExecutableName}");
                return true;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("UpdateError", association, $"Failed to update association: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveAssociationAsync(string associationId)
        {
            try
            {
                ProcessPowerPlanAssociation? removedAssociation = null;
                bool removed;

                lock (_lockObject)
                {
                    removedAssociation = _configuration.Associations.FirstOrDefault(a => a.Id == associationId);
                    removed = _configuration.RemoveAssociation(associationId);
                }

                if (removed)
                {
                    await SaveConfigurationAsync();
                    OnConfigurationChanged("Removed", removedAssociation, 
                        $"Association removed for {removedAssociation?.ExecutableName}");
                }

                return removed;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("RemoveError", null, $"Failed to remove association: {ex.Message}");
                return false;
            }
        }

        public async Task<ProcessPowerPlanAssociation?> FindMatchingAssociationAsync(ProcessModel process)
        {
            await Task.CompletedTask;
            lock (_lockObject)
            {
                return _configuration.FindMatchingAssociation(process);
            }
        }

        public async Task<ProcessPowerPlanAssociation?> FindAssociationByExecutableAsync(string executableName)
        {
            await Task.CompletedTask;
            lock (_lockObject)
            {
                return _configuration.FindAssociationByExecutable(executableName);
            }
        }

        public async Task<bool> SetDefaultPowerPlanAsync(string powerPlanGuid, string powerPlanName)
        {
            try
            {
                lock (_lockObject)
                {
                    _configuration.DefaultPowerPlanGuid = powerPlanGuid;
                    _configuration.DefaultPowerPlanName = powerPlanName;
                }

                await SaveConfigurationAsync();
                OnConfigurationChanged("DefaultPowerPlanChanged", null, $"Default power plan set to {powerPlanName}");
                return true;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("DefaultPowerPlanError", null, $"Failed to set default power plan: {ex.Message}");
                return false;
            }
        }

        public async Task<(string Guid, string Name)> GetDefaultPowerPlanAsync()
        {
            await Task.CompletedTask;
            lock (_lockObject)
            {
                return (_configuration.DefaultPowerPlanGuid, _configuration.DefaultPowerPlanName);
            }
        }

        public async Task<IEnumerable<string>> ValidateConfigurationAsync()
        {
            await Task.CompletedTask;
            lock (_lockObject)
            {
                return _configuration.Validate();
            }
        }

        public async Task ResetConfigurationAsync()
        {
            lock (_lockObject)
            {
                _configuration = new ProcessMonitorConfiguration();
            }

            await SaveConfigurationAsync();
            OnConfigurationChanged("Reset", null, "Configuration reset to defaults");
        }

        public async Task<bool> ExportConfigurationAsync(string filePath)
        {
            try
            {
                ProcessMonitorConfiguration configToExport;
                lock (_lockObject)
                {
                    configToExport = _configuration;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(configToExport, options);
                await File.WriteAllTextAsync(filePath, json);
                
                OnConfigurationChanged("Exported", null, $"Configuration exported to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("ExportError", null, $"Failed to export configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ImportConfigurationAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                var json = await File.ReadAllTextAsync(filePath);
                var importedConfig = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(json);
                
                if (importedConfig != null)
                {
                    lock (_lockObject)
                    {
                        _configuration = importedConfig;
                    }
                    
                    await SaveConfigurationAsync();
                    OnConfigurationChanged("Imported", null, $"Configuration imported from {filePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                OnConfigurationChanged("ImportError", null, $"Failed to import configuration: {ex.Message}");
                return false;
            }
        }

        private void EnsureConfigurationDirectoryExists()
        {
            if (!Directory.Exists(_configurationDirectory))
            {
                Directory.CreateDirectory(_configurationDirectory);
            }
        }

        private void OnConfigurationChanged(string changeType, ProcessPowerPlanAssociation? association = null, string? details = null)
        {
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(changeType, association, details));
        }
    }
}
