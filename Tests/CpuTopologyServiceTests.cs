using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Services;
using ThreadPilot.Models;

namespace ThreadPilot.Tests
{
    /// <summary>
    /// Simple test class for CPU topology detection
    /// </summary>
    public static class CpuTopologyServiceTests
    {
        /// <summary>
        /// Test CPU topology detection
        /// </summary>
        public static async Task TestCpuTopologyDetection()
        {
            Console.WriteLine("=== CPU Topology Detection Test ===");
            
            try
            {
                // Create logger
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<CpuTopologyService>();
                
                // Create service
                var service = new CpuTopologyService(logger);
                
                // Subscribe to events
                service.TopologyDetected += (sender, e) =>
                {
                    Console.WriteLine($"Topology detected: Success={e.DetectionSuccessful}");
                    if (e.Topology != null)
                    {
                        PrintTopologyInfo(e.Topology);
                    }
                };
                
                // Detect topology
                Console.WriteLine("Starting topology detection...");
                await service.DetectTopologyAsync();
                
                // Get current topology
                var topology = service.CurrentTopology;
                if (topology != null)
                {
                    Console.WriteLine("\n=== Current Topology ===");
                    PrintTopologyInfo(topology);
                    
                    // Test affinity presets
                    Console.WriteLine("\n=== Affinity Presets ===");
                    var presets = service.GetAffinityPresets();
                    foreach (var preset in presets)
                    {
                        Console.WriteLine($"- {preset.Name}: {preset.Description}");
                        Console.WriteLine($"  Mask: 0x{preset.AffinityMask:X}");
                    }
                    
                    // Test affinity validation
                    Console.WriteLine("\n=== Affinity Validation ===");
                    var testMask = topology.CalculateAffinityMask(topology.LogicalCores);
                    var isValid = service.IsAffinityMaskValid(testMask);
                    Console.WriteLine($"Test mask 0x{testMask:X} is valid: {isValid}");
                }
                else
                {
                    Console.WriteLine("No topology detected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n=== Test Complete ===");
        }
        
        private static void PrintTopologyInfo(CpuTopologyModel topology)
        {
            Console.WriteLine($"Total Logical Cores: {topology.LogicalCores.Count}");
            Console.WriteLine($"Total Physical Cores: {topology.PhysicalCores.Count()}");
            Console.WriteLine($"Socket Count: {topology.SocketCount}");
            Console.WriteLine($"CCD Count: {topology.CcdCount}");
            Console.WriteLine($"Has Hybrid Architecture: {topology.HasHybridArchitecture}");
            Console.WriteLine($"Has SMT: {topology.HasSmt}");
            Console.WriteLine($"Architecture: {topology.Architecture}");
            
            if (topology.HasHybridArchitecture)
            {
                var pCores = topology.LogicalCores.Count(c => c.CoreType == CpuCoreType.PerformanceCore);
                var eCores = topology.LogicalCores.Count(c => c.CoreType == CpuCoreType.EfficiencyCore);
                Console.WriteLine($"P-Cores: {pCores}, E-Cores: {eCores}");
            }
            
            if (topology.CcdCount > 1)
            {
                for (int i = 0; i < topology.CcdCount; i++)
                {
                    var ccdCores = topology.LogicalCores.Count(c => c.CcdId == i);
                    Console.WriteLine($"CCD {i}: {ccdCores} cores");
                }
            }
            
            Console.WriteLine("\nCore Details:");
            foreach (var core in topology.LogicalCores.Take(Math.Min(8, topology.LogicalCores.Count)))
            {
                Console.WriteLine($"  Core {core.LogicalCoreId}: Physical={core.PhysicalCoreId}, " +
                                $"CCD={core.CcdId}, Type={core.CoreType}, HT={core.IsHyperThreaded}");
            }
            
            if (topology.LogicalCores.Count > 8)
            {
                Console.WriteLine($"  ... and {topology.LogicalCores.Count - 8} more cores");
            }
        }
    }
}
