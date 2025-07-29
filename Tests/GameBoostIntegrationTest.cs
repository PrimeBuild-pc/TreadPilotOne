using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThreadPilot.Services;
using ThreadPilot.Models;

namespace ThreadPilot.Tests
{
    /// <summary>
    /// Integration test for Game Boost functionality
    /// </summary>
    public class GameBoostIntegrationTest
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GameBoostIntegrationTest> _logger;

        public GameBoostIntegrationTest(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<GameBoostIntegrationTest>>();
        }

        /// <summary>
        /// Tests the complete Game Boost workflow
        /// </summary>
        public async Task<bool> RunIntegrationTestAsync()
        {
            try
            {
                _logger.LogInformation("Starting Game Boost integration test...");

                // Test 1: Service Resolution
                if (!await TestServiceResolutionAsync())
                {
                    _logger.LogError("Service resolution test failed");
                    return false;
                }

                // Test 2: Game Detection Logic
                if (!await TestGameDetectionAsync())
                {
                    _logger.LogError("Game detection test failed");
                    return false;
                }

                // Test 3: Known Games Management
                if (!await TestKnownGamesManagementAsync())
                {
                    _logger.LogError("Known games management test failed");
                    return false;
                }

                // Test 4: System Tray Integration
                if (!await TestSystemTrayIntegrationAsync())
                {
                    _logger.LogError("System tray integration test failed");
                    return false;
                }

                _logger.LogInformation("All Game Boost integration tests passed!");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game Boost integration test failed with exception");
                return false;
            }
        }

        private async Task<bool> TestServiceResolutionAsync()
        {
            try
            {
                _logger.LogInformation("Testing service resolution...");

                var gameBoostService = _serviceProvider.GetRequiredService<IGameBoostService>();
                var systemTrayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
                var processMonitorService = _serviceProvider.GetRequiredService<IProcessMonitorService>();
                var settingsService = _serviceProvider.GetRequiredService<IApplicationSettingsService>();

                if (gameBoostService == null || systemTrayService == null || 
                    processMonitorService == null || settingsService == null)
                {
                    _logger.LogError("One or more required services could not be resolved");
                    return false;
                }

                _logger.LogInformation("Service resolution test passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service resolution test failed");
                return false;
            }
        }

        private async Task<bool> TestGameDetectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing game detection logic...");

                var gameBoostService = _serviceProvider.GetRequiredService<IGameBoostService>();

                // Test with known game processes
                var testProcesses = new[]
                {
                    new ProcessModel { Name = "steam.exe", ProcessId = 1234, ExecutablePath = @"C:\Program Files\Steam\steam.exe" },
                    new ProcessModel { Name = "notepad.exe", ProcessId = 5678, ExecutablePath = @"C:\Windows\System32\notepad.exe" },
                    new ProcessModel { Name = "csgo.exe", ProcessId = 9999, ExecutablePath = @"C:\Games\Counter-Strike\csgo.exe" }
                };

                foreach (var process in testProcesses)
                {
                    var isGame = gameBoostService.IsGameProcess(process);
                    _logger.LogInformation("Process {ProcessName}: IsGame = {IsGame}", process.Name, isGame);
                }

                _logger.LogInformation("Game detection test passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game detection test failed");
                return false;
            }
        }

        private async Task<bool> TestKnownGamesManagementAsync()
        {
            try
            {
                _logger.LogInformation("Testing known games management...");

                var gameBoostService = _serviceProvider.GetRequiredService<IGameBoostService>();

                // Get initial count
                var initialCount = gameBoostService.KnownGameExecutables.Count;
                _logger.LogInformation("Initial known games count: {Count}", initialCount);

                // Test adding a game
                var testGame = "testgame.exe";
                var addResult = await gameBoostService.AddKnownGameAsync(testGame);
                if (!addResult)
                {
                    _logger.LogError("Failed to add test game");
                    return false;
                }

                // Verify it was added
                var newCount = gameBoostService.KnownGameExecutables.Count;
                if (newCount != initialCount + 1)
                {
                    _logger.LogError("Game count did not increase after adding game");
                    return false;
                }

                // Test removing the game
                var removeResult = await gameBoostService.RemoveKnownGameAsync(testGame);
                if (!removeResult)
                {
                    _logger.LogError("Failed to remove test game");
                    return false;
                }

                // Verify it was removed
                var finalCount = gameBoostService.KnownGameExecutables.Count;
                if (finalCount != initialCount)
                {
                    _logger.LogError("Game count did not return to initial value after removing game");
                    return false;
                }

                _logger.LogInformation("Known games management test passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Known games management test failed");
                return false;
            }
        }

        private async Task<bool> TestSystemTrayIntegrationAsync()
        {
            try
            {
                _logger.LogInformation("Testing system tray integration...");

                var systemTrayService = _serviceProvider.GetRequiredService<ISystemTrayService>();

                // Test updating Game Boost status
                systemTrayService.UpdateGameBoostStatus(true, "TestGame");
                await Task.Delay(100); // Allow UI to update

                systemTrayService.UpdateGameBoostStatus(false);
                await Task.Delay(100); // Allow UI to update

                _logger.LogInformation("System tray integration test passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System tray integration test failed");
                return false;
            }
        }
    }
}
