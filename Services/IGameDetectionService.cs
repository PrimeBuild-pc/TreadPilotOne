using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for detecting running games and applying optimal performance profiles
    /// </summary>
    public interface IGameDetectionService
    {
        /// <summary>
        /// Detect if a process is a known game
        /// </summary>
        Task<GameProfile?> DetectGameAsync(ProcessModel process);

        /// <summary>
        /// Get all currently running games
        /// </summary>
        Task<List<GameProfile>> GetRunningGamesAsync();

        /// <summary>
        /// Apply optimal settings for a detected game
        /// </summary>
        Task<bool> ApplyGameOptimizationsAsync(ProcessModel process, GameProfile gameProfile);

        /// <summary>
        /// Check if a process is running through Steam
        /// </summary>
        Task<bool> IsSteamGameAsync(ProcessModel process);

        /// <summary>
        /// Check if a process is running through Epic Games Launcher
        /// </summary>
        Task<bool> IsEpicGameAsync(ProcessModel process);

        /// <summary>
        /// Get game profile for a Steam game
        /// </summary>
        Task<GameProfile?> GetSteamGameProfileAsync(ProcessModel process);

        /// <summary>
        /// Get game profile for an Epic Games game
        /// </summary>
        Task<GameProfile?> GetEpicGameProfileAsync(ProcessModel process);

        /// <summary>
        /// Add or update a custom game profile
        /// </summary>
        Task AddCustomGameProfileAsync(string processName, GameProfile profile);

        /// <summary>
        /// Remove a custom game profile
        /// </summary>
        Task RemoveCustomGameProfileAsync(string processName);

        /// <summary>
        /// Get all available game profiles
        /// </summary>
        Task<Dictionary<string, GameProfile>> GetAllGameProfilesAsync();

        /// <summary>
        /// Event raised when a new game is detected
        /// </summary>
        event EventHandler<GameProfileDetectedEventArgs>? GameDetected;

        /// <summary>
        /// Event raised when a game stops running
        /// </summary>
        event EventHandler<GameStoppedEventArgs>? GameStopped;
    }

    /// <summary>
    /// Represents a game profile with optimal settings
    /// </summary>
    public class GameProfile
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string OptimalCores { get; set; } = "All"; // "All", "Physical", "P-Cores", "E-Cores", "Custom"
        public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.High;
        public string? PowerPlan { get; set; }
        public bool DisableFullscreenOptimizations { get; set; } = true;
        public bool HighDpiAware { get; set; } = true;
        public string? CustomAffinityMask { get; set; }
        public Dictionary<string, object> CustomSettings { get; set; } = new();
        public DateTime LastDetected { get; set; }
        public int DetectionCount { get; set; }
        public string? SteamAppId { get; set; }
        public string? EpicAppId { get; set; }
        public GameCategory Category { get; set; } = GameCategory.Unknown;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Game categories for better organization
    /// </summary>
    public enum GameCategory
    {
        Unknown,
        FPS,
        MOBA,
        RTS,
        RPG,
        Racing,
        Sports,
        Simulation,
        Strategy,
        Action,
        Adventure,
        Indie
    }

    /// <summary>
    /// Event args for game profile detection
    /// </summary>
    public class GameProfileDetectedEventArgs : EventArgs
    {
        public ProcessModel Process { get; }
        public GameProfile GameProfile { get; }
        public DateTime DetectedAt { get; }

        public GameProfileDetectedEventArgs(ProcessModel process, GameProfile gameProfile)
        {
            Process = process;
            GameProfile = gameProfile;
            DetectedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event args for game stopped
    /// </summary>
    public class GameStoppedEventArgs : EventArgs
    {
        public string ProcessName { get; }
        public GameProfile GameProfile { get; }
        public DateTime StoppedAt { get; }
        public TimeSpan PlayDuration { get; }

        public GameStoppedEventArgs(string processName, GameProfile gameProfile, TimeSpan playDuration)
        {
            ProcessName = processName;
            GameProfile = gameProfile;
            StoppedAt = DateTime.UtcNow;
            PlayDuration = playDuration;
        }
    }
}
