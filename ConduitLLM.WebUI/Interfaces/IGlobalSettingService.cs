using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces;

public interface IGlobalSettingService
{
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
    Task<string?> GetMasterKeyHashAsync();
    Task<string?> GetMasterKeyHashAlgorithmAsync(); // Added for future flexibility
    Task SetMasterKeyAsync(string masterKey); // Takes the raw key, handles hashing
}
