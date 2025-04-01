using System.Windows;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    public partial class MainWindow : Window
    {
        private readonly ProcessViewModel _processViewModel;
        private readonly PowerPlanViewModel _powerPlanViewModel;

        public MainWindow(ProcessViewModel processViewModel, PowerPlanViewModel powerPlanViewModel)
        {
            InitializeComponent();
            _processViewModel = processViewModel;
            _powerPlanViewModel = powerPlanViewModel;

            LoadViewModels();
        }

        private async void LoadViewModels()
        {
            await _processViewModel.LoadProcesses();
            await _powerPlanViewModel.LoadPowerPlans();
        }
    }
}