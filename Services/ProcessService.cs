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
                    .Select(p => new ProcessModel
                    {
                        Process = p,
                        ProcessId = p.Id,
                        Name = p.ProcessName,
                        MemoryUsage = p.WorkingSet64,
                        Priority = p.PriorityClass,
                        ProcessorAffinity = (long)p.ProcessorAffinity
                    })
                    .OrderBy(p => p.Name);

                return new ObservableCollection<ProcessModel>(processes);
            });
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
                    process.MemoryUsage = p.WorkingSet64;
                    process.Priority = p.PriorityClass;
                    process.ProcessorAffinity = (long)p.ProcessorAffinity;
                }
                catch (Exception)
                {
                    // Process may have ended
                }
            });
        }
    }
}