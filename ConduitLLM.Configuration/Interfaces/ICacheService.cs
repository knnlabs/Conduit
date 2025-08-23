namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Interface for caching services
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Gets a value from the cache
        /// </summary>
        /// <typeparam name="T">Type of the cached value</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>Cached value or default if not found</returns>
        T? Get<T>(string key);

        /// <summary>
        /// Sets a value in the cache
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="absoluteExpiration">Absolute expiration time</param>
        /// <param name="slidingExpiration">Sliding expiration time</param>
        void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);

        /// <summary>
        /// Removes a value from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        void Remove(string key);

        /// <summary>
        /// Gets a value from the cache or creates it if it doesn't exist
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create the value if not in cache</param>
        /// <param name="absoluteExpiration">Absolute expiration time</param>
        /// <param name="slidingExpiration">Sliding expiration time</param>
        /// <returns>Cached or newly created value</returns>
        Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);

        /// <summary>
        /// Removes all cache entries with keys that start with the specified prefix
        /// </summary>
        /// <param name="prefix">Key prefix</param>
        void RemoveByPrefix(string prefix);
    }
}
