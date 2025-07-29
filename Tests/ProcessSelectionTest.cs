using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using ThreadPilot.Services;
using ThreadPilot.Models;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Tests
{
    /// <summary>
    /// Test class to validate the improved process selection and real-time data sync functionality
    /// </summary>
    public class ProcessSelectionTest
    {
        private readonly ProcessService _processService;
        private readonly CpuTopologyService _cpuTopologyService;

        public ProcessSelectionTest()
        {
            _processService = new ProcessService();

            // Create a simple logger for the CPU topology service
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<CpuTopologyService>();
            _cpuTopologyService = new CpuTopologyService(logger);
        }

        /// <summary>
        /// Test that process information is correctly refreshed and reflects actual OS state
        /// </summary>
        public async Task<bool> TestProcessInfoRefresh()
        {
            try
            {
                Console.WriteLine("Testing process info refresh...");
                
                // Get current process as test subject
                var currentProcess = Process.GetCurrentProcess();
                var processModel = _processService.CreateProcessModel(currentProcess);
                
                Console.WriteLine($"Initial process info - PID: {processModel.ProcessId}, Priority: {processModel.Priority}, Affinity: 0x{processModel.ProcessorAffinity:X}");
                
                // Refresh the process info
                await _processService.RefreshProcessInfo(processModel);
                
                Console.WriteLine($"After refresh - PID: {processModel.ProcessId}, Priority: {processModel.Priority}, Affinity: 0x{processModel.ProcessorAffinity:X}");
                
                // Verify the data is consistent
                bool isValid = processModel.ProcessId == currentProcess.Id &&
                              processModel.Priority == currentProcess.PriorityClass &&
                              processModel.ProcessorAffinity == (long)currentProcess.ProcessorAffinity;
                
                Console.WriteLine($"Process info refresh test: {(isValid ? "PASSED" : "FAILED")}");
                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Process info refresh test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test that process termination is properly detected
        /// </summary>
        public async Task<bool> TestProcessTerminationDetection()
        {
            try
            {
                Console.WriteLine("Testing process termination detection...");
                
                // Start a short-lived process
                var notepadProcess = Process.Start("notepad.exe");
                if (notepadProcess == null)
                {
                    Console.WriteLine("Could not start test process");
                    return false;
                }
                
                var processModel = _processService.CreateProcessModel(notepadProcess);
                Console.WriteLine($"Started test process - PID: {processModel.ProcessId}");
                
                // Verify process is running
                bool isRunning = await _processService.IsProcessStillRunning(processModel);
                Console.WriteLine($"Process running check: {isRunning}");
                
                // Terminate the process
                notepadProcess.Kill();
                await Task.Delay(1000); // Wait for termination
                
                // Check if termination is detected
                bool isStillRunning = await _processService.IsProcessStillRunning(processModel);
                Console.WriteLine($"Process running after termination: {isStillRunning}");
                
                bool testPassed = isRunning && !isStillRunning;
                Console.WriteLine($"Process termination detection test: {(testPassed ? "PASSED" : "FAILED")}");
                
                return testPassed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Process termination detection test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test active applications filtering
        /// </summary>
        public async Task<bool> TestActiveApplicationsFiltering()
        {
            try
            {
                Console.WriteLine("Testing active applications filtering...");
                
                var allProcesses = await _processService.GetProcessesAsync();
                var activeApps = await _processService.GetActiveApplicationsAsync();
                
                Console.WriteLine($"Total processes: {allProcesses.Count}");
                Console.WriteLine($"Active applications: {activeApps.Count}");
                
                // Verify that active apps is a subset of all processes
                bool isSubset = activeApps.Count <= allProcesses.Count;
                
                // Verify that all active apps have visible windows
                bool allHaveWindows = true;
                foreach (var app in activeApps)
                {
                    if (!app.HasVisibleWindow)
                    {
                        allHaveWindows = false;
                        Console.WriteLine($"Process {app.Name} marked as active but has no visible window");
                        break;
                    }
                }
                
                bool testPassed = isSubset && allHaveWindows;
                Console.WriteLine($"Active applications filtering test: {(testPassed ? "PASSED" : "FAILED")}");
                
                return testPassed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Active applications filtering test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test CPU affinity mask conversion and core selection
        /// </summary>
        public async Task<bool> TestCpuAffinityMaskConversion()
        {
            try
            {
                Console.WriteLine("Testing CPU affinity mask conversion...");

                // Get current process as test subject
                var currentProcess = Process.GetCurrentProcess();
                var processModel = _processService.CreateProcessModel(currentProcess);

                Console.WriteLine($"Process affinity mask: 0x{processModel.ProcessorAffinity:X} ({Convert.ToString(processModel.ProcessorAffinity, 2).PadLeft(Environment.ProcessorCount, '0')})");

                // Test affinity mask bit calculations
                var totalCores = Environment.ProcessorCount;
                var expectedSelectedCores = new List<int>();

                for (int i = 0; i < totalCores; i++)
                {
                    long coreMask = 1L << i;
                    if ((processModel.ProcessorAffinity & coreMask) != 0)
                    {
                        expectedSelectedCores.Add(i);
                    }
                }

                Console.WriteLine($"Expected selected cores based on affinity mask: [{string.Join(", ", expectedSelectedCores)}]");
                Console.WriteLine($"Total cores: {totalCores}, Selected cores: {expectedSelectedCores.Count}");

                // Verify that at least one core is selected (process must run on something)
                bool hasSelectedCores = expectedSelectedCores.Count > 0;

                // Verify that selected cores don't exceed total cores
                bool validCoreCount = expectedSelectedCores.Count <= totalCores;

                // Verify that all selected core IDs are within valid range
                bool validCoreIds = expectedSelectedCores.All(id => id >= 0 && id < totalCores);

                bool testPassed = hasSelectedCores && validCoreCount && validCoreIds;
                Console.WriteLine($"CPU affinity mask conversion test: {(testPassed ? "PASSED" : "FAILED")}");

                if (!testPassed)
                {
                    Console.WriteLine($"  - Has selected cores: {hasSelectedCores}");
                    Console.WriteLine($"  - Valid core count: {validCoreCount}");
                    Console.WriteLine($"  - Valid core IDs: {validCoreIds}");
                }

                return testPassed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CPU affinity mask conversion test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test hyperthreading/SMT status detection and display
        /// </summary>
        public async Task<bool> TestHyperThreadingStatusDetection()
        {
            try
            {
                Console.WriteLine("Testing hyperthreading/SMT status detection...");

                // Get CPU topology information
                await _cpuTopologyService.DetectTopologyAsync();
                var topology = _cpuTopologyService.CurrentTopology;

                if (topology == null)
                {
                    Console.WriteLine("Hyperthreading status test FAILED: Could not detect CPU topology");
                    return false;
                }

                Console.WriteLine($"CPU Brand: {topology.CpuBrand}");
                Console.WriteLine($"Total Logical Cores: {topology.TotalLogicalCores}");
                Console.WriteLine($"Total Physical Cores: {topology.TotalPhysicalCores}");
                Console.WriteLine($"Has Hyperthreading/SMT: {topology.HasHyperThreading}");

                // Determine expected technology name
                string expectedTechName = "Multi-Threading";
                if (topology.CpuBrand.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                {
                    expectedTechName = "Hyper-Threading";
                }
                else if (topology.CpuBrand.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                {
                    expectedTechName = "SMT";
                }

                Console.WriteLine($"Expected technology name: {expectedTechName}");

                // Verify hyperthreading detection logic
                bool expectedHasHT = topology.TotalLogicalCores > topology.TotalPhysicalCores;
                bool actualHasHT = topology.HasHyperThreading;

                bool detectionCorrect = expectedHasHT == actualHasHT;
                Console.WriteLine($"Hyperthreading detection: Expected={expectedHasHT}, Actual={actualHasHT}, Correct={detectionCorrect}");

                // Verify that if HT is detected, there are actually HT cores marked
                bool htCoresMarkedCorrectly = true;
                if (actualHasHT)
                {
                    var htCores = topology.LogicalCores.Where(c => c.IsHyperThreaded).ToList();
                    htCoresMarkedCorrectly = htCores.Count > 0;
                    Console.WriteLine($"HyperThreaded cores found: {htCores.Count}");
                }

                bool testPassed = detectionCorrect && htCoresMarkedCorrectly;
                Console.WriteLine($"Hyperthreading status detection test: {(testPassed ? "PASSED" : "FAILED")}");

                return testPassed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hyperthreading status detection test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public async Task<bool> RunAllTests()
        {
            Console.WriteLine("=== Process Selection and Real-time Data Sync Tests ===");
            Console.WriteLine();

            bool test1 = await TestProcessInfoRefresh();
            Console.WriteLine();

            bool test2 = await TestProcessTerminationDetection();
            Console.WriteLine();

            bool test3 = await TestActiveApplicationsFiltering();
            Console.WriteLine();

            bool test4 = await TestCpuAffinityMaskConversion();
            Console.WriteLine();

            bool test5 = await TestHyperThreadingStatusDetection();
            Console.WriteLine();

            bool allPassed = test1 && test2 && test3 && test4 && test5;
            Console.WriteLine($"=== Overall Test Result: {(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED")} ===");

            return allPassed;
        }
    }
}
