using ConduitLLM.Admin.Controllers;

using FluentAssertions;

using MassTransit;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for the <see cref="AdminControllerBase"/> class.
    /// </summary>
    /// <remarks>
    /// The per-action <c>ExecuteAsync</c>/<c>ExecuteWithNotFoundAsync</c> wrappers were removed in the
    /// Tier 1a cleanup (#902); exception-to-response mapping now lives in <c>AdminExceptionMiddleware</c>
    /// (covered by <c>AdminExceptionMiddlewareTests</c>). These tests cover the constructor/null-validation
    /// behavior that remains on the base class.
    /// </remarks>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class AdminControllerBaseTests
    {
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint = new();
        private readonly Mock<ILogger<TestableAdminController>> _mockLogger = new();

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new TestableAdminController(_mockPublishEndpoint.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithNullPublishEndpoint_DoesNotThrow()
        {
            // Act & Assert
            var act = () => new TestableAdminController(null, _mockLogger.Object);
            act.Should().NotThrow();
        }
    }

    /// <summary>
    /// Concrete implementation of <see cref="AdminControllerBase"/> for testing its constructors.
    /// </summary>
    public class TestableAdminController : AdminControllerBase
    {
        public TestableAdminController(IPublishEndpoint? publishEndpoint, ILogger<TestableAdminController> logger)
            : base(publishEndpoint, logger)
        {
        }
    }
}
