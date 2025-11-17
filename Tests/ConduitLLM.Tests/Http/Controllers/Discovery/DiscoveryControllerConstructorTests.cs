using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class DiscoveryControllerConstructorTests : DiscoveryControllerTestsBase
    {
        public DiscoveryControllerConstructorTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Constructor_WithValidDependencies_ShouldCreateInstance()
        {
            // Arrange & Act
            var controller = new DiscoveryController(
                MockDbContextFactory.Object,
                MockModelCapabilityService.Object,
                MockVirtualKeyService.Object,
                MockDiscoveryCacheService.Object,
                MockLogger.Object);

            // Assert
            Assert.NotNull(controller);
        }

        [Fact]
        public void Constructor_WithNullDbContextFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                null!,
                MockModelCapabilityService.Object,
                MockVirtualKeyService.Object,
                MockDiscoveryCacheService.Object,
                MockLogger.Object));
            
            Assert.Equal("dbContextFactory", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullModelCapabilityService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                MockDbContextFactory.Object,
                null!,
                MockVirtualKeyService.Object,
                MockDiscoveryCacheService.Object,
                MockLogger.Object));
            
            Assert.Equal("modelCapabilityService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                MockDbContextFactory.Object,
                MockModelCapabilityService.Object,
                null!,
                MockDiscoveryCacheService.Object,
                MockLogger.Object));
            
            Assert.Equal("virtualKeyService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                MockDbContextFactory.Object,
                MockModelCapabilityService.Object,
                MockVirtualKeyService.Object,
                MockDiscoveryCacheService.Object,
                null!));
            
            Assert.Equal("logger", ex.ParamName);
        }
    }
}