namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Types of cache operations.
    /// </summary>
    public enum CacheOperationType
    {
        Get,
        Set,
        Remove,
        Clear,
        Refresh,
        Eviction,
        Hit,
        Miss
    }
}
