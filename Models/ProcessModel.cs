using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot.Models
{
    public partial class ProcessModel : ObservableObject
    {
        private Process? _process;
        public Process? Process
        {
            get => _process;
            set
            {
                _process = value;
                if (value != null)
                {
                    ProcessId = value.Id;
                    Name = value.ProcessName;
                    try
                    {
                        ProcessorAffinity = (long)value.ProcessorAffinity;
                        Priority = value.PriorityClass;
                        MemoryUsage = value.WorkingSet64;
                        ExecutablePath = value.MainModule?.FileName ?? string.Empty;
                        MainWindowHandle = value.MainWindowHandle;
                        MainWindowTitle = value.MainWindowTitle ?? string.Empty;
                        HasVisibleWindow = MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(MainWindowTitle);
                    }
                    catch (Exception)
                    {
                        // Process may have terminated or access denied
                    }
                }
            }
        }

        [ObservableProperty]
        private int processId;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string executablePath = string.Empty;

        [ObservableProperty]
        private double cpuUsage;

        [ObservableProperty]
        private long memoryUsage;

        [ObservableProperty]
        private ProcessPriorityClass priority;

        [ObservableProperty]
        private long processorAffinity;

        [ObservableProperty]
        private IntPtr mainWindowHandle;

        [ObservableProperty]
        private string mainWindowTitle = string.Empty;

        [ObservableProperty]
        private bool hasVisibleWindow;

        [ObservableProperty]
        private bool isIdleServerDisabled;

        [ObservableProperty]
        private bool isRegistryPriorityEnabled;
    }
}