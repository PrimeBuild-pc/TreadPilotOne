using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ThreadPilot.Helpers
{
    public static class AffinityHelper
    {
        public static long CalculateAffinityMask(IEnumerable<CheckBox> cpuCheckboxes)
        {
            return cpuCheckboxes
                .Where(cb => cb.IsChecked == true)
                .Sum(cb => (long)cb.Tag);
        }

        public static void UpdateCheckboxesFromMask(IEnumerable<CheckBox> cpuCheckboxes, long affinityMask)
        {
            foreach (var checkbox in cpuCheckboxes)
            {
                var cpuBit = (long)checkbox.Tag;
                checkbox.IsChecked = (affinityMask & cpuBit) != 0;
            }
        }
    }
}