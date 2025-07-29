using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for Game Boost mode functionality
    /// </summary>
    public interface IGameBoostService
    {
        /// <summary>
        /// Event fired when Game Boost mode is activated
        /// </summary>
        event EventHandler<GameBoostActivatedEventArgs>? GameBoostActivated;

        /// <summary>
        /// Event fired when Game Boost mode is deactivated
        /// </summary>
        event EventHandler<GameBoostDeactivatedEventArgs>? GameBoostDeactivated;

        /// <summary>
        /// Event fired when a game is detected
        /// </summary>
        event EventHandler<GameDetectedEventArgs>? GameDetected;

        /// <summary>
        /// Gets whether Game Boost mode is currently active
        /// </summary>
        bool IsGameBoostActive { get; }

        /// <summary>
        /// Gets the currently detected game process
        /// </summary>
        ProcessModel? CurrentGameProcess { get; }

        /// <summary>
        /// Gets the list of known game executables
        /// </summary>
        IReadOnlyList<string> KnownGameExecutables { get; }

        /// <summary>
        /// Gets the list of known game executables as a collection
        /// </summary>
        IReadOnlyList<string> GetKnownGameExecutables();

        /// <summary>
        /// Enables Game Boost mode
        /// </summary>
        Task<bool> EnableGameBoostAsync();

        /// <summary>
        /// Disables Game Boost mode
        /// </summary>
        Task<bool> DisableGameBoostAsync();

        /// <summary>
        /// Manually activates Game Boost for a specific process
        /// </summary>
        /// <param name="process">The process to boost</param>
        Task<bool> ActivateGameBoostAsync(ProcessModel process);

        /// <summary>
        /// Manually deactivates Game Boost
        /// </summary>
        Task<bool> DeactivateGameBoostAsync();

        /// <summary>
        /// Adds a game executable to the known games list
        /// </summary>
        /// <param name="executableName">Name of the executable (e.g., "game.exe")</param>
        Task<bool> AddKnownGameAsync(string executableName);

        /// <summary>
        /// Removes a game executable from the known games list
        /// </summary>
        /// <param name="executableName">Name of the executable to remove</param>
        Task<bool> RemoveKnownGameAsync(string executableName);

        /// <summary>
        /// Checks if a process is considered a game
        /// </summary>
        /// <param name="process">The process to check</param>
        bool IsGameProcess(ProcessModel process);
    }

    /// <summary>
    /// Event arguments for Game Boost activation
    /// </summary>
    public class GameBoostActivatedEventArgs : EventArgs
    {
        public ProcessModel GameProcess { get; }
        public string PowerPlanId { get; }
        public DateTime ActivatedAt { get; }

        public GameBoostActivatedEventArgs(ProcessModel gameProcess, string powerPlanId)
        {
            GameProcess = gameProcess;
            PowerPlanId = powerPlanId;
            ActivatedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for Game Boost deactivation
    /// </summary>
    public class GameBoostDeactivatedEventArgs : EventArgs
    {
        public ProcessModel? GameProcess { get; }
        public string? RestoredPowerPlanId { get; }
        public DateTime DeactivatedAt { get; }
        public TimeSpan Duration { get; }

        public GameBoostDeactivatedEventArgs(ProcessModel? gameProcess, string? restoredPowerPlanId, TimeSpan duration)
        {
            GameProcess = gameProcess;
            RestoredPowerPlanId = restoredPowerPlanId;
            DeactivatedAt = DateTime.Now;
            Duration = duration;
        }
    }

    /// <summary>
    /// Event arguments for game detection
    /// </summary>
    public class GameDetectedEventArgs : EventArgs
    {
        public ProcessModel GameProcess { get; }
        public bool IsKnownGame { get; }
        public DateTime DetectedAt { get; }

        public GameDetectedEventArgs(ProcessModel gameProcess, bool isKnownGame)
        {
            GameProcess = gameProcess;
            IsKnownGame = isKnownGame;
            DetectedAt = DateTime.Now;
        }
    }
}
