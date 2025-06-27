using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ConduitLLM.Http.DTOs.HealthMonitoring;
using ConduitLLM.Http.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for the health monitoring and alert system
    /// </summary>
    public class HealthMonitoringIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public HealthMonitoringIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task ServiceDownSimulation_Should_TriggerCriticalAlert()
        {
            // Arrange
            var alertReceived = new TaskCompletionSource<HealthAlert>();
            var hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_client.BaseAddress}hubs/health-monitoring", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            hubConnection.On<HealthAlert>("HealthAlert", alert =>
            {
                if (alert.Type == AlertType.ServiceDown)
                    alertReceived.TrySetResult(alert);
            });

            await hubConnection.StartAsync();

            // Act
            var response = await _client.PostAsync("/api/test/health-monitoring/start/service-down?durationSeconds=10", null);
            response.EnsureSuccessStatusCode();

            // Assert
            var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotNull(alert);
            Assert.Equal(AlertSeverity.Critical, alert.Severity);
            Assert.Equal(AlertType.ServiceDown, alert.Type);
            Assert.Equal("Database", alert.Component);
            Assert.Contains("Database Connection Failed", alert.Title);

            // Cleanup
            await _client.PostAsync("/api/test/health-monitoring/stop/service-down", null);
            await hubConnection.DisposeAsync();
        }

        [Fact]
        public async Task PerformanceDegradation_Should_TriggerWarningAlert()
        {
            // Arrange
            var alertReceived = new TaskCompletionSource<HealthAlert>();
            var hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_client.BaseAddress}hubs/health-monitoring", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            hubConnection.On<HealthAlert>("HealthAlert", alert =>
            {
                if (alert.Type == AlertType.PerformanceDegradation)
                    alertReceived.TrySetResult(alert);
            });

            await hubConnection.StartAsync();

            // Act
            var response = await _client.PostAsync("/api/test/health-monitoring/start/slow-response?durationSeconds=10", null);
            response.EnsureSuccessStatusCode();

            // Assert
            var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotNull(alert);
            Assert.Equal(AlertSeverity.Warning, alert.Severity);
            Assert.Equal(AlertType.PerformanceDegradation, alert.Type);
            Assert.Contains("Slow API Response", alert.Title);

            // Cleanup
            await _client.PostAsync("/api/test/health-monitoring/stop/slow-response", null);
            await hubConnection.DisposeAsync();
        }

        [Fact]
        public async Task SecurityThreat_Should_TriggerSecurityAlert()
        {
            // Arrange
            var alertReceived = new TaskCompletionSource<HealthAlert>();
            var hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_client.BaseAddress}hubs/health-monitoring", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            hubConnection.On<HealthAlert>("HealthAlert", alert =>
            {
                if (alert.Type == AlertType.SecurityEvent)
                    alertReceived.TrySetResult(alert);
            });

            await hubConnection.StartAsync();

            // Act
            var response = await _client.PostAsync("/api/test/health-monitoring/start/brute-force?durationSeconds=10", null);
            response.EnsureSuccessStatusCode();

            // Assert
            var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotNull(alert);
            Assert.Equal(AlertType.SecurityEvent, alert.Type);
            Assert.Contains("authentication", alert.Title.ToLower());

            // Cleanup
            await _client.PostAsync("/api/test/health-monitoring/stop/brute-force", null);
            await hubConnection.DisposeAsync();
        }

        [Fact]
        public async Task SystemHealthSnapshot_Should_UpdateInRealTime()
        {
            // Arrange
            var snapshotReceived = new TaskCompletionSource<SystemHealthSnapshot>();
            var hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_client.BaseAddress}hubs/health-monitoring", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            hubConnection.On<SystemHealthSnapshot>("SystemHealthSnapshot", snapshot =>
            {
                snapshotReceived.TrySetResult(snapshot);
            });

            await hubConnection.StartAsync();

            // Act - Request current health status
            await hubConnection.InvokeAsync("RequestHealthStatus");

            // Assert
            var snapshot = await snapshotReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(snapshot);
            // OverallStatus is a value type, no need to check for null
            Assert.NotNull(snapshot.Components);
            Assert.True(snapshot.Components.Count > 0);

            await hubConnection.DisposeAsync();
        }

        [Fact]
        public async Task AlertAcknowledgment_Should_UpdateAlertState()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertManagementService>();

            // Create a test alert
            var testAlert = new HealthAlert
            {
                Severity = AlertSeverity.Warning,
                Type = AlertType.Custom,
                Component = "Test",
                Title = "Test Alert",
                Message = "This is a test alert for acknowledgment"
            };

            await alertService.TriggerAlertAsync(testAlert);

            // Act
            var acknowledged = await alertService.AcknowledgeAlertAsync(testAlert.Id, "Test User", "Investigating the issue");

            // Assert
            Assert.True(acknowledged);
            var activeAlerts = await alertService.GetActiveAlertsAsync();
            var alert = activeAlerts.FirstOrDefault(a => a.Id == testAlert.Id);
            Assert.NotNull(alert);
            Assert.NotNull(alert.AcknowledgedAt);
            Assert.Equal("Test User", alert.AcknowledgedBy);
        }

        [Fact]
        public async Task AlertSuppression_Should_PreventAlerts()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertManagementService>();
            var suppressionCreated = false;
            AlertSuppression? createdSuppression = null;

            // Create suppression rule
            var suppression = new AlertSuppression
            {
                AlertPattern = "Test*",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(10),
                Reason = "Testing suppression",
                CreatedBy = "Test User"
            };

            createdSuppression = await alertService.CreateSuppressionAsync(suppression);
            if (createdSuppression != null && !string.IsNullOrEmpty(createdSuppression.Id))
            {
                suppressionCreated = true;
            }

            // Act - Try to trigger an alert that should be suppressed
            var testAlert = new HealthAlert
            {
                Severity = AlertSeverity.Warning,
                Type = AlertType.Custom,
                Component = "Test",
                Title = "Test Alert",
                Message = "This alert should be suppressed"
            };

            await alertService.TriggerAlertAsync(testAlert);

            // Assert
            Assert.True(suppressionCreated);
            var activeAlerts = await alertService.GetActiveAlertsAsync();
            var alert = activeAlerts.FirstOrDefault(a => a.Id == testAlert.Id);
            
            // Alert should either not exist or be in suppressed state
            Assert.True(alert == null || alert.State == AlertState.Suppressed);

            // Cleanup
            if (suppressionCreated && createdSuppression != null)
            {
                await alertService.CancelSuppressionAsync(createdSuppression.Id);
            }
        }

        [Fact]
        public async Task MultipleSimultaneousScenarios_Should_HandleCorrectly()
        {
            // Arrange
            var scenarios = new[] { "high-cpu", "memory-leak", "slow-response" };
            var tasks = new List<Task<HttpResponseMessage>>();

            // Act - Start multiple scenarios
            foreach (var scenario in scenarios)
            {
                tasks.Add(_client.PostAsync($"/api/test/health-monitoring/start/{scenario}?durationSeconds=5", null));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            foreach (var response in responses)
            {
                response.EnsureSuccessStatusCode();
            }

            // Verify active scenarios
            var activeResponse = await _client.GetAsync("/api/test/health-monitoring/active");
            activeResponse.EnsureSuccessStatusCode();
            var activeScenarios = await activeResponse.Content.ReadFromJsonAsync<List<string>>();
            
            Assert.NotNull(activeScenarios);
            Assert.Equal(3, activeScenarios!.Count);
            foreach (var scenario in scenarios)
            {
                Assert.Contains(scenario, activeScenarios);
            }

            // Cleanup
            foreach (var scenario in scenarios)
            {
                await _client.PostAsync($"/api/test/health-monitoring/stop/{scenario}", null);
            }
        }

        [Fact]
        public async Task ResourceExhaustion_Should_TriggerAppropriateAlerts()
        {
            // Arrange
            var alertsReceived = new List<HealthAlert>();
            var hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_client.BaseAddress}hubs/health-monitoring", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            hubConnection.On<HealthAlert>("HealthAlert", alert =>
            {
                if (alert.Type == AlertType.ResourceExhaustion)
                    alertsReceived.Add(alert);
            });

            await hubConnection.StartAsync();

            // Act
            var response = await _client.PostAsync("/api/test/health-monitoring/start/connection-pool?durationSeconds=10", null);
            response.EnsureSuccessStatusCode();

            // Wait for alerts
            await Task.Delay(5000);

            // Assert
            Assert.NotEmpty(alertsReceived);
            var connectionAlert = alertsReceived.FirstOrDefault(a => a.Component.Contains("Connection", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(connectionAlert);
            Assert.Equal(AlertSeverity.Warning, connectionAlert.Severity);

            // Cleanup
            await _client.PostAsync("/api/test/health-monitoring/stop/connection-pool", null);
            await hubConnection.DisposeAsync();
        }

        [Fact]
        public async Task CustomAlert_Should_BeTriggeredSuccessfully()
        {
            // Arrange
            var alertReceived = new TaskCompletionSource<HealthAlert>();
            var hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_client.BaseAddress}hubs/health-monitoring", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            hubConnection.On<HealthAlert>("HealthAlert", alert =>
            {
                if (alert.Type == AlertType.Custom && alert.Title == "Integration Test Alert")
                    alertReceived.TrySetResult(alert);
            });

            await hubConnection.StartAsync();

            // Act
            var customAlert = new
            {
                Severity = AlertSeverity.Info,
                Title = "Integration Test Alert",
                Message = "This is a custom alert from integration tests",
                Component = "IntegrationTest",
                SuggestedActions = new[] { "No action required", "This is just a test" }
            };

            var response = await _client.PostAsJsonAsync("/api/test/health-monitoring/alert", customAlert);
            response.EnsureSuccessStatusCode();

            // Assert
            var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(alert);
            Assert.Equal(AlertSeverity.Info, alert.Severity);
            Assert.Equal("Integration Test Alert", alert.Title);
            Assert.Equal("IntegrationTest", alert.Component);
            Assert.Contains("No action required", alert.SuggestedActions);

            await hubConnection.DisposeAsync();
        }

        [Fact]
        public async Task HealthCheckEndpoint_Should_ReturnDetailedStatus()
        {
            // Act
            var response = await _client.GetAsync("/health");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(content);
            
            // Verify it contains expected health check results
            Assert.Contains("status", content.ToLower());
        }
    }
}