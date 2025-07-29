using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public class ProcessService : IProcessService
    {
        private string ProfilesDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");

        public ProcessService()
        {
            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
            }
        }

        public async Task<ObservableCollection<ProcessModel>> GetProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses()
                    .Select(CreateProcessModel)
                    .Where(p => p != null)
                    .OrderBy(p => p.Name);

                return new ObservableCollection<ProcessModel>(processes);
            });
        }

        public ProcessModel CreateProcessModel(Process process)
        {
            var model = new ProcessModel();
            try
            {
                model.Process = process;
                model.ProcessId = process.Id;
                model.Name = process.ProcessName;
                model.MemoryUsage = process.WorkingSet64;
                model.Priority = process.PriorityClass;
                model.ProcessorAffinity = (long)process.ProcessorAffinity;

                // Capture window information
                model.MainWindowHandle = process.MainWindowHandle;
                model.MainWindowTitle = process.MainWindowTitle ?? string.Empty;
                model.HasVisibleWindow = model.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(model.MainWindowTitle);

                // Try to get executable path
                try
                {
                    model.ExecutablePath = process.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    model.ExecutablePath = string.Empty;
                }
            }
            catch
            {
                // Process may have terminated or access denied
                // Return a minimal model
                model.ProcessId = process.Id;
                model.Name = process.ProcessName;
            }

            return model;
        }

        public async Task SetProcessorAffinity(ProcessModel process, long affinityMask)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (process.Process != null)
                    {
                        process.Process.ProcessorAffinity = new IntPtr(affinityMask);
                        process.ProcessorAffinity = affinityMask;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to set processor affinity: {ex.Message}");
                }
            });
        }

        public async Task SetProcessPriority(ProcessModel process, ProcessPriorityClass priority)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (process.Process != null)
                    {
                        process.Process.PriorityClass = priority;
                        process.Priority = priority;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to set process priority: {ex.Message}");
                }
            });
        }

        public async Task<bool> SaveProcessProfile(string profileName, ProcessModel process)
        {
            var profile = new
            {
                ProcessName = process.Name,
                Priority = process.Priority,
                ProcessorAffinity = process.ProcessorAffinity
            };

            var filePath = Path.Combine(ProfilesDirectory, $"{profileName}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(profile));
            return true;
        }

        public async Task<bool> LoadProcessProfile(string profileName, ProcessModel process)
        {
            var filePath = Path.Combine(ProfilesDirectory, $"{profileName}.json");
            if (!File.Exists(filePath))
                return false;

            var content = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<dynamic>(content);

            if (profile != null)
            {
                await SetProcessPriority(process, profile.Priority);
                await SetProcessorAffinity(process, profile.ProcessorAffinity);
                return true;
            }

            return false;
        }

        public async Task RefreshProcessInfo(ProcessModel process)
        {
            await Task.Run(() =>
            {
                try
                {
                    var p = Process.GetProcessById(process.ProcessId);

                    // Check if process has exited
                    if (p.HasExited)
                    {
                        throw new InvalidOperationException("Process has exited");
                    }

                    process.MemoryUsage = p.WorkingSet64;
                    process.Priority = p.PriorityClass;
                    process.ProcessorAffinity = (long)p.ProcessorAffinity;

                    // Update window information
                    process.MainWindowHandle = p.MainWindowHandle;
                    process.MainWindowTitle = p.MainWindowTitle ?? string.Empty;
                    process.HasVisibleWindow = process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(process.MainWindowTitle);
                }
                catch (ArgumentException)
                {
                    // Process with the specified ID does not exist
                    throw new InvalidOperationException("Process no longer exists");
                }
                catch (Exception ex) when (ex.Message.Contains("exited") || ex.Message.Contains("terminated"))
                {
                    // Process has terminated
                    throw new InvalidOperationException("Process has terminated");
                }
            });
        }

        public async Task<ProcessModel?> GetProcessByIdAsync(int processId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    return CreateProcessModel(process);
                }
                catch
                {
                    return null;
                }
            });
        }

        public async Task<IEnumerable<ProcessModel>> GetProcessesByNameAsync(string executableName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var processes = Process.GetProcessesByName(executableName)
                        .Select(CreateProcessModel)
                        .Where(p => p != null);

                    return processes;
                }
                catch
                {
                    return Enumerable.Empty<ProcessModel>();
                }
            });
        }

        public async Task<bool> IsProcessRunningAsync(string executableName)
        {
            var processes = await GetProcessesByNameAsync(executableName);
            return processes.Any();
        }

        public async Task<IEnumerable<ProcessModel>> GetProcessesWithPathsAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses()
                    .Select(CreateProcessModel)
                    .Where(p => p != null && !string.IsNullOrEmpty(p.ExecutablePath))
                    .OrderBy(p => p.Name);

                return processes;
            });
        }

        public async Task<ObservableCollection<ProcessModel>> GetActiveApplicationsAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses()
                    .Select(CreateProcessModel)
                    .Where(p => p != null && p.HasVisibleWindow)
                    .OrderBy(p => p.Name);

                return new ObservableCollection<ProcessModel>(processes);
            });
        }

        public async Task<bool> IsProcessStillRunning(ProcessModel process)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var p = Process.GetProcessById(process.ProcessId);
                    return !p.HasExited;
                }
                catch (ArgumentException)
                {
                    // Process with the specified ID does not exist
                    return false;
                }
                catch
                {
                    // Any other exception means process is not accessible/running
                    return false;
                }
            });
        }
    }
}