using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Implementation of the IInsecureModeProvider interface that provides
/// information about whether the application is running in insecure mode
/// </summary>
public class InsecureModeProvider : IInsecureModeProvider
{
    /// <summary>
    /// Gets or sets whether the application is running in insecure mode
    /// </summary>
    public bool IsInsecureMode { get; init; }
}
