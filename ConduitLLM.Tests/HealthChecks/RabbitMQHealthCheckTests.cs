using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MassTransit;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using Xunit;

using ConduitLLM.Core.HealthChecks;

namespace ConduitLLM.Tests.HealthChecks
{
    public class RabbitMQHealthCheckTests
    {
        private readonly Mock<IBus> _busMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<RabbitMQHealthCheck>> _loggerMock;
        private readonly RabbitMQHealthCheck _healthCheck;

        public RabbitMQHealthCheckTests()
        {
            _busMock = new Mock<IBus>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<RabbitMQHealthCheck>>();

            // Setup configuration
            var configSection = new Mock<IConfigurationSection>();
            configSection.Setup(x => x.Get<ConduitLLM.Configuration.RabbitMqConfiguration>())
                .Returns(new ConduitLLM.Configuration.RabbitMqConfiguration
                {
                    Host = "localhost",
                    Username = "guest",
                    Password = "guest"
                });
            _configurationMock.Setup(x => x.GetSection("ConduitLLM:RabbitMQ"))
                .Returns(configSection.Object);

            _healthCheck = new RabbitMQHealthCheck(
                _busMock.Object,
                _httpClientFactoryMock.Object,
                _configurationMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task CheckHealthAsync_WithErrorQueues_ShouldReturnHealthy()
        {
            // Arrange
            var overviewJson = @"{
                ""queue_totals"": { ""messages"": 100 },
                ""object_totals"": { ""connections"": 5 },
                ""mem_used"": 104857600,
                ""mem_alarm"": false
            }";

            var queuesJson = @"[
                {
                    ""name"": ""spend-update-events"",
                    ""messages"": 10,
                    ""messages_ready"": 10,
                    ""messages_unacknowledged"": 0,
                    ""consumers"": 2
                },
                {
                    ""name"": ""provider-credential-events_error"",
                    ""messages"": 50,
                    ""messages_ready"": 50,
                    ""messages_unacknowledged"": 0,
                    ""consumers"": 0
                },
                {
                    ""name"": ""virtual-key-events_skipped"",
                    ""messages"": 25,
                    ""messages_ready"": 25,
                    ""messages_unacknowledged"": 0,
                    ""consumers"": 0
                }
            ]";

            var httpMessageHandler = new Mock<HttpMessageHandler>();
            httpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    if (request.RequestUri!.ToString().Contains("/api/overview"))
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(overviewJson, Encoding.UTF8, "application/json")
                        };
                    }
                    else if (request.RequestUri!.ToString().Contains("/api/queues"))
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(queuesJson, Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(httpMessageHandler.Object);
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var result = await _healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = new HealthCheckRegistration("test", _ => _healthCheck, null, null) },
                CancellationToken.None);

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("RabbitMQ healthy", result.Description);
            
            // Verify error queue metrics are included but don't affect health
            Assert.True(result.Data.ContainsKey("error_queue_metrics"));
            var errorMetrics = result.Data["error_queue_metrics"] as Dictionary<string, object>;
            Assert.NotNull(errorMetrics);
            Assert.Equal(75L, errorMetrics["total_error_messages"]); // 50 + 25
            Assert.Equal(2, errorMetrics["queues_with_errors"]);
        }

        [Fact]
        public async Task CheckHealthAsync_WithNormalQueueNoConsumers_ShouldReturnUnhealthy()
        {
            // Arrange
            var overviewJson = @"{
                ""queue_totals"": { ""messages"": 100 },
                ""object_totals"": { ""connections"": 5 },
                ""mem_used"": 104857600,
                ""mem_alarm"": false
            }";

            var queuesJson = @"[
                {
                    ""name"": ""spend-update-events"",
                    ""messages"": 100,
                    ""messages_ready"": 100,
                    ""messages_unacknowledged"": 0,
                    ""consumers"": 0
                }
            ]";

            var httpMessageHandler = new Mock<HttpMessageHandler>();
            httpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    if (request.RequestUri!.ToString().Contains("/api/overview"))
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(overviewJson, Encoding.UTF8, "application/json")
                        };
                    }
                    else if (request.RequestUri!.ToString().Contains("/api/queues"))
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(queuesJson, Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(httpMessageHandler.Object);
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var result = await _healthCheck.CheckHealthAsync(
                new HealthCheckContext { Registration = new HealthCheckRegistration("test", _ => _healthCheck, null, null) },
                CancellationToken.None);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Queue spend-update-events has messages but no consumers", result.Description);
        }
    }
}