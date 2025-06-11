using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using Xunit;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Tests for AdminApiClient functionality
    /// </summary>
    public class AdminApiClientTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly AdminApiClient _adminApiClient;

        public AdminApiClientTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            // Mock HTTP handler for testing
            _mockHttpHandler = new Mock<HttpMessageHandler>();

            var httpClient = new HttpClient(_mockHttpHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:5000")
            };

            services.AddSingleton(_ => httpClient);
            services.AddHttpClient<AdminApiClient>((sp, client) =>
            {
                var existingClient = sp.GetRequiredService<HttpClient>();
                client.BaseAddress = existingClient.BaseAddress;
            }).ConfigurePrimaryHttpMessageHandler(() => _mockHttpHandler.Object);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AdminApi:BaseUrl", "http://localhost:5000" },
                    { "AdminApi:MasterKey", "test-master-key" },
                    { "AdminApi:UseAdminApi", "true" }
                })
                .Build();

            services.AddSingleton<IConfiguration>(config);

            var adminOptions = Microsoft.Extensions.Options.Options.Create(new ConduitLLM.WebUI.Options.AdminApiOptions
            {
                BaseUrl = "http://localhost:5000",
                MasterKey = "test-master-key"
            });

            _serviceProvider = services.BuildServiceProvider();
            _adminApiClient = new AdminApiClient(
                httpClient,
                adminOptions,
                _serviceProvider.GetRequiredService<ILogger<AdminApiClient>>()
            );
        }

        [Fact]
        public async Task GetAllVirtualKeysAsync_ReturnsKeys_WhenSuccessful()
        {
            // Arrange
            var expectedKeys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto
                {
                    Id = 1,
                    Name = "Test Key",
                    AllowedModels = "gpt-4",
                    MaxBudget = 100m
                }
            };

            var jsonResponse = JsonSerializer.Serialize(expectedKeys);

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.PathAndQuery == "/api/virtualkeys"),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _adminApiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Test Key", result.First().Name);
        }

        [Fact]
        public async Task CreateVirtualKeyAsync_ReturnsNewKey_WhenSuccessful()
        {
            // Arrange
            var createRequest = new CreateVirtualKeyRequestDto
            {
                KeyName = "New Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 200m
            };

            var expectedResponse = new CreateVirtualKeyResponseDto
            {
                VirtualKey = "vk_test123",
                KeyInfo = new VirtualKeyDto
                {
                    Id = 2,
                    Name = createRequest.KeyName,
                    AllowedModels = createRequest.AllowedModels,
                    MaxBudget = createRequest.MaxBudget
                }
            };

            var jsonResponse = JsonSerializer.Serialize(expectedResponse);

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.PathAndQuery == "/api/virtualkeys"),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _adminApiClient.CreateVirtualKeyAsync(createRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("vk_test123", result.VirtualKey);
            Assert.Equal("New Test Key", result.KeyInfo.Name);
        }

        [Fact]
        public async Task GetLogsSummaryAsync_ReturnsSummary_WhenSuccessful()
        {
            // Arrange
            var expectedSummary = new LogsSummaryDto
            {
                TotalRequests = 100,
                InputTokens = 1000,
                OutputTokens = 2000,
                TotalCost = 0.05m,
                DailyStats = new List<DailyUsageStatsDto>
                {
                    new DailyUsageStatsDto
                    {
                        Date = DateTime.UtcNow.Date,
                        RequestCount = 50,
                        InputTokens = 500,
                        OutputTokens = 1000,
                        Cost = 0.025m
                    }
                }
            };

            var jsonResponse = JsonSerializer.Serialize(expectedSummary);

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.PathAndQuery.StartsWith("/api/logs/summary")),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _adminApiClient.GetLogsSummaryAsync(7);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.TotalRequests);
            Assert.Equal(0.05m, result.TotalCost);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_ReturnsTrue_WhenSuccessful()
        {
            // Arrange
            var keyId = 123;

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Delete &&
                        req.RequestUri!.PathAndQuery == $"/api/virtualkeys/{keyId}"),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NoContent
                });

            // Act
            var result = await _adminApiClient.DeleteVirtualKeyAsync(keyId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetRequestLogsAsync_ReturnsPagedResult_WhenSuccessful()
        {
            // Arrange
            var expectedResult = new PagedResult<RequestLogDto>
            {
                Items = new List<RequestLogDto>
                {
                    new RequestLogDto
                    {
                        Id = 1,
                        ModelId = "gpt-4",
                        RequestType = "chat",
                        InputTokens = 100,
                        OutputTokens = 200,
                        Cost = 0.01m,
                        Timestamp = DateTime.UtcNow,
                        VirtualKeyId = 1,
                        VirtualKeyName = "test-key"
                    }
                },
                TotalCount = 1,
                CurrentPage = 1,
                PageSize = 10
            };

            var jsonResponse = JsonSerializer.Serialize(expectedResult);

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.PathAndQuery.StartsWith("/api/logs")),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _adminApiClient.GetRequestLogsAsync(1, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.Equal("gpt-4", result.Items.First().ModelId);
        }

        private void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}
