namespace ConduitLLM.Configuration.DTOs.VirtualKey;

/// <summary>
/// DTO representing the response after successfully creating a virtual key.
/// Includes the generated key (only shown once) and its details.
/// </summary>
public class CreateVirtualKeyResponseDto
{
    /// <summary>
    /// The newly generated virtual key. This should be securely stored by the user
    /// as it will not be retrievable again.
    /// </summary>
    public string VirtualKey { get; set; } = string.Empty;

    /// <summary>
    /// Details of the created key (excluding the hash).
    /// </summary>
    public VirtualKeyDto KeyInfo { get; set; } = null!;
}
