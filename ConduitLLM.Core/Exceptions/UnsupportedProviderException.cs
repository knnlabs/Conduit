using System;

namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Represents an error when a requested LLM provider is not supported or configured.
/// </summary>
public class UnsupportedProviderException : ConduitException
{
    /// <summary>
    /// The identifier of the provider that was requested but not found or supported.
    /// </summary>
    public string? ProviderId { get; }

    public UnsupportedProviderException() { }

    public UnsupportedProviderException(string providerId) : base($"Provider '{providerId}' is not supported or configured.")
    {
        ProviderId = providerId;
    }

    public UnsupportedProviderException(string message, Exception innerException) : base(message, innerException) { }

    public UnsupportedProviderException(string providerId, string message) : base($"Provider '{providerId}': {message}")
    {
        ProviderId = providerId;
    }
}
