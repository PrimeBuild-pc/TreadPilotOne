using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Event arguments for profile application events
    /// </summary>
    public class ProfileApplicationEventArgs : EventArgs
    {
        public ConditionalProcessProfile Profile { get; set; } = new();
        public ProcessModel Process { get; set; } = new();
        public SystemState SystemState { get; set; } = new();
        public bool WasApplied { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event arguments for profile conflict events
    /// </summary>
    public class ProfileConflictEventArgs : EventArgs
    {
        public List<ConditionalProcessProfile> ConflictingProfiles { get; set; } = new();
        public ProcessModel Process { get; set; } = new();
        public ConditionalProcessProfile SelectedProfile { get; set; } = new();
        public string Resolution { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for managing conditional process profiles
    /// </summary>
    public interface IConditionalProfileService
    {
        /// <summary>
        /// Initialize the conditional profile service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Add a conditional profile
        /// </summary>
        Task AddProfileAsync(ConditionalProcessProfile profile);

        /// <summary>
        /// Remove a conditional profile
        /// </summary>
        Task RemoveProfileAsync(string profileId);

        /// <summary>
        /// Update an existing conditional profile
        /// </summary>
        Task UpdateProfileAsync(ConditionalProcessProfile profile);

        /// <summary>
        /// Get all conditional profiles
        /// </summary>
        Task<List<ConditionalProcessProfile>> GetAllProfilesAsync();

        /// <summary>
        /// Get profiles for a specific process
        /// </summary>
        Task<List<ConditionalProcessProfile>> GetProfilesForProcessAsync(string processName);

        /// <summary>
        /// Evaluate all profiles for a process and return applicable ones
        /// </summary>
        Task<List<ConditionalProcessProfile>> EvaluateProfilesAsync(ProcessModel process);

        /// <summary>
        /// Apply the best matching profile for a process
        /// </summary>
        Task<bool> ApplyBestProfileAsync(ProcessModel process);

        /// <summary>
        /// Get current system state for condition evaluation
        /// </summary>
        Task<SystemState> GetSystemStateAsync();

        /// <summary>
        /// Start automatic profile monitoring and application
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stop automatic profile monitoring
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Check if monitoring is active
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Resolve conflicts when multiple profiles match
        /// </summary>
        ConditionalProcessProfile ResolveProfileConflict(List<ConditionalProcessProfile> conflictingProfiles, ProcessModel process);

        /// <summary>
        /// Create a default conditional profile template
        /// </summary>
        ConditionalProcessProfile CreateDefaultProfile(string processName);

        /// <summary>
        /// Validate a conditional profile
        /// </summary>
        Task<(bool IsValid, List<string> Errors)> ValidateProfileAsync(ConditionalProcessProfile profile);

        /// <summary>
        /// Export profiles to JSON
        /// </summary>
        Task<string> ExportProfilesToJsonAsync();

        /// <summary>
        /// Import profiles from JSON
        /// </summary>
        Task<int> ImportProfilesFromJsonAsync(string json);

        /// <summary>
        /// Event raised when a profile is automatically applied
        /// </summary>
        event EventHandler<ProfileApplicationEventArgs>? ProfileApplied;

        /// <summary>
        /// Event raised when profile conflicts are resolved
        /// </summary>
        event EventHandler<ProfileConflictEventArgs>? ProfileConflictResolved;

        /// <summary>
        /// Event raised when system state changes significantly
        /// </summary>
        event EventHandler<SystemState>? SystemStateChanged;
    }
}
