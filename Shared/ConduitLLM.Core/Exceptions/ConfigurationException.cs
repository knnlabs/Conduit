namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Represents errors related to loading, parsing, or validating configuration settings.
/// </summary>
public class ConfigurationException : ConduitException
{
    public ConfigurationException() { }
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
