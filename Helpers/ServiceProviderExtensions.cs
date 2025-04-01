using System;
using Microsoft.Extensions.DependencyInjection;

namespace ThreadPilot.Helpers
{
    public static class ServiceProviderExtensions
    {
        public static IServiceProvider Services => ((App)App.Current).ServiceProvider;
        
        public static T? GetService<T>() where T : class
        {
            return Services.GetService(typeof(T)) as T;
        }
    }
}