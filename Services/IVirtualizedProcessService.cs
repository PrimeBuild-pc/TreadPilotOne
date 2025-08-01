using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Configuration for virtualized process loading
    /// </summary>
    public class VirtualizedProcessConfig
    {
        public int BatchSize { get; set; } = 50;
        public int PreloadBatches { get; set; } = 2;
        public bool EnableBackgroundLoading { get; set; } = true;
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Result of a batch loading operation
    /// </summary>
    public class ProcessBatchResult
    {
        public List<ProcessModel> Processes { get; set; } = new();
        public int BatchIndex { get; set; }
        public int TotalBatches { get; set; }
        public int TotalProcessCount { get; set; }
        public bool HasMoreBatches { get; set; }
        public TimeSpan LoadTime { get; set; }
    }

    /// <summary>
    /// Event arguments for batch loading progress
    /// </summary>
    public class BatchLoadProgressEventArgs : EventArgs
    {
        public int LoadedBatches { get; set; }
        public int TotalBatches { get; set; }
        public int LoadedProcesses { get; set; }
        public int TotalProcesses { get; set; }
        public double ProgressPercentage => TotalBatches > 0 ? (double)LoadedBatches / TotalBatches * 100 : 0;
        public string StatusMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for virtualized process loading with batch support
    /// </summary>
    public interface IVirtualizedProcessService
    {
        /// <summary>
        /// Configuration for virtualized loading
        /// </summary>
        VirtualizedProcessConfig Configuration { get; set; }

        /// <summary>
        /// Initialize the virtualized process service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Get the total number of processes available
        /// </summary>
        Task<int> GetTotalProcessCountAsync(bool activeApplicationsOnly = false);

        /// <summary>
        /// Load a specific batch of processes
        /// </summary>
        Task<ProcessBatchResult> LoadProcessBatchAsync(int batchIndex, bool activeApplicationsOnly = false);

        /// <summary>
        /// Load multiple batches starting from a specific index
        /// </summary>
        Task<List<ProcessBatchResult>> LoadProcessBatchesAsync(int startBatchIndex, int batchCount, bool activeApplicationsOnly = false);

        /// <summary>
        /// Preload the next batch in background
        /// </summary>
        Task PreloadNextBatchAsync(int currentBatchIndex, bool activeApplicationsOnly = false);

        /// <summary>
        /// Search processes across all batches
        /// </summary>
        Task<List<ProcessModel>> SearchProcessesAsync(string searchTerm, bool activeApplicationsOnly = false);

        /// <summary>
        /// Refresh a specific batch
        /// </summary>
        Task<ProcessBatchResult> RefreshBatchAsync(int batchIndex, bool activeApplicationsOnly = false);

        /// <summary>
        /// Clear all cached batches
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Event raised when batch loading progress changes
        /// </summary>
        event EventHandler<BatchLoadProgressEventArgs>? BatchLoadProgress;

        /// <summary>
        /// Event raised when background preloading completes
        /// </summary>
        event EventHandler<ProcessBatchResult>? BackgroundBatchLoaded;
    }
}
