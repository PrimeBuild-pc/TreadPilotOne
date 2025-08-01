using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Condition types for profile triggers
    /// </summary>
    public enum ProfileConditionType
    {
        SystemLoad,
        TimeOfDay,
        PowerState,
        ProcessCount,
        MemoryUsage,
        CpuTemperature,
        BatteryLevel,
        NetworkActivity,
        UserIdle,
        Custom
    }

    /// <summary>
    /// Comparison operators for conditions
    /// </summary>
    public enum ComparisonOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Contains,
        NotContains,
        Between,
        NotBetween
    }

    /// <summary>
    /// Logical operators for combining conditions
    /// </summary>
    public enum LogicalOperator
    {
        And,
        Or,
        Not
    }

    /// <summary>
    /// System state information for condition evaluation
    /// </summary>
    public class SystemState
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public int ProcessCount { get; set; }
        public DateTime CurrentTime { get; set; } = DateTime.Now;
        public bool IsOnBattery { get; set; }
        public int BatteryLevel { get; set; }
        public double CpuTemperature { get; set; }
        public bool IsUserIdle { get; set; }
        public TimeSpan UserIdleTime { get; set; }
        public double NetworkActivity { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }

    /// <summary>
    /// Individual condition for profile evaluation
    /// </summary>
    public partial class ProfileCondition : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private ProfileConditionType conditionType;

        [ObservableProperty]
        private ComparisonOperator comparisonOperator;

        [ObservableProperty]
        private object? value;

        [ObservableProperty]
        private object? secondaryValue; // For Between/NotBetween operations

        [ObservableProperty]
        private bool isEnabled = true;

        [ObservableProperty]
        private string description = string.Empty;

        /// <summary>
        /// Evaluate this condition against the current system state
        /// </summary>
        public bool Evaluate(ProcessModel process, SystemState systemState)
        {
            if (!IsEnabled) return true; // Disabled conditions are considered true

            try
            {
                var actualValue = GetActualValue(process, systemState);
                return CompareValues(actualValue, Value, SecondaryValue, ComparisonOperator);
            }
            catch (Exception)
            {
                return false; // Failed conditions are considered false
            }
        }

        private object? GetActualValue(ProcessModel process, SystemState systemState)
        {
            return ConditionType switch
            {
                ProfileConditionType.SystemLoad => systemState.CpuUsage,
                ProfileConditionType.TimeOfDay => systemState.CurrentTime.TimeOfDay.TotalHours,
                ProfileConditionType.PowerState => systemState.IsOnBattery,
                ProfileConditionType.ProcessCount => systemState.ProcessCount,
                ProfileConditionType.MemoryUsage => systemState.MemoryUsage,
                ProfileConditionType.CpuTemperature => systemState.CpuTemperature,
                ProfileConditionType.BatteryLevel => systemState.BatteryLevel,
                ProfileConditionType.NetworkActivity => systemState.NetworkActivity,
                ProfileConditionType.UserIdle => systemState.IsUserIdle,
                ProfileConditionType.Custom => systemState.CustomProperties.GetValueOrDefault(Name),
                _ => null
            };
        }

        private static bool CompareValues(object? actual, object? expected, object? secondary, ComparisonOperator op)
        {
            if (actual == null || expected == null) return false;

            return op switch
            {
                ComparisonOperator.Equals => actual.Equals(expected),
                ComparisonOperator.NotEquals => !actual.Equals(expected),
                ComparisonOperator.GreaterThan => Comparer<object>.Default.Compare(actual, expected) > 0,
                ComparisonOperator.LessThan => Comparer<object>.Default.Compare(actual, expected) < 0,
                ComparisonOperator.GreaterThanOrEqual => Comparer<object>.Default.Compare(actual, expected) >= 0,
                ComparisonOperator.LessThanOrEqual => Comparer<object>.Default.Compare(actual, expected) <= 0,
                ComparisonOperator.Contains => actual.ToString()?.Contains(expected.ToString() ?? string.Empty) ?? false,
                ComparisonOperator.NotContains => !(actual.ToString()?.Contains(expected.ToString() ?? string.Empty) ?? false),
                ComparisonOperator.Between => secondary != null && 
                    Comparer<object>.Default.Compare(actual, expected) >= 0 && 
                    Comparer<object>.Default.Compare(actual, secondary) <= 0,
                ComparisonOperator.NotBetween => secondary != null && 
                    !(Comparer<object>.Default.Compare(actual, expected) >= 0 && 
                      Comparer<object>.Default.Compare(actual, secondary) <= 0),
                _ => false
            };
        }
    }

    /// <summary>
    /// Group of conditions with logical operators
    /// </summary>
    public partial class ConditionGroup : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private LogicalOperator logicalOperator = LogicalOperator.And;

        [ObservableProperty]
        private List<ProfileCondition> conditions = new();

        [ObservableProperty]
        private List<ConditionGroup> subGroups = new();

        [ObservableProperty]
        private bool isEnabled = true;

        /// <summary>
        /// Evaluate this condition group
        /// </summary>
        public bool Evaluate(ProcessModel process, SystemState systemState)
        {
            if (!IsEnabled) return true;

            var conditionResults = Conditions.Select(c => c.Evaluate(process, systemState)).ToList();
            var subGroupResults = SubGroups.Select(g => g.Evaluate(process, systemState)).ToList();
            var allResults = conditionResults.Concat(subGroupResults).ToList();

            if (!allResults.Any()) return true; // No conditions means always true

            return LogicalOperator switch
            {
                LogicalOperator.And => allResults.All(r => r),
                LogicalOperator.Or => allResults.Any(r => r),
                LogicalOperator.Not => !allResults.All(r => r),
                _ => false
            };
        }
    }

    /// <summary>
    /// Extended ProfileModel with conditional triggers
    /// </summary>
    public partial class ConditionalProcessProfile : ProfileModel
    {
        [ObservableProperty]
        private List<ConditionGroup> conditionGroups = new();

        [ObservableProperty]
        private TimeSpan autoApplyDelay = TimeSpan.FromSeconds(5);

        [ObservableProperty]
        private int priority = 0; // Higher priority profiles are applied first

        [ObservableProperty]
        private bool isAutoApplyEnabled = true;

        [ObservableProperty]
        private DateTime lastEvaluated = DateTime.MinValue;

        [ObservableProperty]
        private DateTime lastApplied = DateTime.MinValue;

        [ObservableProperty]
        private bool wasLastEvaluationTrue = false;

        [ObservableProperty]
        private string lastEvaluationReason = string.Empty;

        /// <summary>
        /// Check if this profile should be applied based on conditions
        /// </summary>
        public bool ShouldApply(ProcessModel process, SystemState systemState)
        {
            if (!IsAutoApplyEnabled) return false;

            LastEvaluated = DateTime.UtcNow;

            try
            {
                // If no condition groups, always apply (like regular profile)
                if (!ConditionGroups.Any())
                {
                    WasLastEvaluationTrue = true;
                    LastEvaluationReason = "No conditions defined";
                    return true;
                }

                // Evaluate all condition groups (AND logic between groups)
                var results = ConditionGroups.Select(g => g.Evaluate(process, systemState)).ToList();
                var shouldApply = results.All(r => r);

                WasLastEvaluationTrue = shouldApply;
                LastEvaluationReason = shouldApply 
                    ? "All condition groups satisfied" 
                    : $"Failed conditions: {string.Join(", ", ConditionGroups.Where((g, i) => !results[i]).Select(g => g.Name))}";

                return shouldApply;
            }
            catch (Exception ex)
            {
                WasLastEvaluationTrue = false;
                LastEvaluationReason = $"Evaluation error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Check if enough time has passed since last application
        /// </summary>
        public bool CanApplyNow()
        {
            return DateTime.UtcNow - LastApplied >= AutoApplyDelay;
        }

        /// <summary>
        /// Mark this profile as applied
        /// </summary>
        public void MarkAsApplied()
        {
            LastApplied = DateTime.UtcNow;
        }
    }
}
