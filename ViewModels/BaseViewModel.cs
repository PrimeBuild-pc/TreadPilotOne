using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        protected void SetStatus(string message, bool isBusyState = true)
        {
            StatusMessage = message;
            IsBusy = isBusyState;
        }

        protected void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsBusy = false;
        }
    }
}