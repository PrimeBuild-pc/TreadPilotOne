using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of service health monitoring
    /// </summary>
    public class ServiceHealthMonitor : IServiceHealthMonitor, IDisposable
    {
        private readonly ILogger<ServiceHealthMonitor> _logger;
        private readonly ConcurrentDictionary<string, Func<Task<ServiceHealthResult>>> _healthChecks = new();
        private readonly ConcurrentDictionary<string, ServiceHealthResult> _lastResults = new();
        private readonly object _lockObject = new();
        private bool _disposed;

        public event EventHandler<ServiceHealthChangedEventArgs>? ServiceHealthChanged;

        public ServiceHealthMonitor(ILogger<ServiceHealthMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterService(string serviceName, Func<Task<ServiceHealthResult>> healthCheck)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
            
            if (healthCheck == null)
                throw new ArgumentNullException(nameof(healthCheck));

            _healthChecks.AddOrUpdate(serviceName, healthCheck, (key, oldValue) => healthCheck);
            _logger.LogInformation("Registered health check for service: {ServiceName}", serviceName);
        }

        public void UnregisterService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                return;

            _healthChecks.TryRemove(serviceName, out _);
            _lastResults.TryRemove(serviceName, out _);
            _logger.LogInformation("Unregistered health check for service: {ServiceName}", serviceName);
        }

        public async Task<ServiceHealthResult> CheckServiceHealthAsync(string serviceName)
        {
            if (!_healthChecks.TryGetValue(serviceName, out var healthCheck))
            {
                return new ServiceHealthResult
                {
                    ServiceName = serviceName,
                    Status = ServiceHealthStatus.Critical,
                    Description = "Service not registered for health monitoring",
                    CheckTime = DateTime.UtcNow
                };
            }

            var stopwatch = Stopwatch.StartNew();
            ServiceHealthResult result;

            try
            {
                result = await healthCheck();
                result.ResponseTime = stopwatch.Elapsed;
                result.CheckTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for service: {ServiceName}", serviceName);
                result = new ServiceHealthResult
                {
                    ServiceName = serviceName,
                    Status = ServiceHealthStatus.Critical,
                    Description = $"Health check threw exception: {ex.Message}",
                    ResponseTime = stopwatch.Elapsed,
                    CheckTime = DateTime.UtcNow,
                    Exception = ex
                };
            }

            // Check if status changed and raise event
            if (_lastResults.TryGetValue(serviceName, out var lastResult))
            {
                if (lastResult.Status != result.Status)
                {
                    ServiceHealthChanged?.Invoke(this, new ServiceHealthChangedEventArgs
                    {
                        ServiceName = serviceName,
                        PreviousStatus = lastResult.Status,
                        CurrentStatus = result.Status,
                        HealthResult = result
                    });
                }
            }

            _lastResults.AddOrUpdate(serviceName, result, (key, oldValue) => result);
            return result;
        }

        public async Task<Dictionary<string, ServiceHealthResult>> CheckAllServicesHealthAsync()
        {
            var results = new Dictionary<string, ServiceHealthResult>();
            var tasks = _healthChecks.Keys.Select(async serviceName =>
            {
                var result = await CheckServiceHealthAsync(serviceName);
                lock (_lockObject)
                {
                    results[serviceName] = result;
                }
            });

            await Task.WhenAll(tasks);
            return results;
        }

        public Dictionary<string, ServiceHealthResult> GetCurrentHealthStatus()
        {
            return _lastResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _healthChecks.Clear();
                    _lastResults.Clear();
                    _logger.LogInformation("ServiceHealthMonitor disposed");
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
