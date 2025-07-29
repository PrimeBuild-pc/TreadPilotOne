using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;
using System;

namespace ThreadPilot
{
    public partial class App : System.Windows.Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            ServiceCollection services = new ServiceCollection();

            // Use the new centralized service configuration
            services.ConfigureApplicationServices();

            ServiceProvider = services.BuildServiceProvider();

            // Validate service configuration
            ServiceConfiguration.ValidateServiceConfiguration(ServiceProvider);
        }



        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Parse command line arguments
            bool startMinimized = false;
            bool isAutostart = false;
            bool isTestMode = false;

            foreach (var arg in e.Args)
            {
                switch (arg.ToLowerInvariant())
                {
                    case "--test":
                        isTestMode = true;
                        break;
                    case "--start-minimized":
                        startMinimized = true;
                        break;
                    case "--autostart":
                        isAutostart = true;
                        break;
                }
            }

            // Check for test mode
            if (isTestMode)
            {
                // Run in console test mode
                AllocConsole();
                _ = Task.Run(async () =>
                {
                    await TestRunner.RunTests();
                    Dispatcher.Invoke(() => Shutdown());
                });
                return;
            }

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            // Handle startup behavior
            if (startMinimized || isAutostart)
            {
                // Start minimized to tray - let MainWindow handle the minimize to tray logic
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.Show();

                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Application started minimized to tray (autostart: {IsAutostart})", isAutostart);
            }
            else
            {
                // Normal startup - show the window
                mainWindow.Show();
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
    }
}

