using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public interface IPowerPlanService
    {
        Task<ObservableCollection<PowerPlanModel>> GetPowerPlansAsync();
        Task<ObservableCollection<PowerPlanModel>> GetCustomPowerPlansAsync();
        Task<bool> SetActivePowerPlan(PowerPlanModel powerPlan);
        Task<PowerPlanModel?> GetActivePowerPlan();
        Task<bool> ImportCustomPowerPlan(string filePath);
    }
}