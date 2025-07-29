using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing Game Boost mode functionality
    /// </summary>
    public class GameBoostService : IGameBoostService
    {
        private readonly ILogger<GameBoostService> _logger;
        private readonly IPowerPlanService _powerPlanService;
        private readonly IProcessService _processService;
        private readonly INotificationService _notificationService;
        private readonly IApplicationSettingsService _settingsService;

        private ApplicationSettingsModel _settings;
        private bool _isGameBoostActive;
        private ProcessModel? _currentGameProcess;
        private string? _previousPowerPlanId;
        private DateTime? _gameBoostStartTime;
        private readonly List<string> _knownGameExecutables;

        public event EventHandler<GameBoostActivatedEventArgs>? GameBoostActivated;
        public event EventHandler<GameBoostDeactivatedEventArgs>? GameBoostDeactivated;
        public event EventHandler<GameDetectedEventArgs>? GameDetected;

        public bool IsGameBoostActive => _isGameBoostActive;
        public ProcessModel? CurrentGameProcess => _currentGameProcess;
        public IReadOnlyList<string> KnownGameExecutables => _knownGameExecutables.AsReadOnly();

        public GameBoostService(
            ILogger<GameBoostService> logger,
            IPowerPlanService powerPlanService,
            IProcessService processService,
            INotificationService notificationService,
            IApplicationSettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            _settings = _settingsService.Settings;
            _knownGameExecutables = InitializeKnownGames();

            // Subscribe to settings changes
            _settingsService.SettingsChanged += OnSettingsChanged;

            _logger.LogInformation("Game Boost service initialized with {Count} known games", _knownGameExecutables.Count);
        }

        public async Task<bool> EnableGameBoostAsync()
        {
            try
            {
                if (!_settings.EnableGameBoostMode)
                {
                    _logger.LogWarning("Game Boost mode is disabled in settings");
                    return false;
                }

                _logger.LogInformation("Game Boost mode enabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable Game Boost mode");
                return false;
            }
        }

        public async Task<bool> DisableGameBoostAsync()
        {
            try
            {
                if (_isGameBoostActive)
                {
                    await DeactivateGameBoostAsync();
                }

                _logger.LogInformation("Game Boost mode disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable Game Boost mode");
                return false;
            }
        }

        public async Task<bool> ActivateGameBoostAsync(ProcessModel process)
        {
            try
            {
                if (_isGameBoostActive && _currentGameProcess?.ProcessId == process.ProcessId)
                {
                    _logger.LogDebug("Game Boost already active for process {ProcessName}", process.Name);
                    return true;
                }

                // Deactivate current boost if active
                if (_isGameBoostActive)
                {
                    await DeactivateGameBoostAsync();
                }

                // Store current power plan
                var currentPowerPlan = await _powerPlanService.GetActivePowerPlan();
                _previousPowerPlanId = currentPowerPlan?.Guid;

                // Apply Game Boost power plan
                var gameBoostPowerPlanId = !string.IsNullOrEmpty(_settings.GameBoostPowerPlanId) 
                    ? _settings.GameBoostPowerPlanId 
                    : await GetHighPerformancePowerPlanIdAsync();

                if (!string.IsNullOrEmpty(gameBoostPowerPlanId))
                {
                    await _powerPlanService.SetActivePowerPlanByGuidAsync(gameBoostPowerPlanId);
                }

                // Set high priority if enabled
                if (_settings.GameBoostSetHighPriority)
                {
                    await SetProcessPriorityAsync(process, ProcessPriorityClass.High);
                }

                // Optimize CPU affinity if enabled
                if (_settings.GameBoostOptimizeCpuAffinity)
                {
                    await OptimizeCpuAffinityAsync(process);
                }

                _isGameBoostActive = true;
                _currentGameProcess = process;
                _gameBoostStartTime = DateTime.Now;

                _logger.LogInformation("Game Boost activated for {ProcessName} (PID: {ProcessId})",
                    process.Name, process.ProcessId);

                // Fire events
                GameDetected?.Invoke(this, new GameDetectedEventArgs(process, _knownGameExecutables.Contains(process.Name.ToLowerInvariant())));
                GameBoostActivated?.Invoke(this, new GameBoostActivatedEventArgs(process, gameBoostPowerPlanId ?? ""));

                await _notificationService.ShowSuccessNotificationAsync(
                    "Game Boost Activated",
                    $"Game Boost mode activated for {process.Name}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate Game Boost for process {ProcessName}", process.Name);
                return false;
            }
        }

        public async Task<bool> DeactivateGameBoostAsync()
        {
            try
            {
                if (!_isGameBoostActive)
                {
                    return true;
                }

                var duration = _gameBoostStartTime.HasValue 
                    ? DateTime.Now - _gameBoostStartTime.Value 
                    : TimeSpan.Zero;

                // Restore previous power plan
                if (!string.IsNullOrEmpty(_previousPowerPlanId))
                {
                    await _powerPlanService.SetActivePowerPlanByGuidAsync(_previousPowerPlanId);
                }
                else if (!string.IsNullOrEmpty(_settings.DefaultPowerPlanId))
                {
                    await _powerPlanService.SetActivePowerPlanByGuidAsync(_settings.DefaultPowerPlanId);
                }

                var gameProcess = _currentGameProcess;
                var restoredPowerPlanId = _previousPowerPlanId ?? _settings.DefaultPowerPlanId;

                _isGameBoostActive = false;
                _currentGameProcess = null;
                _previousPowerPlanId = null;
                _gameBoostStartTime = null;

                _logger.LogInformation("Game Boost deactivated after {Duration}", duration);

                GameBoostDeactivated?.Invoke(this, new GameBoostDeactivatedEventArgs(
                    gameProcess, restoredPowerPlanId, duration));

                await _notificationService.ShowNotificationAsync(
                    "Game Boost Deactivated",
                    $"Game Boost mode deactivated after {duration:hh\\:mm\\:ss}",
                    NotificationType.Information);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate Game Boost");
                return false;
            }
        }

        public async Task<bool> AddKnownGameAsync(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName))
                return false;

            var normalizedName = executableName.ToLowerInvariant();
            if (!_knownGameExecutables.Contains(normalizedName))
            {
                _knownGameExecutables.Add(normalizedName);
                _logger.LogInformation("Added known game: {ExecutableName}", executableName);
                return true;
            }

            return false;
        }

        public async Task<bool> RemoveKnownGameAsync(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName))
                return false;

            var normalizedName = executableName.ToLowerInvariant();
            var removed = _knownGameExecutables.Remove(normalizedName);
            
            if (removed)
            {
                _logger.LogInformation("Removed known game: {ExecutableName}", executableName);
            }

            return removed;
        }

        public IReadOnlyList<string> GetKnownGameExecutables()
        {
            return _knownGameExecutables.ToList().AsReadOnly();
        }

        public bool IsGameProcess(ProcessModel process)
        {
            if (process == null || string.IsNullOrEmpty(process.Name))
                return false;

            var processName = process.Name.ToLowerInvariant();

            // Check against known games list
            if (_knownGameExecutables.Contains(processName))
                return true;

            // Auto-detection heuristics (if enabled)
            if (_settings.GameBoostAutoDetectGames)
            {
                return IsLikelyGameProcess(process);
            }

            return false;
        }

        private void OnSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            try
            {
                _settings = e.NewSettings;
                _logger.LogDebug("Game Boost settings updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Game Boost settings");
            }
        }

        private List<string> InitializeKnownGames()
        {
            return new List<string>
            {
                // Game Launchers
                "steam.exe",
                "steamwebhelper.exe",
                "origin.exe",
                "epicgameslauncher.exe",
                "uplay.exe",
                "ubisoft connect.exe",
                "battlenet.exe",
                "battle.net.exe",
                "gog.exe",
                "gog galaxy.exe",
                "rockstarlauncher.exe",
                "bethesdanetlauncher.exe",
                "ea desktop.exe",
                "xbox.exe",
                "xboxapp.exe",
                "gamepass.exe",

                // Popular Games - FPS/Shooters
                "csgo.exe",
                "cs2.exe",
                "valorant.exe",
                "valorant-win64-shipping.exe",
                "fortniteclient-win64-shipping.exe",
                "fortnite.exe",
                "apex_legends.exe",
                "r5apex.exe",
                "overwatch.exe",
                "overwatch2.exe",
                "cod.exe",
                "modernwarfare.exe",
                "warzone.exe",
                "blackops.exe",
                "rainbow6.exe",
                "rainbowsix.exe",
                "pubg.exe",
                "tslgame.exe",
                "bf1.exe",
                "bfv.exe",
                "bf2042.exe",
                "titanfall2.exe",
                "doom.exe",
                "doomslayers.exe",
                "doom eternal.exe",
                "halo.exe",
                "haloinfinite.exe",
                "destiny2.exe",

                // Popular Games - MOBA/Strategy
                "dota2.exe",
                "league of legends.exe",
                "lol.exe",
                "riotclientservices.exe",
                "teamfighttactics.exe",
                "starcraft2.exe",
                "sc2.exe",
                "warcraft3.exe",
                "aoe2de.exe",
                "aoe4.exe",
                "civilization6.exe",
                "civ6.exe",
                "totalwar.exe",

                // Popular Games - RPG/Adventure
                "witcher3.exe",
                "cyberpunk2077.exe",
                "skyrim.exe",
                "skyrimse.exe",
                "fallout4.exe",
                "fallout76.exe",
                "elderscrollsonline.exe",
                "wow.exe",
                "worldofwarcraft.exe",
                "ffxiv.exe",
                "ffxiv_dx11.exe",
                "guildwars2.exe",
                "newworld.exe",
                "lostark.exe",
                "diablo3.exe",
                "diablo4.exe",
                "pathofexile.exe",
                "borderlands3.exe",
                "masseffect.exe",
                "dragonage.exe",
                "assassinscreed.exe",
                "farcry.exe",
                "watchdogs.exe",

                // Popular Games - Open World/Action
                "gta5.exe",
                "gtav.exe",
                "rdr2.exe",
                "reddeadredemption2.exe",
                "minecraft.exe",
                "minecraftlauncher.exe",
                "javaw.exe", // Minecraft Java
                "terraria.exe",
                "stardewvalley.exe",
                "subnautica.exe",
                "nomanssky.exe",
                "spiderman.exe",
                "godofwar.exe",
                "horizonzerodawn.exe",
                "deathstranding.exe",

                // Popular Games - Racing/Sports
                "forza.exe",
                "forzahorizon.exe",
                "granturismo.exe",
                "f1.exe",
                "dirt.exe",
                "wreckfest.exe",
                "fifa.exe",
                "nba2k.exe",
                "madden.exe",
                "rocketleague.exe",

                // Popular Games - Simulation/Building
                "citiesskylines.exe",
                "simcity.exe",
                "planetcoaster.exe",
                "twopointcampus.exe",
                "kerbalspaceprogram.exe",
                "factorio.exe",
                "satisfactory.exe",
                "valheim.exe",
                "rust.exe",
                "ark.exe",
                "7daystodie.exe",
                "greenhell.exe",
                "theforest.exe",

                // VR Games
                "vrchat.exe",
                "beatsaber.exe",
                "halflife alyx.exe",
                "pavlov.exe",
                "boneworks.exe",

                // Indie/Popular Smaller Games
                "amongus.exe",
                "fallguys.exe",
                "cuphead.exe",
                "hollowknight.exe",
                "celeste.exe",
                "ori.exe",
                "hades.exe",
                "deadcells.exe",
                "riskofrain2.exe",
                "deeprockgalactic.exe",
                "seaofthieves.exe",
                "phasmophobia.exe",
                "genshinimpact.exe",
                "honkaiimpact.exe"
            };
        }

        private bool IsLikelyGameProcess(ProcessModel process)
        {
            try
            {
                var processName = process.Name.ToLowerInvariant();
                var processPath = process.ExecutablePath?.ToLowerInvariant() ?? "";

                // Skip system processes and common applications
                if (IsSystemOrCommonProcess(processName))
                    return false;

                // Check for game-related keywords in process name
                if (HasGameKeywords(processName))
                    return true;

                // Check for game-related paths
                if (HasGamePath(processPath))
                    return true;

                // Check for game engines
                if (HasGameEngineIndicators(processName, processPath))
                    return true;

                // Check for executable patterns common in games
                if (HasGameExecutablePatterns(processName))
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in game detection heuristics for process {ProcessName}", process.Name);
                return false;
            }
        }

        private bool IsSystemOrCommonProcess(string processName)
        {
            var systemProcesses = new[]
            {
                "explorer.exe", "dwm.exe", "winlogon.exe", "csrss.exe", "smss.exe", "wininit.exe",
                "services.exe", "lsass.exe", "svchost.exe", "taskhost.exe", "taskhostw.exe",
                "conhost.exe", "audiodg.exe", "spoolsv.exe", "winlogon.exe", "userinit.exe",
                "chrome.exe", "firefox.exe", "msedge.exe", "iexplore.exe", "opera.exe",
                "notepad.exe", "calc.exe", "mspaint.exe", "wordpad.exe", "cmd.exe", "powershell.exe",
                "winword.exe", "excel.exe", "powerpoint.exe", "outlook.exe", "onenote.exe",
                "photoshop.exe", "illustrator.exe", "premiere.exe", "aftereffects.exe",
                "code.exe", "devenv.exe", "rider.exe", "intellij.exe", "eclipse.exe",
                "discord.exe", "slack.exe", "teams.exe", "zoom.exe", "skype.exe",
                "spotify.exe", "vlc.exe", "wmplayer.exe", "itunes.exe", "winamp.exe",
                "7z.exe", "winrar.exe", "winzip.exe", "filezilla.exe", "putty.exe"
            };

            return systemProcesses.Contains(processName);
        }

        private bool HasGameKeywords(string processName)
        {
            var gameKeywords = new[]
            {
                "game", "launcher", "client", "engine", "unity", "unreal", "godot", "cryengine",
                "steam", "epic", "origin", "uplay", "battlenet", "gog", "rockstar",
                "minecraft", "roblox", "fortnite", "valorant", "csgo", "dota", "lol",
                "wow", "overwatch", "apex", "pubg", "cod", "battlefield", "destiny",
                "cyberpunk", "witcher", "skyrim", "fallout", "gta", "rdr", "assassin",
                "farcry", "watchdog", "borderlands", "diablo", "starcraft", "warcraft",
                "civilization", "totalwar", "aoe", "fifa", "nba", "madden", "forza",
                "racing", "simulator", "tycoon", "builder", "strategy", "rpg", "mmo",
                "shooter", "adventure", "action", "puzzle", "platformer", "indie"
            };

            return gameKeywords.Any(keyword => processName.Contains(keyword));
        }

        private bool HasGamePath(string processPath)
        {
            if (string.IsNullOrEmpty(processPath))
                return false;

            var gamePaths = new[]
            {
                @"\steam\steamapps\", @"\steamapps\common\", @"\steam games\",
                @"\epic games\", @"\epicgames\", @"\epic\",
                @"\origin games\", @"\origin\", @"\ea games\",
                @"\ubisoft\", @"\uplay\", @"\ubisoft game launcher\",
                @"\gog galaxy\", @"\gog games\", @"\gog.com\",
                @"\battle.net\", @"\battlenet\", @"\blizzard\",
                @"\rockstar games\", @"\rockstar\",
                @"\xbox games\", @"\microsoft games\", @"\windowsapps\",
                @"\games\", @"\gaming\", @"\my games\",
                @"\program files\games\", @"\program files (x86)\games\",
                @"\minecraft\", @"\roblox\", @"\riot games\",
                @"\square enix\", @"\activision\", @"\electronic arts\",
                @"\2k games\", @"\bethesda\", @"\cd projekt red\",
                @"\valve\", @"\id software\", @"\bungie\"
            };

            return gamePaths.Any(path => processPath.Contains(path));
        }

        private bool HasGameEngineIndicators(string processName, string processPath)
        {
            var engineIndicators = new[]
            {
                "unity", "unreal", "ue4", "ue5", "godot", "cryengine", "frostbite",
                "source", "idtech", "creation", "anvil", "dunia", "snowdrop",
                "decima", "fox", "mt framework", "luminous", "crystal tools",
                "gamebryo", "havok", "physx", "directx", "opengl", "vulkan"
            };

            return engineIndicators.Any(indicator =>
                processName.Contains(indicator) || processPath.Contains(indicator));
        }

        private bool HasGameExecutablePatterns(string processName)
        {
            // Common patterns in game executables
            var patterns = new[]
            {
                // Shipping builds (Unreal Engine)
                "shipping.exe", "-shipping.exe", "_shipping.exe",
                // Win64 builds
                "win64.exe", "-win64.exe", "_win64.exe",
                // Game suffixes
                "game.exe", "_game.exe", "-game.exe",
                // Client executables
                "client.exe", "_client.exe", "-client.exe",
                // Launcher patterns
                "launcher.exe", "_launcher.exe", "-launcher.exe",
                // Engine patterns
                "engine.exe", "_engine.exe", "-engine.exe",
                // Common game number patterns (sequels)
                "2.exe", "3.exe", "4.exe", "5.exe", "2077.exe", "2042.exe"
            };

            return patterns.Any(pattern => processName.EndsWith(pattern));
        }

        private async Task<string?> GetHighPerformancePowerPlanIdAsync()
        {
            try
            {
                var powerPlans = await _powerPlanService.GetPowerPlansAsync();
                var highPerformancePlan = powerPlans.FirstOrDefault(p => 
                    p.Name.Contains("High performance", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase));

                return highPerformancePlan?.Guid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get high performance power plan");
                return null;
            }
        }

        private async Task SetProcessPriorityAsync(ProcessModel processModel, ProcessPriorityClass priority)
        {
            try
            {
                var process = Process.GetProcessById(processModel.ProcessId);
                process.PriorityClass = priority;
                _logger.LogDebug("Set process {ProcessName} priority to {Priority}", processModel.Name, priority);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set process priority for {ProcessName}", processModel.Name);
            }
        }

        private async Task OptimizeCpuAffinityAsync(ProcessModel processModel)
        {
            try
            {
                // This would integrate with the CPU topology service
                // For now, just log the intent
                _logger.LogDebug("CPU affinity optimization requested for {ProcessName}", processModel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to optimize CPU affinity for {ProcessName}", processModel.Name);
            }
        }
    }
}
