using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class MetricsControllerTests : ControllerTestBase
    {
        private readonly Mock<IDbContextFactory<ConfigurationDbContext>> _mockDbContextFactory;
        private readonly Mock<ILogger<MetricsController>> _mockLogger;
        private readonly MetricsController _controller;

        public MetricsControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockDbContextFactory = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            _mockLogger = CreateLogger<MetricsController>();

            _controller = new MetricsController(
                _mockDbContextFactory.Object,
                _mockLogger.Object);

            // Setup default controller context
            _controller.ControllerContext = CreateControllerContext();
        }

        #region GetDatabasePoolMetrics Tests

        [Fact]
        public async Task GetDatabasePoolMetrics_WithException_ShouldReturn500()
        {
            // Arrange
            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _controller.GetDatabasePoolMetrics();

            // Assert
            var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, internalServerErrorResult.StatusCode);
            dynamic errorResponse = internalServerErrorResult.Value;
            Assert.NotNull(errorResponse);
            Assert.Equal("Failed to retrieve metrics", errorResponse.error.ToString());
            Assert.Equal("Database connection failed", errorResponse.message.ToString());
        }

        #endregion

        #region GetAllMetrics Tests

        [Fact]
        public async Task GetAllMetrics_WithDatabaseFailure_ShouldStillReturnPartialMetrics()
        {
            // Arrange
            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database unavailable"));

            // Act
            var result = await _controller.GetAllMetrics();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic metrics = okResult.Value;
            Assert.NotNull(metrics);
            Assert.NotNull(metrics.timestamp);
            Assert.NotNull(metrics.application);
            Assert.NotNull(metrics.system);
            // Database metrics will be null when database call fails
            Assert.Null(metrics.database);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldAllowAnonymousAccess()
        {
            // Arrange & Act
            var controllerType = typeof(MetricsController);
            var allowAnonymousAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute));

            // Assert
            Assert.NotNull(allowAnonymousAttribute);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullDbContextFactory_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MetricsController(null, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MetricsController(_mockDbContextFactory.Object, null));
        }

        #endregion
    }
}