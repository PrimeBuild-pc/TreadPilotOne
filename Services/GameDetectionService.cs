using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for detecting running games and applying optimal performance profiles
    /// </summary>
    public class GameDetectionService : IGameDetectionService
    {
        private readonly ILogger<GameDetectionService> _logger;
        private readonly IProcessService _processService;
        private readonly ICpuTopologyService _cpuTopologyService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly Dictionary<string, GameProfile> _gameProfiles;
        private readonly Dictionary<string, DateTime> _gameStartTimes;

        // ADVANCED GAME DETECTION: ML and performance monitoring
        private readonly Dictionary<string, bool> _gameOverrides = new();
        private readonly Dictionary<int, GamePerformanceMetrics> _monitoredGames = new();
        private readonly List<string> _gameKeywords = new()
        {
            "game", "gaming", "play", "steam", "epic", "origin", "uplay", "battle.net",
            "launcher", "client", "engine", "unity", "unreal", "directx", "opengl", "vulkan"
        };
        private readonly List<string> _gamesFolders = new()
        {
            "games", "steam", "steamapps", "epic games", "origin games", "uplay", "battle.net"
        };

        public event EventHandler<GameProfileDetectedEventArgs>? GameDetected;
        public event EventHandler<GameStoppedEventArgs>? GameStopped;

        public GameDetectionService(
            ILogger<GameDetectionService> logger,
            IProcessService processService,
            ICpuTopologyService cpuTopologyService,
            IPowerPlanService powerPlanService)
        {
            _logger = logger;
            _processService = processService;
            _cpuTopologyService = cpuTopologyService;
            _powerPlanService = powerPlanService;
            _gameProfiles = new Dictionary<string, GameProfile>();
            _gameStartTimes = new Dictionary<string, DateTime>();

            InitializeDefaultGameProfiles();
        }

        public async Task<ProcessFeatures> ExtractProcessFeaturesAsync(ProcessModel process)
        {
            var features = new ProcessFeatures
            {
                ProcessName = process.Name,
                ExecutablePath = process.ExecutablePath ?? string.Empty,
                HasVisibleWindow = process.HasVisibleWindow,
                CpuUsage = process.CpuUsage,
                MemoryUsage = process.MemoryUsage,
                ThreadCount = process.Process?.Threads.Count ?? 0,
                HandleCount = process.Process?.HandleCount ?? 0
            };

            try
            {
                // Check for graphics API DLLs
                features.HasDirectXDlls = await HasLoadedDllAsync(process, "d3d", "dxgi", "d3d11", "d3d12");
                features.HasOpenGLDlls = await HasLoadedDllAsync(process, "opengl32", "glu32");
                features.HasVulkanDlls = await HasLoadedDllAsync(process, "vulkan");
                features.HasAudioDlls = await HasLoadedDllAsync(process, "dsound", "xaudio", "fmod");

                // Check file properties
                if (!string.IsNullOrEmpty(features.ExecutablePath) && File.Exists(features.ExecutablePath))
                {
                    var fileInfo = FileVersionInfo.GetVersionInfo(features.ExecutablePath);
                    features.FileDescription = fileInfo.FileDescription ?? string.Empty;
                    features.CompanyName = fileInfo.CompanyName ?? string.Empty;
                }

                // Check if in games folder
                features.IsInGamesFolder = _gamesFolders.Any(folder =>
                    features.ExecutablePath.Contains(folder, StringComparison.OrdinalIgnoreCase));

                // Check for game keywords
                features.HasGameKeywords = _gameKeywords.Any(keyword =>
                    features.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    features.FileDescription.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                // Check if fullscreen (simplified check)
                features.IsFullscreen = process.HasVisibleWindow && process.MainWindowTitle != null;

                _logger.LogDebug("Extracted features for {ProcessName}: DirectX={HasDirectX}, OpenGL={HasOpenGL}, GamesFolder={IsInGamesFolder}",
                    process.Name, features.HasDirectXDlls, features.HasOpenGLDlls, features.IsInGamesFolder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting features for process {ProcessName}", process.Name);
            }

            return features;
        }

        public async Task<GamePerformanceMetrics> GetGamePerformanceAsync(ProcessModel process)
        {
            var metrics = new GamePerformanceMetrics
            {
                ProcessId = process.ProcessId,
                GameName = process.Name,
                CpuUsage = process.CpuUsage,
                MemoryUsage = process.MemoryUsage,
                Timestamp = DateTime.UtcNow,
                IsFullscreen = process.HasVisibleWindow
            };

            try
            {
                // Estimate frame rate based on CPU usage patterns (simplified)
                metrics.FrameRate = EstimateFrameRate(process);

                // Get GPU usage (would require additional APIs in real implementation)
                metrics.GpuUsage = 0.0; // Placeholder
                metrics.GpuMemoryUsage = 0; // Placeholder

                // Get window resolution (simplified)
                metrics.Resolution = process.HasVisibleWindow ? "1920x1080" : "N/A"; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting performance metrics for {ProcessName}", process.Name);
            }

            return metrics;
        }

        public async Task<GameDetectionResult> DetectGameWithMLAsync(ProcessModel process)
        {
            try
            {
                // Check manual overrides first
                if (_gameOverrides.TryGetValue(process.Name.ToLower(), out var isGameOverride))
                {
                    return new GameDetectionResult
                    {
                        IsGame = isGameOverride,
                        Confidence = 1.0f,
                        GameName = process.Name,
                        DetectionMethod = "Manual Override",
                        DetectionTime = DateTime.UtcNow
                    };
                }

                // Extract features for ML classification
                var features = await ExtractProcessFeaturesAsync(process);

                // Simple ML-like scoring based on features (can be replaced with actual ML model)
                var score = CalculateGameScore(features);

                var result = new GameDetectionResult
                {
                    IsGame = score >= 0.5f,
                    Confidence = score,
                    GameName = features.ProcessName,
                    DetectionMethod = "ML Classification",
                    Features = ConvertFeaturesToDictionary(features),
                    DetectionTime = DateTime.UtcNow
                };

                _logger.LogDebug("ML game detection for {ProcessName}: IsGame={IsGame}, Confidence={Confidence:P1}",
                    process.Name, result.IsGame, result.Confidence);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ML game detection for process {ProcessName}", process.Name);
                return new GameDetectionResult
                {
                    IsGame = false,
                    Confidence = 0.0f,
                    GameName = process.Name,
                    DetectionMethod = "Error",
                    DetectionTime = DateTime.UtcNow
                };
            }
        }

        public async Task<GameProfile?> DetectGameAsync(ProcessModel process)
        {
            try
            {
                // Check known games database first
                if (_gameProfiles.TryGetValue(process.Name.ToLower(), out var profile))
                {
                    profile.LastDetected = DateTime.UtcNow;
                    profile.DetectionCount++;
                    
                    // Track game start time
                    if (!_gameStartTimes.ContainsKey(process.Name))
                    {
                        _gameStartTimes[process.Name] = DateTime.UtcNow;
                        GameDetected?.Invoke(this, new GameProfileDetectedEventArgs(process, profile));
                    }
                    
                    return profile;
                }

                // Check Steam games
                if (await IsSteamGameAsync(process))
                {
                    var steamProfile = await GetSteamGameProfileAsync(process);
                    if (steamProfile != null)
                    {
                        return steamProfile;
                    }
                }

                // Check Epic Games
                if (await IsEpicGameAsync(process))
                {
                    var epicProfile = await GetEpicGameProfileAsync(process);
                    if (epicProfile != null)
                    {
                        return epicProfile;
                    }
                }

                // Check for common game patterns
                if (IsLikelyGame(process))
                {
                    return CreateGenericGameProfile(process);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting game for process {ProcessName}", process.Name);
                return null;
            }
        }

        public async Task<List<GameProfile>> GetRunningGamesAsync()
        {
            var runningGames = new List<GameProfile>();
            var processes = await _processService.GetActiveApplicationsAsync();

            foreach (var process in processes)
            {
                var gameProfile = await DetectGameAsync(process);
                if (gameProfile != null)
                {
                    runningGames.Add(gameProfile);
                }
            }

            return runningGames;
        }

        public async Task<bool> ApplyGameOptimizationsAsync(ProcessModel process, GameProfile gameProfile)
        {
            try
            {
                _logger.LogInformation("Applying optimizations for game {GameName} (Process: {ProcessName})", 
                    gameProfile.Name, process.Name);

                var success = true;

                // Apply CPU affinity
                if (!string.IsNullOrEmpty(gameProfile.OptimalCores))
                {
                    var affinityMask = await CalculateOptimalAffinityMask(gameProfile.OptimalCores);
                    if (affinityMask.HasValue)
                    {
                        await _processService.SetProcessorAffinity(process, (long)affinityMask.Value);
                    }
                }

                // Apply process priority
                await _processService.SetProcessPriority(process, gameProfile.Priority);

                // Apply power plan if specified
                if (!string.IsNullOrEmpty(gameProfile.PowerPlan))
                {
                    var powerPlans = await _powerPlanService.GetPowerPlansAsync();
                    var targetPlan = powerPlans.FirstOrDefault(p => 
                        p.Name.Contains(gameProfile.PowerPlan, StringComparison.OrdinalIgnoreCase));
                    
                    if (targetPlan != null)
                    {
                        success &= await _powerPlanService.SetActivePowerPlan(targetPlan);
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying game optimizations for {ProcessName}", process.Name);
                return false;
            }
        }

        public async Task<bool> IsSteamGameAsync(ProcessModel process)
        {
            try
            {
                // Check if process is launched by Steam
                var parentProcess = GetParentProcess(process.ProcessId);
                if (parentProcess?.ProcessName?.Contains("steam", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }

                // Check Steam installation directory
                var steamPath = GetSteamInstallPath();
                if (!string.IsNullOrEmpty(steamPath) && !string.IsNullOrEmpty(process.ExecutablePath))
                {
                    return process.ExecutablePath.StartsWith(steamPath, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if process is Steam game: {ProcessName}", process.Name);
                return false;
            }
        }

        public async Task<bool> IsEpicGameAsync(ProcessModel process)
        {
            try
            {
                // Check if process is launched by Epic Games Launcher
                var parentProcess = GetParentProcess(process.ProcessId);
                if (parentProcess?.ProcessName?.Contains("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase) == true ||
                    parentProcess?.ProcessName?.Contains("UnrealEngineLauncher", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }

                // Check Epic Games installation directory
                var epicPath = GetEpicGamesInstallPath();
                if (!string.IsNullOrEmpty(epicPath) && !string.IsNullOrEmpty(process.ExecutablePath))
                {
                    return process.ExecutablePath.StartsWith(epicPath, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if process is Epic game: {ProcessName}", process.Name);
                return false;
            }
        }

        public async Task<GameProfile?> GetSteamGameProfileAsync(ProcessModel process)
        {
            // Create a Steam-specific game profile
            return new GameProfile
            {
                Name = $"Steam: {process.Name}",
                ProcessName = process.Name,
                OptimalCores = "Physical", // Steam games generally benefit from physical cores
                Priority = ProcessPriorityClass.High,
                PowerPlan = "High Performance",
                Category = GameCategory.Unknown,
                Description = "Steam game detected automatically"
            };
        }

        public async Task<GameProfile?> GetEpicGameProfileAsync(ProcessModel process)
        {
            // Create an Epic-specific game profile
            return new GameProfile
            {
                Name = $"Epic: {process.Name}",
                ProcessName = process.Name,
                OptimalCores = "Physical", // Epic games generally benefit from physical cores
                Priority = ProcessPriorityClass.High,
                PowerPlan = "High Performance",
                Category = GameCategory.Unknown,
                Description = "Epic Games game detected automatically"
            };
        }

        public async Task AddCustomGameProfileAsync(string processName, GameProfile profile)
        {
            _gameProfiles[processName.ToLower()] = profile;
            _logger.LogInformation("Added custom game profile for {ProcessName}", processName);
        }

        public async Task RemoveCustomGameProfileAsync(string processName)
        {
            if (_gameProfiles.Remove(processName.ToLower()))
            {
                _logger.LogInformation("Removed custom game profile for {ProcessName}", processName);
            }
        }

        public async Task<Dictionary<string, GameProfile>> GetAllGameProfilesAsync()
        {
            return new Dictionary<string, GameProfile>(_gameProfiles);
        }

        private void InitializeDefaultGameProfiles()
        {
            // Popular FPS games
            _gameProfiles["valorant.exe"] = new GameProfile
            {
                Name = "Valorant",
                ProcessName = "valorant.exe",
                OptimalCores = "Physical",
                Priority = ProcessPriorityClass.High,
                PowerPlan = "High Performance",
                Category = GameCategory.FPS,
                Description = "Riot Games' tactical FPS"
            };

            _gameProfiles["csgo.exe"] = new GameProfile
            {
                Name = "Counter-Strike: Global Offensive",
                ProcessName = "csgo.exe",
                OptimalCores = "Physical",
                Priority = ProcessPriorityClass.High,
                PowerPlan = "High Performance",
                Category = GameCategory.FPS
            };

            _gameProfiles["cs2.exe"] = new GameProfile
            {
                Name = "Counter-Strike 2",
                ProcessName = "cs2.exe",
                OptimalCores = "P-Cores",
                Priority = ProcessPriorityClass.High,
                PowerPlan = "High Performance",
                Category = GameCategory.FPS
            };

            _gameProfiles["cyberpunk2077.exe"] = new GameProfile
            {
                Name = "Cyberpunk 2077",
                ProcessName = "cyberpunk2077.exe",
                OptimalCores = "P-Cores",
                Priority = ProcessPriorityClass.High,
                PowerPlan = "High Performance",
                Category = GameCategory.RPG
            };

            _gameProfiles["fortnite.exe"] = new GameProfile
            {
                Name = "Fortnite",
                ProcessName = "fortnite.exe",
                OptimalCores = "Physical",
                Priority = ProcessPriorityClass.High,
                PowerPlan = "High Performance",
                Category = GameCategory.FPS
            };

            _gameProfiles["league of legends.exe"] = new GameProfile
            {
                Name = "League of Legends",
                ProcessName = "league of legends.exe",
                OptimalCores = "Physical",
                Priority = ProcessPriorityClass.AboveNormal,
                PowerPlan = "High Performance",
                Category = GameCategory.MOBA
            };
        }

        private async Task<IntPtr?> CalculateOptimalAffinityMask(string optimalCores)
        {
            try
            {
                var topology = await _cpuTopologyService.DetectTopologyAsync();
                if (topology == null) return null;

                return optimalCores switch
                {
                    "Physical" => new IntPtr(topology.GetPhysicalCoresAffinityMask()),
                    "P-Cores" => new IntPtr(topology.GetPerformanceCoresAffinityMask()),
                    "E-Cores" => new IntPtr(topology.GetEfficiencyCoresAffinityMask()),
                    "All" => new IntPtr(topology.CalculateAffinityMask(topology.LogicalCores)),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating affinity mask for {OptimalCores}", optimalCores);
                return null;
            }
        }

        private float CalculateGameScore(ProcessFeatures features)
        {
            float score = 0.0f;

            // Graphics API indicators (strong indicators)
            if (features.HasDirectXDlls) score += 0.3f;
            if (features.HasOpenGLDlls) score += 0.25f;
            if (features.HasVulkanDlls) score += 0.3f;

            // Audio indicators
            if (features.HasAudioDlls) score += 0.1f;

            // Location indicators
            if (features.IsInGamesFolder) score += 0.2f;

            // Keyword indicators
            if (features.HasGameKeywords) score += 0.15f;

            // Window and resource usage indicators
            if (features.HasVisibleWindow) score += 0.1f;
            if (features.IsFullscreen) score += 0.15f;
            if (features.CpuUsage > 10.0) score += 0.1f;
            if (features.MemoryUsage > 100 * 1024 * 1024) score += 0.05f; // > 100MB

            // Company indicators
            var gameCompanies = new[] { "valve", "epic", "ubisoft", "ea", "activision", "blizzard", "steam" };
            if (gameCompanies.Any(company => features.CompanyName.Contains(company, StringComparison.OrdinalIgnoreCase)))
                score += 0.1f;

            // Clamp score to [0, 1]
            return Math.Min(1.0f, Math.Max(0.0f, score));
        }

        private Dictionary<string, object> ConvertFeaturesToDictionary(ProcessFeatures features)
        {
            return new Dictionary<string, object>
            {
                ["ProcessName"] = features.ProcessName,
                ["HasDirectXDlls"] = features.HasDirectXDlls,
                ["HasOpenGLDlls"] = features.HasOpenGLDlls,
                ["HasVulkanDlls"] = features.HasVulkanDlls,
                ["HasAudioDlls"] = features.HasAudioDlls,
                ["IsInGamesFolder"] = features.IsInGamesFolder,
                ["HasGameKeywords"] = features.HasGameKeywords,
                ["HasVisibleWindow"] = features.HasVisibleWindow,
                ["IsFullscreen"] = features.IsFullscreen,
                ["CpuUsage"] = features.CpuUsage,
                ["MemoryUsage"] = features.MemoryUsage,
                ["CompanyName"] = features.CompanyName
            };
        }

        private async Task<bool> HasLoadedDllAsync(ProcessModel process, params string[] dllNames)
        {
            try
            {
                // Simplified check - in real implementation would check loaded modules
                // For now, check if executable path contains any of the DLL indicators
                var execPath = process.ExecutablePath?.ToLower() ?? string.Empty;
                return dllNames.Any(dll => execPath.Contains(dll.ToLower()));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking DLLs for process {ProcessName}", process.Name);
                return false;
            }
        }

        private float EstimateFrameRate(ProcessModel process)
        {
            // Simplified frame rate estimation based on CPU usage patterns
            // In real implementation, would use performance counters or graphics APIs
            if (process.CpuUsage > 20.0)
                return 60.0f; // Assume 60 FPS for high CPU usage games
            else if (process.CpuUsage > 10.0)
                return 30.0f; // Assume 30 FPS for moderate CPU usage
            else
                return 0.0f; // Not actively rendering
        }

        private static bool IsLikelyGame(ProcessModel process)
        {
            var gameIndicators = new[]
            {
                "game", "launcher", "client", "engine", "unity", "unreal",
                "dx11", "dx12", "vulkan", "opengl"
            };

            var processName = process.Name.ToLower();
            var executablePath = process.ExecutablePath?.ToLower() ?? "";

            return gameIndicators.Any(indicator =>
                processName.Contains(indicator) || executablePath.Contains(indicator)) ||
                   process.HasVisibleWindow && process.CpuUsage > 5.0; // High CPU with window
        }

        private GameProfile CreateGenericGameProfile(ProcessModel process)
        {
            return new GameProfile
            {
                Name = $"Generic Game: {process.Name}",
                ProcessName = process.Name,
                OptimalCores = "Physical",
                Priority = ProcessPriorityClass.AboveNormal,
                PowerPlan = "Balanced",
                Category = GameCategory.Unknown,
                Description = "Automatically detected game"
            };
        }

        private static Process? GetParentProcess(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var parentId = GetParentProcessId(processId);
                return parentId > 0 ? Process.GetProcessById(parentId) : null;
            }
            catch
            {
                return null;
            }
        }

        private static int GetParentProcessId(int processId)
        {
            // Implementation would use WMI or P/Invoke to get parent process ID
            // Simplified for now
            return 0;
        }

        private static string? GetSteamInstallPath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") ??
                               Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                return key?.GetValue("InstallPath")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string? GetEpicGamesInstallPath()
        {
            try
            {
                var epicPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher");
                return Directory.Exists(epicPath) ? epicPath : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
