using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for detecting CPU topology using WMI and Windows APIs
    /// </summary>
    public class CpuTopologyService : ICpuTopologyService
    {
        private readonly ILogger<CpuTopologyService> _logger;
        private readonly IMemoryCache _cache;
        private CpuTopologyModel? _currentTopology;

        private const string TOPOLOGY_CACHE_KEY = "cpu_topology";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(1);

        public event EventHandler<CpuTopologyDetectedEventArgs>? TopologyDetected;
        public CpuTopologyModel? CurrentTopology => _currentTopology;

        public CpuTopologyService(ILogger<CpuTopologyService> logger, IMemoryCache? cache = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 10,
                CompactionPercentage = 0.1
            });
        }

        public async Task<CpuTopologyModel> DetectTopologyAsync()
        {
            // PERFORMANCE IMPROVEMENT: Check cache first to avoid expensive WMI calls
            if (_cache.TryGetValue(TOPOLOGY_CACHE_KEY, out CpuTopologyModel? cachedTopology))
            {
                _logger.LogInformation("CPU topology retrieved from cache");
                _currentTopology = cachedTopology;
                return cachedTopology;
            }

            try
            {
                _logger.LogInformation("Starting CPU topology detection (cache miss)");
                
                var topology = new CpuTopologyModel();
                
                // Get basic system information
                await DetectBasicCpuInfoAsync(topology);
                
                // Detect logical cores using multiple methods
                await DetectLogicalCoresAsync(topology);
                
                // Try to detect advanced topology (CCD, P/E cores, etc.)
                await DetectAdvancedTopologyAsync(topology);
                
                // Validate and finalize topology
                ValidateTopology(topology);
                
                _currentTopology = topology;
                topology.TopologyDetectionSuccessful = true;

                // PERFORMANCE IMPROVEMENT: Cache the topology to avoid expensive WMI calls
                _cache.Set(TOPOLOGY_CACHE_KEY, topology, CACHE_DURATION);

                _logger.LogInformation("CPU topology detection completed successfully and cached. " +
                    "Logical cores: {LogicalCores}, Physical cores: {PhysicalCores}, " +
                    "Sockets: {Sockets}, HT: {HasHT}, Hybrid: {HasHybrid}, CCD: {HasCcd}",
                    topology.TotalLogicalCores, topology.TotalPhysicalCores, topology.TotalSockets,
                    topology.HasHyperThreading, topology.HasIntelHybrid, topology.HasAmdCcd);

                TopologyDetected?.Invoke(this, new CpuTopologyDetectedEventArgs(topology, true));
                return topology;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detect CPU topology");
                
                // Create fallback topology
                var fallbackTopology = CreateFallbackTopology();
                _currentTopology = fallbackTopology;
                
                TopologyDetected?.Invoke(this, new CpuTopologyDetectedEventArgs(fallbackTopology, false, ex.Message));
                return fallbackTopology;
            }
        }

        private async Task DetectBasicCpuInfoAsync(CpuTopologyModel topology)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                using var collection = searcher.Get();
                
                foreach (ManagementObject processor in collection)
                {
                    topology.CpuBrand = processor["Name"]?.ToString() ?? "Unknown";
                    topology.CpuArchitecture = processor["Architecture"]?.ToString() ?? "Unknown";
                    break; // Take first processor for basic info
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect basic CPU info via WMI");
            }
        }

        private async Task DetectLogicalCoresAsync(CpuTopologyModel topology)
        {
            try
            {
                // Method 1: Use Environment.ProcessorCount as baseline
                int logicalCoreCount = Environment.ProcessorCount;
                
                // Method 2: Try WMI for more detailed information
                await DetectCoresViaWmiAsync(topology);
                
                // If WMI failed, create basic topology
                if (topology.LogicalCores.Count == 0)
                {
                    CreateBasicTopology(topology, logicalCoreCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect logical cores, using fallback");
                CreateBasicTopology(topology, Environment.ProcessorCount);
            }
        }

        private async Task DetectCoresViaWmiAsync(CpuTopologyModel topology)
        {
            try
            {
                // First, get physical processor information
                var physicalCoreCount = 0;
                var logicalCoreCount = 0;

                using (var processorSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                using (var processorCollection = processorSearcher.Get())
                {
                    foreach (ManagementObject processor in processorCollection)
                    {
                        var numberOfCores = Convert.ToInt32(processor["NumberOfCores"] ?? 0);
                        var numberOfLogicalProcessors = Convert.ToInt32(processor["NumberOfLogicalProcessors"] ?? 0);

                        physicalCoreCount += numberOfCores;
                        logicalCoreCount += numberOfLogicalProcessors;

                        _logger.LogInformation("Detected CPU: {Cores} physical cores, {LogicalProcessors} logical processors",
                            numberOfCores, numberOfLogicalProcessors);
                    }
                }

                // If WMI didn't provide the info, fall back to Environment.ProcessorCount
                if (logicalCoreCount == 0)
                {
                    logicalCoreCount = Environment.ProcessorCount;
                    physicalCoreCount = logicalCoreCount; // Assume no HT if we can't detect
                }

                // Create logical cores with proper physical core mapping
                var hasHyperThreading = logicalCoreCount > physicalCoreCount;
                var threadsPerCore = hasHyperThreading ? logicalCoreCount / physicalCoreCount : 1;

                for (int logicalId = 0; logicalId < logicalCoreCount; logicalId++)
                {
                    var physicalId = logicalId / threadsPerCore;
                    var isHyperThreaded = hasHyperThreading && (logicalId % threadsPerCore != 0);
                    var htSibling = hasHyperThreading ? (logicalId % threadsPerCore == 0 ? logicalId + 1 : logicalId - 1) : (int?)null;

                    var core = new CpuCoreModel
                    {
                        LogicalCoreId = logicalId,
                        PhysicalCoreId = physicalId,
                        SocketId = 0, // Will be refined later
                        Label = $"Core {logicalId}",
                        IsEnabled = true,
                        IsHyperThreaded = isHyperThreaded,
                        HyperThreadSibling = htSibling
                    };

                    topology.LogicalCores.Add(core);
                }

                _logger.LogInformation("Created topology: {LogicalCores} logical cores, {PhysicalCores} physical cores, HT: {HasHT}",
                    logicalCoreCount, physicalCoreCount, hasHyperThreading);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WMI logical processor detection failed");
            }
        }

        private void CreateBasicTopology(CpuTopologyModel topology, int logicalCoreCount)
        {
            topology.LogicalCores.Clear();
            
            for (int i = 0; i < logicalCoreCount; i++)
            {
                var core = new CpuCoreModel
                {
                    LogicalCoreId = i,
                    PhysicalCoreId = i, // Assume no HT for basic topology
                    SocketId = 0,
                    CoreType = CpuCoreType.Standard,
                    Label = $"Core {i}",
                    IsEnabled = true
                };
                
                topology.LogicalCores.Add(core);
            }
        }

        private async Task DetectAdvancedTopologyAsync(CpuTopologyModel topology)
        {
            // Try to detect Intel Hybrid (P/E cores)
            await DetectIntelHybridAsync(topology);
            
            // Try to detect AMD CCD information
            await DetectAmdCcdAsync(topology);
            
            // Try to detect HyperThreading
            DetectHyperThreading(topology);
        }

        private async Task DetectIntelHybridAsync(CpuTopologyModel topology)
        {
            try
            {
                // Intel Hybrid detection is complex and requires specific APIs
                // For now, we'll use heuristics based on CPU brand and core count patterns
                if (topology.CpuBrand.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                {
                    // Check for 12th gen or later Intel processors (Alder Lake+)
                    if (topology.CpuBrand.Contains("12th") || topology.CpuBrand.Contains("13th") || 
                        topology.CpuBrand.Contains("14th") || topology.CpuBrand.Contains("15th"))
                    {
                        // Heuristic: Assume first cores are P-cores, later ones are E-cores
                        // This is a simplified approach - real detection would require CPUID
                        var totalCores = topology.LogicalCores.Count;
                        var estimatedPCores = Math.Min(8, totalCores / 2); // Rough estimate
                        
                        for (int i = 0; i < topology.LogicalCores.Count; i++)
                        {
                            if (i < estimatedPCores * 2) // P-cores with HT
                            {
                                topology.LogicalCores[i].CoreType = CpuCoreType.PerformanceCore;
                            }
                            else
                            {
                                topology.LogicalCores[i].CoreType = CpuCoreType.EfficiencyCore;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect Intel Hybrid topology");
            }
        }

        private async Task DetectAmdCcdAsync(CpuTopologyModel topology)
        {
            try
            {
                if (topology.CpuBrand.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                {
                    // AMD CCD detection - improved heuristic
                    // Only assign CCD if we actually have multiple CCDs
                    var totalPhysicalCores = topology.TotalPhysicalCores;
                    var coresPerCcd = 8; // Typical for Zen 2/3/4

                    // Only assign CCD IDs if we have more than 8 physical cores (indicating multiple CCDs)
                    if (totalPhysicalCores > coresPerCcd)
                    {
                        for (int i = 0; i < topology.LogicalCores.Count; i++)
                        {
                            var physicalCoreId = topology.LogicalCores[i].PhysicalCoreId;
                            topology.LogicalCores[i].CcdId = physicalCoreId / coresPerCcd;
                            topology.LogicalCores[i].CoreType = CpuCoreType.Zen3; // Default assumption
                        }

                        _logger.LogInformation("Detected AMD multi-CCD configuration: {PhysicalCores} physical cores, estimated {CcdCount} CCDs",
                            totalPhysicalCores, (totalPhysicalCores + coresPerCcd - 1) / coresPerCcd);
                    }
                    else
                    {
                        // Single CCD or small core count - don't assign CCD IDs
                        foreach (var core in topology.LogicalCores)
                        {
                            core.CoreType = CpuCoreType.Zen3; // Default assumption
                        }

                        _logger.LogInformation("Detected AMD single-CCD configuration: {PhysicalCores} physical cores", totalPhysicalCores);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect AMD CCD topology");
            }
        }

        private void DetectHyperThreading(CpuTopologyModel topology)
        {
            try
            {
                // Simple HT detection: if we have more logical than physical cores
                var logicalCount = topology.LogicalCores.Count;
                var physicalCount = topology.TotalPhysicalCores;
                
                if (logicalCount > physicalCount)
                {
                    // Mark cores as HT siblings
                    for (int i = 0; i < topology.LogicalCores.Count; i += 2)
                    {
                        if (i + 1 < topology.LogicalCores.Count)
                        {
                            topology.LogicalCores[i].IsHyperThreaded = true;
                            topology.LogicalCores[i].HyperThreadSibling = i + 1;
                            topology.LogicalCores[i + 1].IsHyperThreaded = true;
                            topology.LogicalCores[i + 1].HyperThreadSibling = i;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect HyperThreading");
            }
        }

        private void ValidateTopology(CpuTopologyModel topology)
        {
            // Ensure we have at least one core
            if (topology.LogicalCores.Count == 0)
            {
                CreateBasicTopology(topology, Environment.ProcessorCount);
            }
            
            // Ensure logical core IDs are sequential
            for (int i = 0; i < topology.LogicalCores.Count; i++)
            {
                topology.LogicalCores[i].LogicalCoreId = i;
            }
            
            // Update labels - simplified without CCD clutter
            foreach (var core in topology.LogicalCores)
            {
                var typeLabel = core.CoreType switch
                {
                    CpuCoreType.PerformanceCore => "P",
                    CpuCoreType.EfficiencyCore => "E",
                    _ => ""
                };

                var htLabel = core.IsHyperThreaded ? " (HT)" : "";

                core.Label = $"{typeLabel}Core {core.LogicalCoreId}{htLabel}";
            }
        }

        private CpuTopologyModel CreateFallbackTopology()
        {
            var topology = new CpuTopologyModel();
            CreateBasicTopology(topology, Environment.ProcessorCount);
            topology.TopologyDetectionSuccessful = false;
            return topology;
        }

        public IEnumerable<CpuAffinityPreset> GetAffinityPresets()
        {
            if (_currentTopology == null)
                return Enumerable.Empty<CpuAffinityPreset>();

            var presets = new List<CpuAffinityPreset>();

            // All cores preset
            presets.Add(new CpuAffinityPreset
            {
                Name = "All Cores",
                Description = $"All {_currentTopology.TotalLogicalCores} logical cores",
                AffinityMask = (1L << _currentTopology.TotalLogicalCores) - 1,
                IsAvailable = true
            });

            // Physical cores only (if HT is available)
            if (_currentTopology.HasHyperThreading)
            {
                presets.Add(new CpuAffinityPreset
                {
                    Name = "Physical Cores Only",
                    Description = $"All {_currentTopology.TotalPhysicalCores} physical cores (no HyperThreading)",
                    AffinityMask = _currentTopology.GetPhysicalCoresAffinityMask(),
                    IsAvailable = true
                });
            }

            // Performance cores (Intel Hybrid)
            if (_currentTopology.HasIntelHybrid && _currentTopology.PerformanceCores.Any())
            {
                presets.Add(new CpuAffinityPreset
                {
                    Name = "Performance Cores",
                    Description = $"Intel P-cores ({_currentTopology.PerformanceCores.Count()} cores)",
                    AffinityMask = _currentTopology.GetPerformanceCoresAffinityMask(),
                    IsAvailable = true
                });
            }

            // Efficiency cores (Intel Hybrid)
            if (_currentTopology.HasIntelHybrid && _currentTopology.EfficiencyCores.Any())
            {
                presets.Add(new CpuAffinityPreset
                {
                    Name = "Efficiency Cores",
                    Description = $"Intel E-cores ({_currentTopology.EfficiencyCores.Count()} cores)",
                    AffinityMask = _currentTopology.GetEfficiencyCoresAffinityMask(),
                    IsAvailable = true
                });
            }

            // CCD presets (AMD)
            if (_currentTopology.HasAmdCcd)
            {
                foreach (var ccdId in _currentTopology.AvailableCcds)
                {
                    var ccdCores = _currentTopology.GetCoresByCcd(ccdId);
                    presets.Add(new CpuAffinityPreset
                    {
                        Name = $"CCD {ccdId}",
                        Description = $"AMD CCD {ccdId} ({ccdCores.Count()} cores)",
                        AffinityMask = _currentTopology.GetCcdAffinityMask(ccdId),
                        IsAvailable = true
                    });
                }
            }

            return presets;
        }

        public bool IsAffinityMaskValid(long affinityMask)
        {
            if (_currentTopology == null) return false;
            
            var maxMask = (1L << _currentTopology.TotalLogicalCores) - 1;
            return affinityMask > 0 && affinityMask <= maxMask;
        }

        public int GetMaxLogicalCores()
        {
            return _currentTopology?.TotalLogicalCores ?? Environment.ProcessorCount;
        }

        public async Task RefreshTopologyAsync()
        {
            await DetectTopologyAsync();
        }
    }
}
