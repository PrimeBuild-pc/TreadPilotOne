using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a logical CPU core with topology information
    /// </summary>
    public class CpuCoreModel
    {
        public int LogicalCoreId { get; set; }
        public int PhysicalCoreId { get; set; }
        public int SocketId { get; set; }
        public int? CcdId { get; set; } // Core Complex Die (AMD)
        public int? ClusterId { get; set; } // Intel Cluster
        public CpuCoreType CoreType { get; set; } = CpuCoreType.Unknown;
        public bool IsHyperThreaded { get; set; }
        public int? HyperThreadSibling { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool IsSelected { get; set; } = false;
        
        /// <summary>
        /// Gets the affinity mask bit for this logical core
        /// </summary>
        public long AffinityMask => 1L << LogicalCoreId;
    }

    /// <summary>
    /// Types of CPU cores
    /// </summary>
    public enum CpuCoreType
    {
        Unknown,
        Standard,
        PerformanceCore, // Intel P-cores
        EfficiencyCore,  // Intel E-cores
        Zen,             // AMD Zen cores
        ZenPlus,         // AMD Zen+ cores
        Zen2,            // AMD Zen2 cores
        Zen3,            // AMD Zen3 cores
        Zen4             // AMD Zen4 cores
    }

    /// <summary>
    /// Represents CPU topology information
    /// </summary>
    public class CpuTopologyModel
    {
        public List<CpuCoreModel> LogicalCores { get; set; } = new();
        public int TotalLogicalCores => LogicalCores.Count;
        public int TotalPhysicalCores => LogicalCores.GroupBy(c => c.PhysicalCoreId).Count();
        public int TotalSockets => LogicalCores.GroupBy(c => c.SocketId).Count();
        public int SocketCount => TotalSockets; // Alias for TotalSockets
        public bool HasHyperThreading => LogicalCores.Any(c => c.IsHyperThreaded);
        public bool HasSmt => HasHyperThreading; // SMT is AMD's term for HyperThreading
        public bool HasIntelHybrid => LogicalCores.Any(c => c.CoreType == CpuCoreType.PerformanceCore || c.CoreType == CpuCoreType.EfficiencyCore);
        public bool HasHybridArchitecture => HasIntelHybrid; // Alias for HasIntelHybrid
        public bool HasAmdCcd => LogicalCores.Any(c => c.CcdId.HasValue);
        public int CcdCount => LogicalCores.Where(c => c.CcdId.HasValue).Select(c => c.CcdId!.Value).Distinct().Count();
        public string Architecture => CpuArchitecture; // Alias for CpuArchitecture
        public string CpuArchitecture { get; set; } = "Unknown";
        public string CpuBrand { get; set; } = "Unknown";
        public bool TopologyDetectionSuccessful { get; set; } = false;

        /// <summary>
        /// Gets all CCDs (Core Complex Dies) available
        /// </summary>
        public IEnumerable<int> AvailableCcds => LogicalCores
            .Where(c => c.CcdId.HasValue)
            .Select(c => c.CcdId!.Value)
            .Distinct()
            .OrderBy(id => id);

        /// <summary>
        /// Gets all performance cores (Intel P-cores)
        /// </summary>
        public IEnumerable<CpuCoreModel> PerformanceCores => LogicalCores
            .Where(c => c.CoreType == CpuCoreType.PerformanceCore);

        /// <summary>
        /// Gets all efficiency cores (Intel E-cores)
        /// </summary>
        public IEnumerable<CpuCoreModel> EfficiencyCores => LogicalCores
            .Where(c => c.CoreType == CpuCoreType.EfficiencyCore);

        /// <summary>
        /// Gets all physical cores (one logical core per physical core, excluding HT siblings)
        /// </summary>
        public IEnumerable<CpuCoreModel> PhysicalCores => LogicalCores
            .GroupBy(c => c.PhysicalCoreId)
            .Select(g => g.OrderBy(c => c.LogicalCoreId).First());

        /// <summary>
        /// Gets cores by CCD ID
        /// </summary>
        public IEnumerable<CpuCoreModel> GetCoresByCcd(int ccdId) => LogicalCores
            .Where(c => c.CcdId == ccdId);

        /// <summary>
        /// Gets cores by socket ID
        /// </summary>
        public IEnumerable<CpuCoreModel> GetCoresBySocket(int socketId) => LogicalCores
            .Where(c => c.SocketId == socketId);

        /// <summary>
        /// Calculates affinity mask for selected cores
        /// </summary>
        public long CalculateAffinityMask(IEnumerable<CpuCoreModel> cores)
        {
            return cores.Aggregate(0L, (mask, core) => mask | core.AffinityMask);
        }

        /// <summary>
        /// Gets affinity mask for all physical cores (excluding HT siblings)
        /// </summary>
        public long GetPhysicalCoresAffinityMask() => CalculateAffinityMask(PhysicalCores);

        /// <summary>
        /// Gets affinity mask for performance cores
        /// </summary>
        public long GetPerformanceCoresAffinityMask() => CalculateAffinityMask(PerformanceCores);

        /// <summary>
        /// Gets affinity mask for efficiency cores
        /// </summary>
        public long GetEfficiencyCoresAffinityMask() => CalculateAffinityMask(EfficiencyCores);

        /// <summary>
        /// Gets affinity mask for a specific CCD
        /// </summary>
        public long GetCcdAffinityMask(int ccdId) => CalculateAffinityMask(GetCoresByCcd(ccdId));
    }

    /// <summary>
    /// Quick selection preset for CPU affinity
    /// </summary>
    public class CpuAffinityPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long AffinityMask { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string UnavailableReason { get; set; } = string.Empty;
    }
}
