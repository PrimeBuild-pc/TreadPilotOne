using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of retry policy service with exponential backoff
    /// </summary>
    public class RetryPolicyService : IRetryPolicyService
    {
        private readonly ILogger<RetryPolicyService> _logger;

        public RetryPolicyService(ILogger<RetryPolicyService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, RetryPolicy? policy = null)
        {
            policy ??= CreateDefaultPolicy();
            
            Exception? lastException = null;
            var delay = policy.InitialDelay;

            for (int attempt = 1; attempt <= policy.MaxAttempts; attempt++)
            {
                try
                {
                    var result = await operation();
                    if (attempt > 1)
                    {
                        _logger.LogInformation("Operation succeeded on attempt {Attempt}", attempt);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt == policy.MaxAttempts || (policy.ShouldRetry != null && !policy.ShouldRetry(ex)))
                    {
                        _logger.LogError(ex, "Operation failed after {Attempts} attempts", attempt);
                        throw;
                    }

                    _logger.LogWarning(ex, "Operation failed on attempt {Attempt}, retrying in {Delay}ms", 
                        attempt, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(Math.Min(
                        delay.TotalMilliseconds * policy.BackoffMultiplier,
                        policy.MaxDelay.TotalMilliseconds));
                }
            }

            throw lastException ?? new InvalidOperationException("Retry loop completed without result");
        }

        public async Task ExecuteAsync(Func<Task> operation, RetryPolicy? policy = null)
        {
            await ExecuteAsync(async () =>
            {
                await operation();
                return true; // Dummy return value
            }, policy);
        }

        public RetryPolicy CreateProcessOperationPolicy()
        {
            return new RetryPolicy
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(2),
                BackoffMultiplier = 1.5,
                ShouldRetry = ex => ex switch
                {
                    Win32Exception win32Ex when win32Ex.NativeErrorCode == 5 => false, // Access denied - don't retry
                    InvalidOperationException invalidOp when invalidOp.Message.Contains("terminated") => false, // Process terminated
                    UnauthorizedAccessException => false, // Permission issue - don't retry
                    _ => true // Retry other exceptions
                }
            };
        }

        public RetryPolicy CreateWmiOperationPolicy()
        {
            return new RetryPolicy
            {
                MaxAttempts = 4,
                InitialDelay = TimeSpan.FromMilliseconds(500),
                MaxDelay = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 2.0,
                ShouldRetry = ex => ex switch
                {
                    ManagementException mgmtEx when mgmtEx.ErrorCode == ManagementStatus.AccessDenied => false,
                    ManagementException mgmtEx when mgmtEx.ErrorCode == ManagementStatus.NotFound => false,
                    _ => true
                }
            };
        }

        public RetryPolicy CreateFileOperationPolicy()
        {
            return new RetryPolicy
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                MaxDelay = TimeSpan.FromSeconds(1),
                BackoffMultiplier = 2.0,
                ShouldRetry = ex => ex switch
                {
                    FileNotFoundException => false, // File doesn't exist - don't retry
                    DirectoryNotFoundException => false, // Directory doesn't exist - don't retry
                    UnauthorizedAccessException => false, // Permission issue - don't retry
                    IOException ioEx when ioEx.Message.Contains("being used by another process") => true, // File in use - retry
                    _ => true
                }
            };
        }

        private static RetryPolicy CreateDefaultPolicy()
        {
            return new RetryPolicy
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                MaxDelay = TimeSpan.FromSeconds(2),
                BackoffMultiplier = 2.0
            };
        }
    }
}
