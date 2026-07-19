namespace ConduitLLM.Functions.Entities.Interfaces;

/// <summary>
/// Shared interface for credential entities that support enable/disable and primary selection.
/// Implemented by both ProviderKeyCredential and FunctionCredential.
/// </summary>
public interface ICredentialEntity
{
    int Id { get; set; }
    bool IsEnabled { get; set; }
    bool IsPrimary { get; set; }
}
