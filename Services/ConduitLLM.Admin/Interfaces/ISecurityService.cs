using SharedSecurity = ConduitLLM.Security.Interfaces;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Admin-specific security service interface.
    /// Extends the shared security service with master key validation.
    /// </summary>
    public interface IAdminSecurityService : SharedSecurity.ISecurityService
    {
        /// <summary>
        /// Validates the API key against the configured master key
        /// </summary>
        bool ValidateApiKey(string providedKey);
    }
}
