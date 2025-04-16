using System.Linq.Expressions;

namespace ConduitLLM.Tests.TestHelpers;

/// <summary>
/// Utility class to define Moq expression matchers for tests
/// </summary>
public static class ItExpr
{
    /// <summary>
    /// Matches any value of the specified type
    /// </summary>
    public static T IsAny<T>()
    {
        return default!;
    }
}
