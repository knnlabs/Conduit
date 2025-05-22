using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration;
using Moq;

namespace ConduitLLM.Tests.WebUI
{
    /// <summary>
    /// Integration tests to verify all WebUI pages load without server errors.
    /// </summary>
    public class PageLoadingIntegrationTests : IClassFixture<WebApplicationFactory<ConduitLLM.WebUI.Program>>
    {
        private readonly WebApplicationFactory<ConduitLLM.WebUI.Program> _factory;

        public PageLoadingIntegrationTests(WebApplicationFactory<ConduitLLM.WebUI.Program> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/about")]
        [InlineData("/access-denied")]
        [InlineData("/admin-api-auth-error")]
        [InlineData("/admin-api-error")]
        [InlineData("/blazor-diagnostics")]
        [InlineData("/caching-settings")]
        [InlineData("/chat")]
        [InlineData("/configuration")]
        [InlineData("/configuration-template")]
        [InlineData("/cost-dashboard")]
        [InlineData("/error")]
        [InlineData("/home")]
        [InlineData("/ip-access-filtering")]
        [InlineData("/login")]
        [InlineData("/logout")]
        [InlineData("/mapping-edit")]
        [InlineData("/model-costs")]
        [InlineData("/provider-edit")]
        [InlineData("/provider-health")]
        [InlineData("/provider-health-config")]
        [InlineData("/request-logs")]
        [InlineData("/routing-settings")]
        [InlineData("/system-info")]
        [InlineData("/test-interactive")]
        [InlineData("/virtual-key-edit")]
        [InlineData("/virtual-keys")]
        [InlineData("/virtual-keys-dashboard")]
        public async Task Get_EndpointsReturnSuccessAndCorrectContentType(string url)
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove real services and add mocks
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IAdminApiClient));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add mock AdminApiClient
                    var mockAdminApiClient = new Mock<IAdminApiClient>();
                    mockAdminApiClient.Setup(x => x.IsConfiguredAsync())
                        .ReturnsAsync(true);
                    mockAdminApiClient.Setup(x => x.HealthCheckAsync())
                        .ReturnsAsync(true);
                    mockAdminApiClient.Setup(x => x.GetVirtualKeysAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto>());
                    mockAdminApiClient.Setup(x => x.GetGlobalSettingsAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.GlobalSettingDto>());
                    mockAdminApiClient.Setup(x => x.GetModelProviderMappingsAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.ModelProviderMappingDto>());
                    mockAdminApiClient.Setup(x => x.GetProviderCredentialsAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.ProviderCredentialDto>());
                    mockAdminApiClient.Setup(x => x.GetCostDashboardDataAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                        .ReturnsAsync(new ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto());
                    mockAdminApiClient.Setup(x => x.GetRequestLogsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.RequestLogDto>());
                    mockAdminApiClient.Setup(x => x.GetIpFiltersAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterDto>());
                    mockAdminApiClient.Setup(x => x.GetProviderHealthConfigurationsAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.ProviderHealthConfigurationDto>());
                    mockAdminApiClient.Setup(x => x.GetProviderHealthRecordsAsync(It.IsAny<string>()))
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.ProviderHealthRecordDto>());
                    mockAdminApiClient.Setup(x => x.GetModelCostsAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.ModelCostDto>());
                    mockAdminApiClient.Setup(x => x.GetSystemInfoAsync())
                        .ReturnsAsync(new Dictionary<string, object>());
                    mockAdminApiClient.Setup(x => x.GetNotificationsAsync())
                        .ReturnsAsync(new List<ConduitLLM.Configuration.DTOs.NotificationDto>());

                    services.AddSingleton(mockAdminApiClient.Object);

                    // Add other required mocks
                    var mockProviderStatusService = new Mock<IProviderStatusService>();
                    services.AddSingleton(mockProviderStatusService.Object);

                    var mockModelHealthCheckService = new Mock<IConduitApiClient>();
                    services.AddSingleton(mockModelHealthCheckService.Object);

                    // Add mocked configuration
                    var inMemorySettings = new Dictionary<string, string>
                    {
                        {"AdminApi:BaseUrl", "http://localhost:5001"},
                        {"AdminApi:MasterKey", "test-key"}
                    };
                    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(inMemorySettings)
                        .Build();
                    services.AddSingleton<IConfiguration>(configuration);

                    // Mock global setting service for InitialSetupService
                    var mockGlobalSettingService = new Mock<IGlobalSettingService>();
                    mockGlobalSettingService.Setup(x => x.GetSettingAsync(It.IsAny<string>()))
                        .ReturnsAsync((string key) => new GlobalSettingDto { Key = key, Value = "test-value" });
                    services.AddSingleton(mockGlobalSettingService.Object);

                    var mockInsecureModeProvider = new Mock<IInsecureModeProvider>();
                    mockInsecureModeProvider.Setup(x => x.IsInsecureMode).Returns(false);
                    services.AddSingleton(mockInsecureModeProvider.Object);

                    // Configure test environment
                    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                });

                builder.UseEnvironment("Development");
            }).CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // Assert
            // We expect either success (2xx) or redirect (3xx) for authentication
            Assert.True(
                response.IsSuccessStatusCode || 
                response.StatusCode == HttpStatusCode.Redirect ||
                response.StatusCode == HttpStatusCode.Found,
                $"Page {url} returned {response.StatusCode}");

            // Ensure no server errors (5xx)
            Assert.True(
                (int)response.StatusCode < 500,
                $"Page {url} returned server error: {response.StatusCode}");
        }

        [Fact]
        public async Task Get_SecuredEndpoints_RedirectToLoginWhenNotAuthenticated()
        {
            // Arrange
            var securedUrls = new[]
            {
                "/configuration",
                "/virtual-keys",
                "/cost-dashboard",
                "/request-logs",
                "/provider-health",
                "/model-costs",
                "/system-info"
            };

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Add minimal mocks for unauthenticated requests
                    var mockAdminApiClient = new Mock<IAdminApiClient>();
                    mockAdminApiClient.Setup(x => x.IsConfiguredAsync())
                        .ReturnsAsync(false);
                    services.AddSingleton(mockAdminApiClient.Object);

                    // Add minimal configuration
                    var inMemorySettings = new Dictionary<string, string>
                    {
                        {"AdminApi:BaseUrl", "http://localhost:5001"},
                        {"AdminApi:MasterKey", "test-key"}
                    };
                    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(inMemorySettings)
                        .Build();
                    services.AddSingleton<IConfiguration>(configuration);

                    var mockInsecureModeProvider = new Mock<IInsecureModeProvider>();
                    mockInsecureModeProvider.Setup(x => x.IsInsecureMode).Returns(false);
                    services.AddSingleton(mockInsecureModeProvider.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            foreach (var url in securedUrls)
            {
                // Act
                var response = await client.GetAsync(url);

                // Assert
                Assert.True(
                    response.StatusCode == HttpStatusCode.Redirect || 
                    response.StatusCode == HttpStatusCode.Found ||
                    response.StatusCode == HttpStatusCode.Unauthorized,
                    $"Secured page {url} should redirect or return unauthorized when not authenticated, but returned {response.StatusCode}");
            }
        }

        [Fact]
        public async Task Get_NonExistentPage_ReturnsNotFound()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/this-page-does-not-exist");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}