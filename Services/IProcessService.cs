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
    }
}