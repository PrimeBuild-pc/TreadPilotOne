using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string statusMessage = string.Empty;
    }
}