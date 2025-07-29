using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public interface IProcessService
    {
        Task<ObservableCollection<ProcessModel>> GetProcessesAsync();
        Task SetProcessorAffinity(ProcessModel process, long affinityMask);
        Task SetProcessPriority(ProcessModel process, ProcessPriorityClass priority);
        Task<bool> SaveProcessProfile(string profileName, ProcessModel process);
        Task<bool> LoadProcessProfile(string profileName, ProcessModel process);
        Task RefreshProcessInfo(ProcessModel process);

        /// <summary>
        /// Gets a process by its ID
        /// </summary>
        Task<ProcessModel?> GetProcessByIdAsync(int processId);

        /// <summary>
        /// Gets processes by executable name
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetProcessesByNameAsync(string executableName);

        /// <summary>
        /// Checks if a process with the given name is currently running
        /// </summary>
        Task<bool> IsProcessRunningAsync(string executableName);

        /// <summary>
        /// Gets all running processes with their executable paths
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetProcessesWithPathsAsync();

        /// <summary>
        /// Gets only active applications with visible windows (user-facing applications)
        /// </summary>
        Task<ObservableCollection<ProcessModel>> GetActiveApplicationsAsync();

        /// <summary>
        /// Creates a ProcessModel from a System.Diagnostics.Process
        /// </summary>
        ProcessModel CreateProcessModel(Process process);

        /// <summary>
        /// Checks if a specific process is still running
        /// </summary>
        Task<bool> IsProcessStillRunning(ProcessModel process);
    }
}