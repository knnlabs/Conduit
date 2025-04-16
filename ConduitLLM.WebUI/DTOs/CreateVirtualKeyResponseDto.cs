namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// DTO for the response after successfully creating a Virtual Key.
/// Includes the generated key itself.
/// </summary>
public class CreateVirtualKeyResponseDto
{
    /// <summary>
    /// The generated virtual API key. This should be copied and stored securely by the user.
    /// </summary>
    public required string VirtualKey { get; set; }

    /// <summary>
    /// The details of the created key (excluding the full key hash).
    /// </summary>
    public required VirtualKeyDto KeyInfo { get; set; }
}
