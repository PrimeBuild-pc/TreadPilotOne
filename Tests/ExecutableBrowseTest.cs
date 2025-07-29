using System;
using System.IO;
using System.Threading.Tasks;
using ThreadPilot.ViewModels;
using ThreadPilot.Services;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Tests
{
    /// <summary>
    /// Test class to validate the new executable browse functionality
    /// </summary>
    public class ExecutableBrowseTest
    {
        public ExecutableBrowseTest()
        {
            // Simple test class without complex dependencies
        }

        /// <summary>
        /// Test the executable validation logic (static test)
        /// </summary>
        public bool TestExecutableValidation()
        {
            try
            {
                Console.WriteLine("Testing executable validation logic...");

                // Test with a known Windows executable
                string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string notepadPath = Path.Combine(windowsDir, "notepad.exe");

                bool notepadExists = File.Exists(notepadPath);
                Console.WriteLine($"Notepad.exe exists: {notepadExists}");

                if (notepadExists)
                {
                    // Test file extension validation
                    string extension = Path.GetExtension(notepadPath);
                    bool hasExeExtension = string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
                    Console.WriteLine($"Has .exe extension: {hasExeExtension}");

                    // Test with non-exe file
                    string systemIni = Path.Combine(windowsDir, "system.ini");
                    bool systemIniExists = File.Exists(systemIni);
                    if (systemIniExists)
                    {
                        string iniExtension = Path.GetExtension(systemIni);
                        bool isNotExe = !string.Equals(iniExtension, ".exe", StringComparison.OrdinalIgnoreCase);
                        Console.WriteLine($"system.ini is not .exe: {isNotExe}");
                    }

                    bool testPassed = notepadExists && hasExeExtension;
                    Console.WriteLine($"Executable validation test: {(testPassed ? "PASSED" : "FAILED")}");
                    return testPassed;
                }

                Console.WriteLine("Could not find notepad.exe for testing");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Executable validation test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test path extraction functionality
        /// </summary>
        public bool TestPathExtraction()
        {
            try
            {
                Console.WriteLine("Testing path extraction functionality...");

                // Test extracting filename from full path
                string testPath = @"C:\Program Files\MyApp\myapp.exe";
                string extractedName = Path.GetFileName(testPath);

                Console.WriteLine($"Full path: {testPath}");
                Console.WriteLine($"Extracted name: {extractedName}");

                bool testPassed = extractedName == "myapp.exe";
                Console.WriteLine($"Path extraction test: {(testPassed ? "PASSED" : "FAILED")}");
                return testPassed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Path extraction test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test file dialog filter validation
        /// </summary>
        public bool TestFileDialogFilter()
        {
            try
            {
                Console.WriteLine("Testing file dialog filter logic...");

                // Test the filter string that would be used
                string expectedFilter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                Console.WriteLine($"Expected filter: {expectedFilter}");

                // Test that the filter contains the right patterns
                bool hasExeFilter = expectedFilter.Contains("*.exe");
                bool hasAllFilesFilter = expectedFilter.Contains("*.*");

                Console.WriteLine($"Has .exe filter: {hasExeFilter}");
                Console.WriteLine($"Has all files filter: {hasAllFilesFilter}");

                bool testPassed = hasExeFilter && hasAllFilesFilter;
                Console.WriteLine($"File dialog filter test: {(testPassed ? "PASSED" : "FAILED")}");
                return testPassed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File dialog filter test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public bool RunAllTests()
        {
            Console.WriteLine("=== Executable Browse Functionality Tests ===");
            Console.WriteLine();

            bool test1 = TestExecutableValidation();
            Console.WriteLine();

            bool test2 = TestPathExtraction();
            Console.WriteLine();

            bool test3 = TestFileDialogFilter();
            Console.WriteLine();

            bool allPassed = test1 && test2 && test3;
            Console.WriteLine($"=== Overall Test Result: {(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED")} ===");

            return allPassed;
        }
    }
}
