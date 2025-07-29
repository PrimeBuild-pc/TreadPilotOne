namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for security validation and auditing of elevated operations
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Validates that the specified operation is allowed to be performed with elevated privileges
        /// </summary>
        /// <param name="operation">The operation to validate</param>
        /// <returns>True if the operation is allowed, false otherwise</returns>
        bool ValidateElevatedOperation(string operation);

        /// <summary>
        /// Audits an elevated action for security logging
        /// </summary>
        /// <param name="action">The action that was performed</param>
        /// <param name="target">The target of the action (process name, power plan, etc.)</param>
        /// <param name="success">Whether the action was successful</param>
        Task AuditElevatedAction(string action, string target, bool success);

        /// <summary>
        /// Validates that a process operation is safe to perform
        /// </summary>
        /// <param name="processName">The name of the process</param>
        /// <param name="operation">The operation to perform</param>
        /// <returns>True if the operation is safe, false otherwise</returns>
        bool ValidateProcessOperation(string processName, string operation);

        /// <summary>
        /// Validates that a power plan operation is safe to perform
        /// </summary>
        /// <param name="powerPlanId">The power plan GUID</param>
        /// <param name="operation">The operation to perform</param>
        /// <returns>True if the operation is safe, false otherwise</returns>
        bool ValidatePowerPlanOperation(string powerPlanId, string operation);

        /// <summary>
        /// Gets the list of allowed elevated operations
        /// </summary>
        /// <returns>Array of allowed operation names</returns>
        string[] GetAllowedElevatedOperations();
    }
}
