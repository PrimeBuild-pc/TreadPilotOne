using System;
using System.ComponentModel;
using System.Windows;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            SettingsViewControl.DataContext = _viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Check for unsaved changes
            if (!_viewModel.CanClose())
            {
                var result = System.Windows.MessageBox.Show(
                    "You have unsaved changes. Do you want to save them before closing?",
                    "Unsaved Changes",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Question);

                switch (result)
                {
                    case System.Windows.MessageBoxResult.Yes:
                        // Save and close
                        if (_viewModel.SaveSettingsCommand.CanExecute(null))
                        {
                            _viewModel.SaveSettingsCommand.Execute(null);
                        }
                        break;
                    case System.Windows.MessageBoxResult.No:
                        // Close without saving
                        break;
                    case System.Windows.MessageBoxResult.Cancel:
                        // Cancel closing
                        e.Cancel = true;
                        return;
                }
            }

            base.OnClosing(e);
        }
    }
}
