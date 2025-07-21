using System.Diagnostics;
using Xunit;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Provides conditional execution for integration tests based on infrastructure availability.
/// </summary>
public static class IntegrationTestConditions
{
    private static readonly Lazy<bool> _shouldRunIntegrationTests = new(CheckIfIntegrationTestsShouldRun);

    /// <summary>
    /// Gets whether integration tests should run.
    /// </summary>
    public static bool ShouldRunIntegrationTests => _shouldRunIntegrationTests.Value;

    /// <summary>
    /// Skip reason when integration tests are disabled.
    /// </summary>
    public static string SkipReason => "Integration tests require infrastructure. Set RUN_INTEGRATION_TESTS=true to run.";

    private static bool CheckIfIntegrationTestsShouldRun()
    {
        // Check environment variable - MUST be explicitly set to true to run
        var runIntegrationTests = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        
        // Only run if explicitly enabled
        return !string.IsNullOrEmpty(runIntegrationTests) && 
               (runIntegrationTests.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                runIntegrationTests.Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = "info";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            
            process.Start();
            
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return false;
            }
            
            return process.ExitCode == 0;
        }
        catch
        {
            // Docker not available
            return false;
        }
    }
}

/// <summary>
/// Collection definition for integration tests that require infrastructure.
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection
{
}

/// <summary>
/// Custom fact attribute that conditionally runs integration tests.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!IntegrationTestConditions.ShouldRunIntegrationTests)
        {
            Skip = IntegrationTestConditions.SkipReason;
        }
    }
}

/// <summary>
/// Custom theory attribute that conditionally runs integration tests.
/// </summary>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (!IntegrationTestConditions.ShouldRunIntegrationTests)
        {
            Skip = IntegrationTestConditions.SkipReason;
        }
    }
}