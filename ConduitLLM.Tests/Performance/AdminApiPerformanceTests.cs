using System;
using System.Collections.Generic;
using System.Diagnostics;
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit;

namespace ConduitLLM.Tests.Performance
{
    /// <summary>
    /// Performance tests for Admin API operations
    /// </summary>
    public class AdminApiPerformanceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly AdminApiClient _adminApiClient;

        public AdminApiPerformanceTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            _mockHttpHandler = new Mock<HttpMessageHandler>();

            var httpClient = new HttpClient(_mockHttpHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:5000")
            };

            services.AddSingleton(_ => httpClient);

            var adminOptions = Options.Create(new ConduitLLM.WebUI.Options.AdminApiOptions
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
        public async Task GetAllVirtualKeys_Performance_HandlesLargeDatasets()
        {
            // Arrange
            var largeKeySet = new List<VirtualKeyDto>();
            for (int i = 1; i <= 1000; i++)
            {
                largeKeySet.Add(new VirtualKeyDto
                {
                    Id = i,
                    Name = $"Test Key {i}",
                    AllowedModels = "gpt-4,claude-v1",
                    MaxBudget = 100m + i
                });
            }

            var jsonResponse = JsonSerializer.Serialize(largeKeySet);

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

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var result = await _adminApiClient.GetAllVirtualKeysAsync();
            stopwatch.Stop();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1000, result.Count());
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Operation took {stopwatch.ElapsedMilliseconds}ms, should be under 1000ms");
        }

        [Fact]
        public async Task GetRequestLogs_Performance_HandlesPagination()
        {
            // Arrange
            var logItems = new List<RequestLogDto>();
            for (int i = 1; i <= 100; i++)
            {
                logItems.Add(new RequestLogDto
                {
                    Id = i,
                    ModelId = $"model-{i % 5}",
                    RequestType = "chat",
                    InputTokens = 100 + i,
                    OutputTokens = 200 + i,
                    Cost = 0.01m * i,
                    Timestamp = DateTime.UtcNow.AddMinutes(-i),
                    VirtualKeyId = i % 10,
                    VirtualKeyName = $"key-{i % 10}"
                });
            }

            var pagedResult = new PagedResult<RequestLogDto>
            {
                Items = logItems.Take(20).ToList(),
                TotalCount = 100,
                CurrentPage = 1,
                PageSize = 20,
                TotalPages = 5
            };

            var jsonResponse = JsonSerializer.Serialize(pagedResult);

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

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var result = await _adminApiClient.GetRequestLogsAsync(1, 20);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(20, result.Items.Count);
            Assert.Equal(100, result.TotalCount);
            Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Pagination query took {stopwatch.ElapsedMilliseconds}ms, should be under 500ms");
        }

        [Fact]
        public async Task ConcurrentRequests_Performance_HandlesMultipleOperations()
        {
            // Arrange
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                });

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();

            // Simulate 50 concurrent requests
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(_adminApiClient.GetAllVirtualKeysAsync());
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"50 concurrent requests took {stopwatch.ElapsedMilliseconds}ms, should be under 2000ms");
        }

        [Fact]
        public async Task GetLogsSummary_Performance_HandlesAggregation()
        {
            // Arrange
            var summary = new LogsSummaryDto
            {
                TotalRequests = 10000,
                InputTokens = 1000000,
                OutputTokens = 2000000,
                TotalCost = 500.00m,
                AverageResponseTime = 250.5,
                SuccessfulRequests = 9800,
                FailedRequests = 200,
                RequestsByModel = new Dictionary<string, int>
                {
                    ["gpt-4"] = 5000,
                    ["claude-v1"] = 3000,
                    ["gpt-3.5-turbo"] = 2000
                },
                CostByModel = new Dictionary<string, decimal>
                {
                    ["gpt-4"] = 300.00m,
                    ["claude-v1"] = 150.00m,
                    ["gpt-3.5-turbo"] = 50.00m
                }
            };

            var jsonResponse = JsonSerializer.Serialize(summary);

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

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();
            var result = await _adminApiClient.GetLogsSummaryAsync(7);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10000, result.TotalRequests);
            Assert.True(stopwatch.ElapsedMilliseconds < 300, $"Summary aggregation took {stopwatch.ElapsedMilliseconds}ms, should be under 300ms");
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}
