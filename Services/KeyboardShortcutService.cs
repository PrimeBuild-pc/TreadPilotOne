using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing global keyboard shortcuts using Windows API
    /// </summary>
    public class KeyboardShortcutService : IKeyboardShortcutService, IDisposable
    {
        private readonly ILogger<KeyboardShortcutService> _logger;
        private readonly IApplicationSettingsService _settingsService;
        private readonly Dictionary<string, KeyboardShortcut> _registeredShortcuts = new();
        private readonly Dictionary<int, string> _hotkeyIdToAction = new();
        private int _nextHotkeyId = 1;
        private IntPtr _windowHandle = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private bool _disposed;

        // Windows API constants
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        // Windows API functions
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public event EventHandler<ShortcutActivatedEventArgs>? ShortcutActivated;

        public KeyboardShortcutService(
            ILogger<KeyboardShortcutService> logger,
            IApplicationSettingsService settingsService)
        {
            _logger = logger;
            _settingsService = settingsService;
        }

        public async Task<bool> RegisterShortcutAsync(string actionName, Key key, ModifierKeys modifiers)
        {
            try
            {
                if (string.IsNullOrEmpty(actionName))
                    return false;

                // Check if shortcut is already registered
                if (await IsShortcutRegisteredAsync(key, modifiers))
                {
                    _logger.LogWarning("Shortcut {Key}+{Modifiers} is already registered", key, modifiers);
                    return false;
                }

                // Unregister existing shortcut for this action if it exists
                if (_registeredShortcuts.ContainsKey(actionName))
                {
                    await UnregisterShortcutAsync(actionName);
                }

                var shortcut = new KeyboardShortcut
                {
                    ActionName = actionName,
                    Key = key,
                    Modifiers = modifiers,
                    Description = GetActionDescription(actionName),
                    IsEnabled = true,
                    IsGlobal = true
                };

                // Register with Windows API
                var hotkeyId = _nextHotkeyId++;
                var winModifiers = ConvertToWinModifiers(modifiers);
                var virtualKey = KeyInterop.VirtualKeyFromKey(key);

                if (RegisterHotKey(_windowHandle, hotkeyId, winModifiers, (uint)virtualKey))
                {
                    _registeredShortcuts[actionName] = shortcut;
                    _hotkeyIdToAction[hotkeyId] = actionName;
                    
                    _logger.LogInformation("Registered global shortcut {Shortcut} for action {Action}", 
                        shortcut.ToString(), actionName);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to register global shortcut {Shortcut} for action {Action}", 
                        shortcut.ToString(), actionName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering shortcut for action {Action}", actionName);
                return false;
            }
        }

        public async Task<bool> UnregisterShortcutAsync(string actionName)
        {
            try
            {
                if (!_registeredShortcuts.TryGetValue(actionName, out var shortcut))
                    return false;

                // Find the hotkey ID
                var hotkeyId = _hotkeyIdToAction.FirstOrDefault(kvp => kvp.Value == actionName).Key;
                if (hotkeyId == 0)
                    return false;

                // Unregister from Windows API
                if (UnregisterHotKey(_windowHandle, hotkeyId))
                {
                    _registeredShortcuts.Remove(actionName);
                    _hotkeyIdToAction.Remove(hotkeyId);
                    
                    _logger.LogInformation("Unregistered shortcut {Shortcut} for action {Action}", 
                        shortcut.ToString(), actionName);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to unregister shortcut {Shortcut} for action {Action}", 
                        shortcut.ToString(), actionName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering shortcut for action {Action}", actionName);
                return false;
            }
        }

        public async Task<bool> UpdateShortcutAsync(string actionName, Key key, ModifierKeys modifiers)
        {
            // Unregister existing shortcut and register new one
            await UnregisterShortcutAsync(actionName);
            return await RegisterShortcutAsync(actionName, key, modifiers);
        }

        public async Task<Dictionary<string, KeyboardShortcut>> GetRegisteredShortcutsAsync()
        {
            return new Dictionary<string, KeyboardShortcut>(_registeredShortcuts);
        }

        public async Task<bool> IsShortcutRegisteredAsync(Key key, ModifierKeys modifiers)
        {
            return _registeredShortcuts.Values.Any(s => s.Key == key && s.Modifiers == modifiers);
        }

        public async Task LoadShortcutsFromSettingsAsync()
        {
            try
            {
                var settings = _settingsService.Settings;
                if (settings.KeyboardShortcuts != null)
                {
                    foreach (var shortcutSetting in settings.KeyboardShortcuts)
                    {
                        if (shortcutSetting.IsEnabled)
                        {
                            await RegisterShortcutAsync(shortcutSetting.ActionName, shortcutSetting.Key, shortcutSetting.Modifiers);
                        }
                    }
                }
                else
                {
                    // Load default shortcuts if none are configured
                    await LoadDefaultShortcutsAsync();
                }

                _logger.LogInformation("Loaded {Count} keyboard shortcuts from settings", _registeredShortcuts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shortcuts from settings");
            }
        }

        public async Task SaveShortcutsToSettingsAsync()
        {
            try
            {
                var settings = _settingsService.Settings;
                settings.KeyboardShortcuts = _registeredShortcuts.Values.ToList();
                await _settingsService.SaveSettingsAsync();
                
                _logger.LogInformation("Saved {Count} keyboard shortcuts to settings", _registeredShortcuts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving shortcuts to settings");
            }
        }

        public async Task ClearAllShortcutsAsync()
        {
            var actions = _registeredShortcuts.Keys.ToList();
            foreach (var action in actions)
            {
                await UnregisterShortcutAsync(action);
            }
        }

        public Dictionary<string, KeyboardShortcut> GetDefaultShortcuts()
        {
            return new Dictionary<string, KeyboardShortcut>
            {
                [ShortcutActions.ShowMainWindow] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.ShowMainWindow,
                    Key = Key.T,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Show/Hide main window",
                    IsEnabled = true,
                    IsGlobal = true
                },
                [ShortcutActions.ToggleMonitoring] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.ToggleMonitoring,
                    Key = Key.M,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Toggle process monitoring",
                    IsEnabled = true,
                    IsGlobal = true
                },
                [ShortcutActions.GameBoostToggle] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.GameBoostToggle,
                    Key = Key.G,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Toggle Game Boost mode",
                    IsEnabled = true,
                    IsGlobal = true
                },
                [ShortcutActions.PowerPlanHighPerformance] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.PowerPlanHighPerformance,
                    Key = Key.H,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Switch to High Performance power plan",
                    IsEnabled = true,
                    IsGlobal = true
                },
                [ShortcutActions.OpenTweaks] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.OpenTweaks,
                    Key = Key.W,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Open System Tweaks tab",
                    IsEnabled = true,
                    IsGlobal = true
                }
            };
        }

        public void SetWindowHandle(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            
            // Set up message hook for hotkey messages
            if (_windowHandle != IntPtr.Zero)
            {
                _hwndSource = HwndSource.FromHwnd(_windowHandle);
                if (_hwndSource != null)
                {
                    _hwndSource.AddHook(WndProc);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var hotkeyId = wParam.ToInt32();
                if (_hotkeyIdToAction.TryGetValue(hotkeyId, out var actionName) &&
                    _registeredShortcuts.TryGetValue(actionName, out var shortcut))
                {
                    ShortcutActivated?.Invoke(this, new ShortcutActivatedEventArgs(actionName, shortcut));
                    handled = true;
                }
            }
            
            return IntPtr.Zero;
        }

        private async Task LoadDefaultShortcutsAsync()
        {
            var defaultShortcuts = GetDefaultShortcuts();
            foreach (var shortcut in defaultShortcuts.Values)
            {
                await RegisterShortcutAsync(shortcut.ActionName, shortcut.Key, shortcut.Modifiers);
            }
        }

        private uint ConvertToWinModifiers(ModifierKeys modifiers)
        {
            uint winModifiers = 0;
            
            if (modifiers.HasFlag(ModifierKeys.Alt))
                winModifiers |= MOD_ALT;
            if (modifiers.HasFlag(ModifierKeys.Control))
                winModifiers |= MOD_CONTROL;
            if (modifiers.HasFlag(ModifierKeys.Shift))
                winModifiers |= MOD_SHIFT;
            if (modifiers.HasFlag(ModifierKeys.Windows))
                winModifiers |= MOD_WIN;
                
            return winModifiers;
        }

        private string GetActionDescription(string actionName)
        {
            return actionName switch
            {
                ShortcutActions.QuickApply => "Quick apply current settings",
                ShortcutActions.ToggleMonitoring => "Toggle process monitoring",
                ShortcutActions.ShowMainWindow => "Show/Hide main window",
                ShortcutActions.HideToTray => "Hide to system tray",
                ShortcutActions.PowerPlanBalanced => "Switch to Balanced power plan",
                ShortcutActions.PowerPlanHighPerformance => "Switch to High Performance power plan",
                ShortcutActions.PowerPlanPowerSaver => "Switch to Power Saver power plan",
                ShortcutActions.GameBoostToggle => "Toggle Game Boost mode",
                ShortcutActions.RefreshProcessList => "Refresh process list",
                ShortcutActions.OpenSettings => "Open Settings tab",
                ShortcutActions.OpenTweaks => "Open System Tweaks tab",
                ShortcutActions.ExitApplication => "Exit application",
                _ => actionName
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ClearAllShortcutsAsync().Wait();
                
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource = null;
                }
                
                _disposed = true;
            }
        }
    }
}
