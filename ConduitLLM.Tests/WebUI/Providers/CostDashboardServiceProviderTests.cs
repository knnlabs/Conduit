using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit;

using CostDTOs = ConduitLLM.Configuration.DTOs.Costs;
using WebUIDto = ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.Tests.WebUI.Providers
{
    public class AdminApiClientCostDashboardTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<AdminApiClient>> _mockLogger;
        private readonly AdminApiClient _adminApiClient;

        public AdminApiClientCostDashboardTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockLogger = new Mock<ILogger<AdminApiClient>>();

            var options = new AdminApiOptions
            {
                BaseUrl = "https://admin-api.example.com",
                MasterKey = "test-api-key"
            };

            var optionsWrapper = new Mock<IOptions<AdminApiOptions>>();
            optionsWrapper.Setup(x => x.Value).Returns(options);

            _adminApiClient = new AdminApiClient(_httpClient, optionsWrapper.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ReturnsValidDashboard_WhenApiReturnsData()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKeyId = 1;
            var modelName = "Model1";

            // Create dashboard data to return from the API
            var dashboardData = new CostDTOs.CostDashboardDto
            {
                TotalCost = 4.0m,
                StartDate = startDate,
                EndDate = endDate,
                Last24HoursCost = 1.0m,
                Last7DaysCost = 3.0m,
                Last30DaysCost = 4.0m,
                TopModelsBySpend = new List<CostDTOs.DetailedCostDataDto>
                {
                    new CostDTOs.DetailedCostDataDto { Name = "Model1", Cost = 2.5m, Percentage = 62.5m },
                    new CostDTOs.DetailedCostDataDto { Name = "Model2", Cost = 1.5m, Percentage = 37.5m }
                },
                TopProvidersBySpend = new List<CostDTOs.DetailedCostDataDto>
                {
                    new CostDTOs.DetailedCostDataDto { Name = "Provider1", Cost = 4.0m, Percentage = 100m }
                },
                TopVirtualKeysBySpend = new List<CostDTOs.DetailedCostDataDto>
                {
                    new CostDTOs.DetailedCostDataDto { Name = "Key1", Cost = 4.0m, Percentage = 100m }
                }
            };

            // Setup mock HTTP handler
            var jsonResponse = JsonSerializer.Serialize(dashboardData);
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await ((ICostDashboardService)_adminApiClient).GetDashboardDataAsync(startDate, endDate, virtualKeyId, modelName);

            // Assert
            Assert.NotNull(result);
            // We're properly deserializing the response due to our test setup
            Assert.Equal(4.0m, result.TotalCost);
            Assert.NotNull(result.TopModelsBySpend);
            Assert.NotNull(result.TopProvidersBySpend);
            Assert.NotNull(result.TopVirtualKeysBySpend);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ReturnsEmptyDashboard_WhenApiReturnsNull()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            // Setup mock HTTP handler to return not found
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await ((ICostDashboardService)_adminApiClient).GetDashboardDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalCost);
            Assert.Equal(0, result.Last24HoursCost);
            Assert.Equal(0, result.Last7DaysCost);
            Assert.Equal(0, result.Last30DaysCost);
            Assert.Empty(result.TopModelsBySpend);
            Assert.Empty(result.TopProvidersBySpend);
            Assert.Empty(result.TopVirtualKeysBySpend);
        }

        [Fact]
        public async Task GetDetailedCostDataAsync_ReturnsDetailedData_WhenApiReturnsData()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKeyId = 1;
            var modelName = "Model1";

            // Mock detailed cost data API response with WebUI DTOs
            var detailedCostData = new List<WebUIDto.DetailedCostDataDto>
            {
                new WebUIDto.DetailedCostDataDto
                {
                    Name = "Request 1",
                    Cost = 1.5m,
                    Percentage = 42.8m
                },
                new WebUIDto.DetailedCostDataDto
                {
                    Name = "Request 2",
                    Cost = 2.0m,
                    Percentage = 57.2m
                }
            };

            // Setup mock HTTP handler
            var jsonResponse = JsonSerializer.Serialize(detailedCostData);
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await ((ICostDashboardService)_adminApiClient).GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);

            // Assert
            Assert.NotNull(result);
            // We should get the data from our mocked response
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetAvailableModelsAsync_ReturnsModels_WhenApiReturnsData()
        {
            // Arrange
            var models = new List<string> { "Model1", "Model2", "Model3" };

            // Setup mock HTTP handler
            var jsonResponse = JsonSerializer.Serialize(models);
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await ((ICostDashboardService)_adminApiClient).GetAvailableModelsAsync();

            // Assert
            Assert.NotNull(result);
            // We should get the data from our mocked response
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetVirtualKeysAsync_ReturnsVirtualKeys_WhenApiReturnsData()
        {
            // Arrange
            var virtualKeys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto { Id = 1, KeyName = "Key1" },
                new VirtualKeyDto { Id = 2, KeyName = "Key2" }
            };

            // Setup mock HTTP handler
            var jsonResponse = JsonSerializer.Serialize(virtualKeys);
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await ((ICostDashboardService)_adminApiClient).GetVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            // We should get the data from our mocked response
            Assert.Equal(2, result.Count);
        }
    }
}
