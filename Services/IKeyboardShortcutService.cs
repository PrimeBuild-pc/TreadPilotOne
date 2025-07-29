using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing global keyboard shortcuts
    /// </summary>
    public interface IKeyboardShortcutService
    {
        /// <summary>
        /// Event raised when a registered shortcut is activated
        /// </summary>
        event EventHandler<ShortcutActivatedEventArgs>? ShortcutActivated;

        /// <summary>
        /// Register a global keyboard shortcut
        /// </summary>
        Task<bool> RegisterShortcutAsync(string actionName, Key key, ModifierKeys modifiers);

        /// <summary>
        /// Unregister a keyboard shortcut
        /// </summary>
        Task<bool> UnregisterShortcutAsync(string actionName);

        /// <summary>
        /// Update an existing shortcut with new key combination
        /// </summary>
        Task<bool> UpdateShortcutAsync(string actionName, Key key, ModifierKeys modifiers);

        /// <summary>
        /// Get all registered shortcuts
        /// </summary>
        Task<Dictionary<string, KeyboardShortcut>> GetRegisteredShortcutsAsync();

        /// <summary>
        /// Check if a key combination is already registered
        /// </summary>
        Task<bool> IsShortcutRegisteredAsync(Key key, ModifierKeys modifiers);

        /// <summary>
        /// Load shortcuts from settings
        /// </summary>
        Task LoadShortcutsFromSettingsAsync();

        /// <summary>
        /// Save shortcuts to settings
        /// </summary>
        Task SaveShortcutsToSettingsAsync();

        /// <summary>
        /// Clear all registered shortcuts
        /// </summary>
        Task ClearAllShortcutsAsync();

        /// <summary>
        /// Get the default shortcuts for the application
        /// </summary>
        Dictionary<string, KeyboardShortcut> GetDefaultShortcuts();
    }

    /// <summary>
    /// Represents a keyboard shortcut
    /// </summary>
    public class KeyboardShortcut
    {
        public string ActionName { get; set; } = string.Empty;
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool IsGlobal { get; set; } = true;

        public override string ToString()
        {
            var parts = new List<string>();
            
            if (Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");
                
            parts.Add(Key.ToString());
            
            return string.Join(" + ", parts);
        }
    }

    /// <summary>
    /// Event args for shortcut activation
    /// </summary>
    public class ShortcutActivatedEventArgs : EventArgs
    {
        public string ActionName { get; }
        public KeyboardShortcut Shortcut { get; }
        public DateTime ActivationTime { get; }

        public ShortcutActivatedEventArgs(string actionName, KeyboardShortcut shortcut)
        {
            ActionName = actionName;
            Shortcut = shortcut;
            ActivationTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Predefined shortcut actions
    /// </summary>
    public static class ShortcutActions
    {
        public const string QuickApply = "QuickApply";
        public const string ToggleMonitoring = "ToggleMonitoring";
        public const string ShowMainWindow = "ShowMainWindow";
        public const string HideToTray = "HideToTray";
        public const string PowerPlanBalanced = "PowerPlanBalanced";
        public const string PowerPlanHighPerformance = "PowerPlanHighPerformance";
        public const string PowerPlanPowerSaver = "PowerPlanPowerSaver";
        public const string GameBoostToggle = "GameBoostToggle";
        public const string RefreshProcessList = "RefreshProcessList";
        public const string OpenSettings = "OpenSettings";
        public const string OpenTweaks = "OpenTweaks";
        public const string ExitApplication = "ExitApplication";
    }
}
