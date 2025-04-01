using System.Diagnostics;

namespace ThreadPilot.Models
{
    public class ProfileModel
    {
        public string ProcessName { get; set; } = string.Empty;
        public ProcessPriorityClass Priority { get; set; }
        public long ProcessorAffinity { get; set; }
    }
}