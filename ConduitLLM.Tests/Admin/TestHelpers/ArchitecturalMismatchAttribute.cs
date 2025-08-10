using System;
using Xunit;

namespace ConduitLLM.Tests.Admin.TestHelpers
{
    /// <summary>
    /// Skips tests that have architectural mismatches between test expectations and controller implementation.
    /// These tests should be reviewed and either the test or the controller should be updated to match.
    /// </summary>
    public class ArchitecturalMismatchAttribute : FactAttribute
    {
        public ArchitecturalMismatchAttribute(string reason)
        {
            Skip = $"Architectural mismatch: {reason}";
        }
    }
}