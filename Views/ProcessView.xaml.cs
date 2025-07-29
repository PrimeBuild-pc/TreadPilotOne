using System.Windows.Controls;
using ThreadPilot.Helpers;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    public partial class ProcessView : System.Windows.Controls.UserControl
    {
        public ProcessView()
        {
            InitializeComponent();
            DataContext = ServiceProviderExtensions.GetService<ProcessViewModel>();
        }
    }
}