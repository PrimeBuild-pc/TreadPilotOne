using System.Windows.Controls;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for PerformanceView.xaml
    /// </summary>
    public partial class PerformanceView : System.Windows.Controls.UserControl
    {
        public PerformanceView()
        {
            InitializeComponent();
        }

        public PerformanceView(PerformanceViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
