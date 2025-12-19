using ConduitLLM.Tests.Http.Middleware.Fixtures;
using ConduitLLM.Tests.Http.Middleware.Helpers;

namespace ConduitLLM.Tests.Http.Middleware
{
    /// <summary>
    /// Tests for UsageTrackingMiddleware.
    /// Uses test infrastructure from Fixtures, Builders, Helpers, and Assertions namespaces.
    /// </summary>
    /// <remarks>
    /// Test files are organized by concern:
    /// - UsageTrackingMiddlewareTests.Providers.cs - Provider-specific behavior (OpenAI, Anthropic, etc.)
    /// - UsageTrackingMiddlewareTests.Media.cs - Image and video generation handling
    /// - UsageTrackingMiddlewareTests.PathFiltering.cs - Path-based filtering logic
    /// - UsageTrackingMiddlewareTests.EdgeCases.cs - Streaming, fallback, billing policy, error handling
    /// - UsageTrackingMiddlewareTests.ToolUsage.cs - Provider tool usage tracking
    /// </remarks>
    public partial class UsageTrackingMiddlewareTests : IDisposable
    {
        /// <summary>
        /// The test fixture containing all mock dependencies.
        /// Provides pre-configured mocks for ICostCalculationService, IBatchSpendUpdateService,
        /// IRequestLogService, IVirtualKeyService, IBillingAuditService, IToolCostCalculationService,
        /// and ILogger.
        /// </summary>
        protected readonly UsageTrackingMiddlewareTestFixture Fixture;

        /// <summary>
        /// The middleware invoker for simplified test execution.
        /// Handles the 6-dependency parameter explosion and response configuration.
        /// </summary>
        protected readonly MiddlewareInvoker Invoker;

        /// <summary>
        /// Initializes a new test instance with fresh fixture and invoker.
        /// </summary>
        public UsageTrackingMiddlewareTests()
        {
            Fixture = new UsageTrackingMiddlewareTestFixture();
            Invoker = new MiddlewareInvoker(Fixture);
        }

        /// <summary>
        /// Disposes of test resources including the database context.
        /// </summary>
        public void Dispose()
        {
            Fixture.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
