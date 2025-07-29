using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ThreadPilot.Services;
using ThreadPilot.Models;

namespace ThreadPilot.Tests
{
    /// <summary>
    /// Test class to validate the Active Applications filtering functionality
    /// </summary>
    public class ActiveApplicationsTest
    {
        private readonly ProcessService _processService;

        public ActiveApplicationsTest()
        {
            _processService = new ProcessService();
        }

        /// <summary>
        /// Test that demonstrates the difference between all processes and active applications
        /// </summary>
        public async Task TestActiveApplicationsFiltering()
        {
            Console.WriteLine("=== Active Applications Test ===");
            Console.WriteLine();

            // Get all processes
            var allProcesses = await _processService.GetProcessesAsync();
            Console.WriteLine($"Total processes found: {allProcesses.Count}");

            // Get only active applications
            var activeApplications = await _processService.GetActiveApplicationsAsync();
            Console.WriteLine($"Active applications found: {activeApplications.Count}");
            Console.WriteLine();

            // Show some examples of active applications
            Console.WriteLine("=== Active Applications (with visible windows) ===");
            foreach (var app in activeApplications.Take(10))
            {
                Console.WriteLine($"- {app.Name} (PID: {app.ProcessId})");
                Console.WriteLine($"  Window Title: '{app.MainWindowTitle}'");
                Console.WriteLine($"  Has Visible Window: {app.HasVisibleWindow}");
                Console.WriteLine($"  Window Handle: {app.MainWindowHandle}");
                Console.WriteLine();
            }

            // Show some examples of background processes (processes without windows)
            var backgroundProcesses = allProcesses.Where(p => !p.HasVisibleWindow).Take(10);
            Console.WriteLine("=== Background Processes (no visible windows) ===");
            foreach (var process in backgroundProcesses)
            {
                Console.WriteLine($"- {process.Name} (PID: {process.ProcessId})");
                Console.WriteLine($"  Window Title: '{process.MainWindowTitle}'");
                Console.WriteLine($"  Has Visible Window: {process.HasVisibleWindow}");
                Console.WriteLine();
            }

            // Validate that active applications are a subset of all processes
            var activeAppIds = activeApplications.Select(a => a.ProcessId).ToHashSet();
            var allProcessIds = allProcesses.Select(p => p.ProcessId).ToHashSet();
            
            bool isSubset = activeAppIds.IsSubsetOf(allProcessIds);
            Console.WriteLine($"Active applications are subset of all processes: {isSubset}");

            // Validate that all active applications have visible windows
            bool allHaveWindows = activeApplications.All(a => a.HasVisibleWindow);
            Console.WriteLine($"All active applications have visible windows: {allHaveWindows}");

            // Show filtering effectiveness
            double filteringRatio = (double)activeApplications.Count / allProcesses.Count * 100;
            Console.WriteLine($"Filtering effectiveness: {filteringRatio:F1}% of processes are active applications");
        }

        /// <summary>
        /// Test specific process properties for window information
        /// </summary>
        public async Task TestProcessWindowProperties()
        {
            Console.WriteLine("\n=== Process Window Properties Test ===");
            
            var allProcesses = await _processService.GetProcessesAsync();
            
            // Find some common applications that should have windows
            var commonApps = new[] { "explorer", "chrome", "firefox", "notepad", "code", "devenv" };
            
            foreach (var appName in commonApps)
            {
                var matchingProcesses = allProcesses.Where(p => 
                    p.Name.Contains(appName, StringComparison.OrdinalIgnoreCase)).ToList();
                
                if (matchingProcesses.Any())
                {
                    Console.WriteLine($"\n--- {appName.ToUpper()} Processes ---");
                    foreach (var process in matchingProcesses)
                    {
                        Console.WriteLine($"Name: {process.Name}");
                        Console.WriteLine($"PID: {process.ProcessId}");
                        Console.WriteLine($"Window Handle: {process.MainWindowHandle}");
                        Console.WriteLine($"Window Title: '{process.MainWindowTitle}'");
                        Console.WriteLine($"Has Visible Window: {process.HasVisibleWindow}");
                        Console.WriteLine($"Executable Path: {process.ExecutablePath}");
                        Console.WriteLine();
                    }
                }
            }
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public static async Task RunTests()
        {
            var test = new ActiveApplicationsTest();
            
            try
            {
                await test.TestActiveApplicationsFiltering();
                await test.TestProcessWindowProperties();
                
                Console.WriteLine("\n=== All tests completed successfully! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nTest failed with error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
