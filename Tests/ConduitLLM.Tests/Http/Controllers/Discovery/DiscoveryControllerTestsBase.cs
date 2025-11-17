using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using ConduitLLM.Tests.Http.TestHelpers;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery
{
    public abstract class DiscoveryControllerTestsBase : ControllerTestBase, IDisposable
    {
        protected readonly Mock<IDbContextFactory<ConduitDbContext>> MockDbContextFactory;
        protected readonly Mock<IModelCapabilityService> MockModelCapabilityService;
        protected readonly Mock<IVirtualKeyService> MockVirtualKeyService;
        protected readonly Mock<IDiscoveryCacheService> MockDiscoveryCacheService;
        protected readonly Mock<ILogger<DiscoveryController>> MockLogger;
        protected ConduitDbContext DbContext;
        protected readonly DiscoveryController Controller;
        protected readonly string DatabaseName;

        protected DiscoveryControllerTestsBase(ITestOutputHelper output) : base(output)
        {
            DatabaseName = Guid.NewGuid().ToString();
            MockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            MockModelCapabilityService = new Mock<IModelCapabilityService>();
            MockVirtualKeyService = new Mock<IVirtualKeyService>();
            MockDiscoveryCacheService = new Mock<IDiscoveryCacheService>();
            MockLogger = new Mock<ILogger<DiscoveryController>>();

            Controller = new DiscoveryController(
                MockDbContextFactory.Object,
                MockModelCapabilityService.Object,
                MockVirtualKeyService.Object,
                MockDiscoveryCacheService.Object,
                MockLogger.Object);

            // Setup default DbContext factory to return InMemory database
            SetupInMemoryDatabase();
        }

        private void SetupInMemoryDatabase()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: DatabaseName)
                .Options;

            DbContext = new ConduitDbContext(options);
            MockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(DbContext);
        }

        protected void SetupValidVirtualKey(string keyValue)
        {
            var claims = new List<Claim> { new Claim("VirtualKey", keyValue) };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var httpContext = new DefaultHttpContext { User = principal };
            Controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var virtualKey = new VirtualKey { Id = 1, KeyHash = keyValue, IsEnabled = true };
            MockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(keyValue, It.IsAny<string>()))
                .ReturnsAsync(virtualKey);
        }

        protected void SetupModelProviderMappings(List<ModelProviderMapping> mappings)
        {
            // Clear existing data
            DbContext.ModelProviderMappings.RemoveRange(DbContext.ModelProviderMappings);
            
            // Add new data
            if (mappings.Any())
            {
                DbContext.ModelProviderMappings.AddRange(mappings);
                DbContext.SaveChanges();
            }
        }
        
        public new void Dispose()
        {
            DbContext?.Dispose();
            base.Dispose();
        }
    }
}