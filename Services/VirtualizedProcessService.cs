using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of virtualized process service with batch loading and caching
    /// </summary>
    public class VirtualizedProcessService : IVirtualizedProcessService, IDisposable
    {
        private readonly IProcessService _processService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VirtualizedProcessService> _logger;
        private readonly IRetryPolicyService _retryPolicy;
        private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private readonly ConcurrentDictionary<int, ProcessBatchResult> _batchCache = new();
        private readonly System.Threading.Timer _backgroundPreloadTimer;
        
        private List<ProcessModel>? _allProcesses;
        private DateTime _lastFullRefresh = DateTime.MinValue;
        private bool _disposed;

        public VirtualizedProcessConfig Configuration { get; set; } = new();
        
        public event EventHandler<BatchLoadProgressEventArgs>? BatchLoadProgress;
        public event EventHandler<ProcessBatchResult>? BackgroundBatchLoaded;

        public VirtualizedProcessService(
            IProcessService processService,
            IMemoryCache cache,
            ILogger<VirtualizedProcessService> logger,
            IRetryPolicyService retryPolicy)
        {
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

            // Set up background preloading timer
            _backgroundPreloadTimer = new System.Threading.Timer(BackgroundPreloadCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing VirtualizedProcessService with batch size: {BatchSize}", Configuration.BatchSize);
            
            // Perform initial load to get total count
            await RefreshAllProcessesAsync(false);
            
            if (Configuration.EnableBackgroundLoading)
            {
                _backgroundPreloadTimer.Change(Configuration.RefreshInterval, Configuration.RefreshInterval);
            }
        }

        public async Task<int> GetTotalProcessCountAsync(bool activeApplicationsOnly = false)
        {
            await EnsureProcessesLoadedAsync(activeApplicationsOnly);
            
            if (activeApplicationsOnly)
            {
                return _allProcesses?.Count(p => p.HasVisibleWindow) ?? 0;
            }
            
            return _allProcesses?.Count ?? 0;
        }

        public async Task<ProcessBatchResult> LoadProcessBatchAsync(int batchIndex, bool activeApplicationsOnly = false)
        {
            var cacheKey = $"batch_{batchIndex}_{activeApplicationsOnly}";
            
            if (_batchCache.TryGetValue(cacheKey.GetHashCode(), out var cachedBatch))
            {
                _logger.LogDebug("Returning cached batch {BatchIndex}", batchIndex);
                return cachedBatch;
            }

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                
                await EnsureProcessesLoadedAsync(activeApplicationsOnly);
                
                var filteredProcesses = activeApplicationsOnly 
                    ? _allProcesses?.Where(p => p.HasVisibleWindow).ToList() ?? new List<ProcessModel>()
                    : _allProcesses ?? new List<ProcessModel>();

                var totalCount = filteredProcesses.Count;
                var totalBatches = (int)Math.Ceiling((double)totalCount / Configuration.BatchSize);
                
                var startIndex = batchIndex * Configuration.BatchSize;
                var batchProcesses = filteredProcesses
                    .Skip(startIndex)
                    .Take(Configuration.BatchSize)
                    .ToList();

                var result = new ProcessBatchResult
                {
                    Processes = batchProcesses,
                    BatchIndex = batchIndex,
                    TotalBatches = totalBatches,
                    TotalProcessCount = totalCount,
                    HasMoreBatches = batchIndex < totalBatches - 1,
                    LoadTime = stopwatch.Elapsed
                };

                // Cache the result
                _batchCache.TryAdd(cacheKey.GetHashCode(), result);
                
                _logger.LogDebug("Loaded batch {BatchIndex}/{TotalBatches} with {ProcessCount} processes in {LoadTime}ms",
                    batchIndex, totalBatches, batchProcesses.Count, stopwatch.ElapsedMilliseconds);

                return result;
            }, _retryPolicy.CreateProcessOperationPolicy());
        }

        public async Task<List<ProcessBatchResult>> LoadProcessBatchesAsync(int startBatchIndex, int batchCount, bool activeApplicationsOnly = false)
        {
            var results = new List<ProcessBatchResult>();
            var totalBatches = await GetTotalBatchCountAsync(activeApplicationsOnly);
            
            for (int i = 0; i < batchCount && (startBatchIndex + i) < totalBatches; i++)
            {
                var batchIndex = startBatchIndex + i;
                var batch = await LoadProcessBatchAsync(batchIndex, activeApplicationsOnly);
                results.Add(batch);
                
                // Report progress
                BatchLoadProgress?.Invoke(this, new BatchLoadProgressEventArgs
                {
                    LoadedBatches = i + 1,
                    TotalBatches = batchCount,
                    LoadedProcesses = results.Sum(r => r.Processes.Count),
                    TotalProcesses = batch.TotalProcessCount,
                    StatusMessage = $"Loaded batch {batchIndex + 1} of {totalBatches}"
                });
            }
            
            return results;
        }

        public async Task PreloadNextBatchAsync(int currentBatchIndex, bool activeApplicationsOnly = false)
        {
            if (!Configuration.EnableBackgroundLoading) return;
            
            var nextBatchIndex = currentBatchIndex + 1;
            var totalBatches = await GetTotalBatchCountAsync(activeApplicationsOnly);
            
            if (nextBatchIndex < totalBatches)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var batch = await LoadProcessBatchAsync(nextBatchIndex, activeApplicationsOnly);
                        BackgroundBatchLoaded?.Invoke(this, batch);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to preload batch {BatchIndex}", nextBatchIndex);
                    }
                });
            }
        }

        public async Task<List<ProcessModel>> SearchProcessesAsync(string searchTerm, bool activeApplicationsOnly = false)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ProcessModel>();

            await EnsureProcessesLoadedAsync(activeApplicationsOnly);
            
            var filteredProcesses = activeApplicationsOnly 
                ? _allProcesses?.Where(p => p.HasVisibleWindow) ?? Enumerable.Empty<ProcessModel>()
                : _allProcesses ?? Enumerable.Empty<ProcessModel>();

            return filteredProcesses
                .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           (p.MainWindowTitle?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        public async Task<ProcessBatchResult> RefreshBatchAsync(int batchIndex, bool activeApplicationsOnly = false)
        {
            var cacheKey = $"batch_{batchIndex}_{activeApplicationsOnly}";
            _batchCache.TryRemove(cacheKey.GetHashCode(), out _);
            
            // Force refresh of all processes
            await RefreshAllProcessesAsync(activeApplicationsOnly);
            
            return await LoadProcessBatchAsync(batchIndex, activeApplicationsOnly);
        }

        public void ClearCache()
        {
            _batchCache.Clear();
            _allProcesses = null;
            _lastFullRefresh = DateTime.MinValue;
            _logger.LogInformation("Cleared virtualized process cache");
        }

        private async Task EnsureProcessesLoadedAsync(bool activeApplicationsOnly)
        {
            if (_allProcesses == null || DateTime.UtcNow - _lastFullRefresh > Configuration.RefreshInterval)
            {
                await RefreshAllProcessesAsync(activeApplicationsOnly);
            }
        }

        private async Task RefreshAllProcessesAsync(bool activeApplicationsOnly)
        {
            await _loadingSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Refreshing all processes (activeOnly: {ActiveOnly})", activeApplicationsOnly);
                
                var processes = activeApplicationsOnly
                    ? await _processService.GetActiveApplicationsAsync()
                    : await _processService.GetProcessesAsync();

                _allProcesses = processes.ToList();
                _lastFullRefresh = DateTime.UtcNow;
                
                // Clear batch cache since underlying data changed
                _batchCache.Clear();
                
                _logger.LogInformation("Refreshed {ProcessCount} processes", _allProcesses.Count);
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        private async Task<int> GetTotalBatchCountAsync(bool activeApplicationsOnly)
        {
            var totalCount = await GetTotalProcessCountAsync(activeApplicationsOnly);
            return (int)Math.Ceiling((double)totalCount / Configuration.BatchSize);
        }

        private async void BackgroundPreloadCallback(object? state)
        {
            try
            {
                // Refresh processes in background
                await RefreshAllProcessesAsync(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background process refresh failed");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _backgroundPreloadTimer?.Dispose();
                    _loadingSemaphore?.Dispose();
                    _batchCache.Clear();
                    _logger.LogInformation("VirtualizedProcessService disposed");
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
