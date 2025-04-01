using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Register Services
            services.AddSingleton<IProcessService, ProcessService>();
            services.AddSingleton<IPowerPlanService, PowerPlanService>();

            // Register ViewModels
            services.AddTransient<ProcessViewModel>();
            services.AddTransient<PowerPlanViewModel>();

            // Register Views
            services.AddTransient<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}

