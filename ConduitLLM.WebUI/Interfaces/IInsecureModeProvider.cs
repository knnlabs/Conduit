namespace ConduitLLM.WebUI.Interfaces;

/// <summary>
/// Provides information about whether the application is running in insecure mode
/// </summary>
public interface IInsecureModeProvider
{
    /// <summary>
    /// Gets whether the application is running in insecure mode
    /// </summary>
    bool IsInsecureMode { get; }
}
