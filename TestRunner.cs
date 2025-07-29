using System;
using System.Threading.Tasks;
using ThreadPilot.Tests;

namespace ThreadPilot
{
    /// <summary>
    /// Simple test runner for CPU topology functionality
    /// </summary>
    public static class TestRunner
    {
        /// <summary>
        /// Main test entry point
        /// </summary>
        public static async Task RunTests()
        {
            Console.WriteLine("ThreadPilot CPU Topology Test Runner");
            Console.WriteLine("====================================");
            
            try
            {
                await CpuTopologyServiceTests.TestCpuTopologyDetection();

                Console.WriteLine();

                // Run Process Selection Test
                var processSelectionTest = new ProcessSelectionTest();
                await processSelectionTest.RunAllTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
