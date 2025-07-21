using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.DTOs.HealthMonitoring;
using ConduitLLM.Http.Services;
using ConduitLLM.Security.Interfaces;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Test controller for simulating various failure scenarios to test the health monitoring system
    /// </summary>
    [ApiController]
    [Route("api/test/health-monitoring")]
    [Authorize(Policy = "AdminOnly")]
    public class HealthMonitoringTestController : ControllerBase
    {
        private readonly ILogger<HealthMonitoringTestController> _logger;
        private readonly IAlertManagementService _alertManagementService;
        private readonly IPerformanceMonitoringService _performanceMonitoring;
        private readonly ISecurityEventMonitoringService _securityEventMonitoring;
        private readonly IMemoryCache _memoryCache;
        private static readonly Dictionary<string, CancellationTokenSource> _activeSimulations = new();

        public HealthMonitoringTestController(
            ILogger<HealthMonitoringTestController> logger,
            IAlertManagementService alertManagementService,
            IPerformanceMonitoringService performanceMonitoring,
            ISecurityEventMonitoringService securityEventMonitoring,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _alertManagementService = alertManagementService;
            _performanceMonitoring = performanceMonitoring;
            _securityEventMonitoring = securityEventMonitoring;
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Get available test scenarios
        /// </summary>
        [HttpGet("scenarios")]
        public IActionResult GetTestScenarios()
        {
            var scenarios = new[]
            {
                new { Id = "service-down", Name = "Simulate Service Down", Description = "Simulates a critical service being unavailable" },
                new { Id = "high-cpu", Name = "High CPU Usage", Description = "Simulates high CPU utilization" },
                new { Id = "memory-leak", Name = "Memory Leak", Description = "Simulates gradual memory exhaustion" },
                new { Id = "slow-response", Name = "Slow Response Times", Description = "Simulates degraded API performance" },
                new { Id = "high-error-rate", Name = "High Error Rate", Description = "Simulates increased API errors" },
                new { Id = "brute-force", Name = "Brute Force Attack", Description = "Simulates authentication attack" },
                new { Id = "rate-limit-breach", Name = "Rate Limit Violations", Description = "Simulates excessive API usage" },
                new { Id = "data-exfiltration", Name = "Data Exfiltration", Description = "Simulates suspicious data transfer" },
                new { Id = "connection-pool", Name = "Connection Pool Exhaustion", Description = "Simulates database connection issues" },
                new { Id = "disk-space", Name = "Low Disk Space", Description = "Simulates disk space exhaustion" }
            };

            return Ok(scenarios);
        }

        /// <summary>
        /// Start a test scenario
        /// </summary>
        [HttpPost("start/{scenario}")]
        public Task<IActionResult> StartScenario(string scenario, [FromQuery] int durationSeconds = 60)
        {
            if (_activeSimulations.ContainsKey(scenario))
            {
                return Task.FromResult<IActionResult>(BadRequest($"Scenario '{scenario}' is already running"));
            }

            var cts = new CancellationTokenSource();
            _activeSimulations[scenario] = cts;

            _logger.LogWarning("Starting test scenario: {Scenario} for {Duration} seconds", scenario, durationSeconds);

            // Start scenario in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunScenario(scenario, durationSeconds, cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running scenario {Scenario}", scenario);
                }
                finally
                {
                    _activeSimulations.Remove(scenario);
                }
            });

            return Task.FromResult<IActionResult>(Ok(new { message = $"Started scenario '{scenario}' for {durationSeconds} seconds" }));
        }

        /// <summary>
        /// Stop a running test scenario
        /// </summary>
        [HttpPost("stop/{scenario}")]
        public IActionResult StopScenario(string scenario)
        {
            if (_activeSimulations.TryGetValue(scenario, out var cts))
            {
                cts.Cancel();
                _activeSimulations.Remove(scenario);
                _logger.LogInformation("Stopped test scenario: {Scenario}", scenario);
                return Ok(new { message = $"Stopped scenario '{scenario}'" });
            }

            return NotFound($"Scenario '{scenario}' is not running");
        }

        /// <summary>
        /// Get currently running scenarios
        /// </summary>
        [HttpGet("active")]
        public IActionResult GetActiveScenarios()
        {
            return Ok(_activeSimulations.Keys.ToList());
        }

        /// <summary>
        /// Trigger a custom alert
        /// </summary>
        [HttpPost("alert")]
        public async Task<IActionResult> TriggerCustomAlert([FromBody] CustomAlertRequest request)
        {
            var alert = new HealthAlert
            {
                Severity = request.Severity,
                Type = AlertType.Custom,
                Component = request.Component ?? "Test",
                Title = request.Title,
                Message = request.Message,
                Context = new Dictionary<string, object>
                {
                    ["Source"] = "Test Controller",
                    ["TriggeredBy"] = User.Identity?.Name ?? "Unknown",
                    ["IsTest"] = true
                },
                SuggestedActions = request.SuggestedActions ?? new List<string>()
            };

            await _alertManagementService.TriggerAlertAsync(alert);

            return Ok(new { alertId = alert.Id, message = "Alert triggered successfully" });
        }

        private async Task RunScenario(string scenario, int durationSeconds, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);

            switch (scenario)
            {
                case "service-down":
                    await SimulateServiceDown(endTime, cancellationToken);
                    break;
                case "high-cpu":
                    await SimulateHighCpu(endTime, cancellationToken);
                    break;
                case "memory-leak":
                    await SimulateMemoryLeak(endTime, cancellationToken);
                    break;
                case "slow-response":
                    await SimulateSlowResponse(endTime, cancellationToken);
                    break;
                case "high-error-rate":
                    await SimulateHighErrorRate(endTime, cancellationToken);
                    break;
                case "brute-force":
                    await SimulateBruteForce(endTime, cancellationToken);
                    break;
                case "rate-limit-breach":
                    await SimulateRateLimitBreach(endTime, cancellationToken);
                    break;
                case "data-exfiltration":
                    await SimulateDataExfiltration(endTime, cancellationToken);
                    break;
                case "connection-pool":
                    await SimulateConnectionPoolExhaustion(endTime, cancellationToken);
                    break;
                case "disk-space":
                    await SimulateLowDiskSpace(endTime, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Unknown scenario: {Scenario}", scenario);
                    break;
            }
        }

        private async Task SimulateServiceDown(DateTime endTime, CancellationToken cancellationToken)
        {
            await _alertManagementService.TriggerAlertAsync(new HealthAlert
            {
                Severity = AlertSeverity.Critical,
                Type = AlertType.ServiceDown,
                Component = "Database",
                Title = "Database Connection Failed",
                Message = "Unable to connect to primary database server",
                Context = new Dictionary<string, object>
                {
                    ["ConnectionString"] = "Server=db.example.com;Database=conduit;",
                    ["LastSuccessfulConnection"] = DateTime.UtcNow.AddMinutes(-5),
                    ["AttemptsCount"] = 10,
                    ["IsSimulated"] = true
                },
                SuggestedActions = new List<string>
                {
                    "Check database server status",
                    "Verify network connectivity",
                    "Review database logs",
                    "Check connection string configuration"
                }
            });

            // Simulate periodic retry attempts
            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10000, cancellationToken); // Every 10 seconds
                
                await _alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Error,
                    Type = AlertType.ConnectivityIssue,
                    Component = "Database",
                    Title = "Database Reconnection Failed",
                    Message = "Retry attempt failed to establish database connection",
                    Context = new Dictionary<string, object>
                    {
                        ["RetryCount"] = DateTime.UtcNow.Subtract(endTime.AddSeconds(-60)).TotalSeconds / 10,
                        ["IsSimulated"] = true
                    }
                });
            }
        }

        private async Task SimulateHighCpu(DateTime endTime, CancellationToken cancellationToken)
        {
            var cpuTasks = new List<Task>();
            
            // Create CPU-intensive tasks
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                cpuTasks.Add(Task.Run(() =>
                {
                    while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
                    {
                        // CPU-intensive calculation
                        double result = 0;
                        for (int j = 0; j < 1000000; j++)
                        {
                            result += Math.Sqrt(j) * Math.Sin(j);
                        }
                    }
                }, cancellationToken));
            }

            // Monitor and report high CPU
            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000, cancellationToken);
                
                var process = Process.GetCurrentProcess();
                var cpuTime = process.TotalProcessorTime.TotalMilliseconds;
                
                await _alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Warning,
                    Type = AlertType.ResourceExhaustion,
                    Component = "System",
                    Title = "High CPU Usage Detected",
                    Message = "CPU usage is above threshold",
                    Context = new Dictionary<string, object>
                    {
                        ["CpuTimeMs"] = cpuTime,
                        ["ThreadCount"] = process.Threads.Count,
                        ["IsSimulated"] = true
                    }
                });
            }

            await Task.WhenAll(cpuTasks);
        }

        private async Task SimulateMemoryLeak(DateTime endTime, CancellationToken cancellationToken)
        {
            var leakedMemory = new List<byte[]>();
            var allocationSize = 10 * 1024 * 1024; // 10MB chunks

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Allocate memory that won't be freed
                    leakedMemory.Add(new byte[allocationSize]);
                    
                    // Fill with data to ensure it's actually allocated
                    var lastArray = leakedMemory.Last();
                    new Random().NextBytes(lastArray);

                    // Report memory usage
                    var process = Process.GetCurrentProcess();
                    var memoryMB = process.WorkingSet64 / (1024 * 1024);
                    
                    if (memoryMB > 500) // Alert if over 500MB
                    {
                        await _alertManagementService.TriggerAlertAsync(new HealthAlert
                        {
                            Severity = AlertSeverity.Warning,
                            Type = AlertType.ResourceExhaustion,
                            Component = "Memory",
                            Title = "High Memory Usage Detected",
                            Message = $"Process memory usage: {memoryMB}MB",
                            Context = new Dictionary<string, object>
                            {
                                ["WorkingSetMB"] = memoryMB,
                                ["GCGen0"] = GC.CollectionCount(0),
                                ["GCGen1"] = GC.CollectionCount(1),
                                ["GCGen2"] = GC.CollectionCount(2),
                                ["IsSimulated"] = true
                            }
                        });
                    }

                    await Task.Delay(2000, cancellationToken); // Every 2 seconds
                }
                catch (OutOfMemoryException)
                {
                    _logger.LogWarning("Simulated memory leak reached system limits");
                    break;
                }
            }

            // Cleanup
            leakedMemory.Clear();
            GC.Collect();
        }

        private async Task SimulateSlowResponse(DateTime endTime, CancellationToken cancellationToken)
        {
            var endpoints = new[] { "/v1/chat/completions", "/v1/embeddings", "/v1/images/generations" };
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var endpoint = endpoints[random.Next(endpoints.Length)];
                var responseTime = random.Next(3000, 10000); // 3-10 seconds

                _performanceMonitoring.RecordRequestMetric(endpoint, responseTime, true);

                if (responseTime > 5000)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Warning,
                        Type = AlertType.PerformanceDegradation,
                        Component = "API",
                        Title = "Slow API Response",
                        Message = $"Endpoint {endpoint} responded in {responseTime}ms",
                        Context = new Dictionary<string, object>
                        {
                            ["Endpoint"] = endpoint,
                            ["ResponseTimeMs"] = responseTime,
                            ["Threshold"] = 5000,
                            ["IsSimulated"] = true
                        }
                    });
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        private async Task SimulateHighErrorRate(DateTime endTime, CancellationToken cancellationToken)
        {
            var endpoints = new[] { "/v1/chat/completions", "/v1/embeddings", "/v1/images/generations" };
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var endpoint = endpoints[random.Next(endpoints.Length)];
                var isError = random.Next(100) < 30; // 30% error rate

                _performanceMonitoring.RecordRequestMetric(endpoint, random.Next(100, 500), !isError);

                if (isError)
                {
                    _logger.LogError("Simulated error for endpoint {Endpoint}", endpoint);
                }

                await Task.Delay(100, cancellationToken); // High frequency
            }
        }

        private async Task SimulateBruteForce(DateTime endTime, CancellationToken cancellationToken)
        {
            var attackerIps = new[] { "192.168.1.100", "10.0.0.50", "172.16.0.25" };
            var virtualKeys = new[] { "vk_test_key_001", "vk_test_key_002", "vk_test_key_003" };
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var ip = attackerIps[random.Next(attackerIps.Length)];
                var key = virtualKeys[random.Next(virtualKeys.Length)] + random.Next(1000);
                var endpoint = "/v1/chat/completions";

                _securityEventMonitoring.RecordAuthenticationFailure(ip, key, endpoint);

                await Task.Delay(200, cancellationToken); // Rapid attempts
            }
        }

        private async Task SimulateRateLimitBreach(DateTime endTime, CancellationToken cancellationToken)
        {
            var ip = "192.168.1.200";
            var virtualKey = "vk_test_heavy_user";
            var endpoint = "/v1/chat/completions";

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                for (int i = 0; i < 10; i++)
                {
                    _securityEventMonitoring.RecordRateLimitViolation(ip, virtualKey, endpoint, "RPM");
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        private async Task SimulateDataExfiltration(DateTime endTime, CancellationToken cancellationToken)
        {
            var ip = "10.0.0.100";
            var virtualKey = "vk_test_suspicious";
            var endpoints = new[] { "/v1/embeddings", "/v1/chat/completions" };
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var endpoint = endpoints[random.Next(endpoints.Length)];
                var dataSize = random.Next(10_000_000, 100_000_000); // 10MB to 100MB

                _securityEventMonitoring.RecordDataExfiltrationAttempt(ip, virtualKey, dataSize, endpoint);

                await Task.Delay(5000, cancellationToken);
            }
        }

        private async Task SimulateConnectionPoolExhaustion(DateTime endTime, CancellationToken cancellationToken)
        {
            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                _performanceMonitoring.RecordConnectionPoolMetric("PostgreSQL", 95, 5, 20);
                _performanceMonitoring.RecordConnectionPoolMetric("Redis", 48, 2, 15);

                await Task.Delay(2000, cancellationToken);
            }
        }

        private async Task SimulateLowDiskSpace(DateTime endTime, CancellationToken cancellationToken)
        {
            await _alertManagementService.TriggerAlertAsync(new HealthAlert
            {
                Severity = AlertSeverity.Critical,
                Type = AlertType.ResourceExhaustion,
                Component = "Disk",
                Title = "Low Disk Space",
                Message = "Primary disk has less than 5% free space",
                Context = new Dictionary<string, object>
                {
                    ["DiskPath"] = "/",
                    ["TotalGB"] = 100,
                    ["FreeGB"] = 4.5,
                    ["UsedPercent"] = 95.5,
                    ["IsSimulated"] = true
                },
                SuggestedActions = new List<string>
                {
                    "Clean up old log files",
                    "Remove temporary files",
                    "Archive old media assets",
                    "Increase disk capacity"
                }
            });

            // Wait for scenario duration
            await Task.Delay((int)(endTime - DateTime.UtcNow).TotalMilliseconds, cancellationToken);
        }

        public class CustomAlertRequest
        {
            public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
            public string Title { get; set; } = "";
            public string Message { get; set; } = "";
            public string? Component { get; set; }
            public List<string>? SuggestedActions { get; set; }
        }
    }
}