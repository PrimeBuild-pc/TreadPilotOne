using System;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public class RetryPolicy
    {
        public int MaxAttempts { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);
        public double BackoffMultiplier { get; set; } = 2.0;
        public Func<Exception, bool>? ShouldRetry { get; set; }
    }

    /// <summary>
    /// Service for implementing retry policies with exponential backoff
    /// </summary>
    public interface IRetryPolicyService
    {
        /// <summary>
        /// Execute an operation with retry policy
        /// </summary>
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, RetryPolicy? policy = null);

        /// <summary>
        /// Execute an operation with retry policy (no return value)
        /// </summary>
        Task ExecuteAsync(Func<Task> operation, RetryPolicy? policy = null);

        /// <summary>
        /// Create a default retry policy for process operations
        /// </summary>
        RetryPolicy CreateProcessOperationPolicy();

        /// <summary>
        /// Create a default retry policy for WMI operations
        /// </summary>
        RetryPolicy CreateWmiOperationPolicy();

        /// <summary>
        /// Create a default retry policy for file operations
        /// </summary>
        RetryPolicy CreateFileOperationPolicy();
    }
}
