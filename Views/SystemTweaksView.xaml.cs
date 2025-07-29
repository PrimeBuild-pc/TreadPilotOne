using System.Windows.Controls;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for SystemTweaksView.xaml
    /// </summary>
    public partial class SystemTweaksView : System.Windows.Controls.UserControl
    {
        public SystemTweaksView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SystemTweaksViewModel viewModel)
            {
                await viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }
}
