namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Unit tests for authentication service implementations.
    /// This class is split into multiple partial class files:
    /// - AuthenticationServiceTests.Setup.cs: Setup, mocks, and helper methods
    /// - AuthenticationServiceTests.Authentication.cs: General authentication tests
    /// - AuthenticationServiceTests.ApiKey.cs: API key validation tests
    /// - AuthenticationServiceTests.Admin.cs: Admin authentication tests
    /// - AuthenticationServiceTests.Token.cs: Token generation and validation tests
    /// - AuthenticationServiceTests.Security.cs: Security-related tests (HTTPS, rate limiting)
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public partial class AuthenticationServiceTests
    {
        // The implementation is split across the partial class files
    }
}
