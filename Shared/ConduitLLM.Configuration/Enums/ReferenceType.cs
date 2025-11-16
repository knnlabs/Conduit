namespace ConduitLLM.Configuration.Enums
{
    /// <summary>
    /// Represents the type of reference that triggered a transaction
    /// </summary>
    public enum ReferenceType
    {
        /// <summary>
        /// Manual transaction created by an administrator
        /// </summary>
        Manual = 1,

        /// <summary>
        /// Transaction created by virtual key usage
        /// </summary>
        VirtualKey = 2,

        /// <summary>
        /// System-generated transaction (e.g., scheduled adjustments)
        /// </summary>
        System = 3,

        /// <summary>
        /// Initial balance when creating a group
        /// </summary>
        Initial = 4
    }
}