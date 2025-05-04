using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces;

/// <summary>
/// Interface for a service that manages global application settings.
/// </summary>
/// <remarks>
/// <para>
/// This service provides a consistent interface for accessing and modifying
/// global application settings that are stored in the database. It abstracts
/// the details of how these settings are persisted and retrieved.
/// </para>
/// <para>
/// The service includes special handling for security-sensitive settings like
/// the master key, which is stored as a hash rather than in plaintext.
/// </para>
/// </remarks>
public interface IGlobalSettingService
{
    /// <summary>
    /// Retrieves the value of a setting by its key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <returns>The value of the setting, or null if the setting does not exist.</returns>
    /// <remarks>
    /// This method is the primary way to retrieve generic application settings.
    /// </remarks>
    Task<string?> GetSettingAsync(string key);
    
    /// <summary>
    /// Sets the value of a setting.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="value">The value to assign to the setting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If the setting with the specified key does not exist, it will be created.
    /// If it does exist, its value will be updated.
    /// </remarks>
    Task SetSettingAsync(string key, string value);
    
    /// <summary>
    /// Retrieves the hash of the master key.
    /// </summary>
    /// <returns>The hash of the master key, or null if no master key has been set.</returns>
    /// <remarks>
    /// This method is used for authentication purposes. The hash should be compared
    /// against the hash of the user-provided master key, not against the raw key itself.
    /// </remarks>
    Task<string?> GetMasterKeyHashAsync();
    
    /// <summary>
    /// Retrieves the algorithm used to hash the master key.
    /// </summary>
    /// <returns>The name of the hashing algorithm (e.g., "SHA256"), or null if not set.</returns>
    /// <remarks>
    /// This provides flexibility for future changes to the hashing algorithm.
    /// Authentication systems can use this information to determine how to hash
    /// the provided key for comparison.
    /// </remarks>
    Task<string?> GetMasterKeyHashAlgorithmAsync();
    
    /// <summary>
    /// Sets the master key, storing it as a hash.
    /// </summary>
    /// <param name="masterKey">The raw master key to hash and store.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method takes the raw master key, hashes it using the configured algorithm,
    /// and stores the hash in the database. The raw key is never persisted.
    /// </remarks>
    Task SetMasterKeyAsync(string masterKey);
}
