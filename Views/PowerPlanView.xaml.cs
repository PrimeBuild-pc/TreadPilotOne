using System.Windows.Controls;
using ThreadPilot.Helpers;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    public partial class PowerPlanView : System.Windows.Controls.UserControl
    {
        public PowerPlanView()
        {
            InitializeComponent();
            DataContext = ServiceProviderExtensions.GetService<PowerPlanViewModel>();
        }
    }
}