using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Exports image generation metrics to Prometheus.
    /// </summary>
    public class PrometheusImageGenerationMetricsExporter : BackgroundService
    {
        private readonly ILogger<PrometheusImageGenerationMetricsExporter> _logger;
        private readonly IImageGenerationMetricsCollector _metricsCollector;
        private readonly PrometheusImageGenerationOptions _options;
        
        // Prometheus metrics
        private readonly Counter _totalGenerationsCounter;
        private readonly Counter _successfulGenerationsCounter;
        private readonly Counter _failedGenerationsCounter;
        private readonly Counter _totalImagesCounter;
        private readonly Counter _totalCostCounter;
        
        private readonly Gauge _activeGenerationsGauge;
        private readonly Gauge _queueDepthGauge;
        private readonly Gauge _providerHealthGauge;
        private readonly Gauge _providerResponseTimeGauge;
        private readonly Gauge _systemSuccessRateGauge;
        
        private readonly Histogram _generationDurationHistogram;
        private readonly Histogram _imageCostHistogram;
        private readonly Histogram _queueWaitTimeHistogram;
        
        private readonly Summary _responseTimeSummary;
        
        private Timer? _exportTimer;

        public PrometheusImageGenerationMetricsExporter(
            ILogger<PrometheusImageGenerationMetricsExporter> logger,
            IImageGenerationMetricsCollector metricsCollector,
            IOptions<PrometheusImageGenerationOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _options = options?.Value ?? new PrometheusImageGenerationOptions();
            
            // Initialize counters
            _totalGenerationsCounter = Metrics.CreateCounter(
                "conduit_image_generations_total",
                "Total number of image generation requests",
                new CounterConfiguration
                {
                    LabelNames = new[] { "provider", "model", "status" }
                });
            
            _successfulGenerationsCounter = Metrics.CreateCounter(
                "conduit_image_generations_successful_total",
                "Total number of successful image generations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "provider", "model" }
                });
            
            _failedGenerationsCounter = Metrics.CreateCounter(
                "conduit_image_generations_failed_total",
                "Total number of failed image generations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "provider", "model", "error_type" }
                });
            
            _totalImagesCounter = Metrics.CreateCounter(
                "conduit_images_generated_total",
                "Total number of images generated",
                new CounterConfiguration
                {
                    LabelNames = new[] { "provider", "model", "size" }
                });
            
            _totalCostCounter = Metrics.CreateCounter(
                "conduit_image_generation_cost_total",
                "Total cost of image generation in dollars",
                new CounterConfiguration
                {
                    LabelNames = new[] { "provider", "model" }
                });
            
            // Initialize gauges
            _activeGenerationsGauge = Metrics.CreateGauge(
                "conduit_image_generations_active",
                "Number of currently active image generation requests");
            
            _queueDepthGauge = Metrics.CreateGauge(
                "conduit_image_generation_queue_depth",
                "Current depth of the image generation queue",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "priority" }
                });
            
            _providerHealthGauge = Metrics.CreateGauge(
                "conduit_image_provider_health_score",
                "Health score of image generation providers (0-1)",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "provider" }
                });
            
            _providerResponseTimeGauge = Metrics.CreateGauge(
                "conduit_image_provider_response_time_ms",
                "Average response time of image generation providers in milliseconds",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "provider" }
                });
            
            _systemSuccessRateGauge = Metrics.CreateGauge(
                "conduit_image_generation_success_rate",
                "Overall success rate of image generation (0-100)");
            
            // Initialize histograms
            _generationDurationHistogram = Metrics.CreateHistogram(
                "conduit_image_generation_duration_seconds",
                "Duration of image generation requests in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "provider", "model" },
                    Buckets = Histogram.ExponentialBuckets(1, 2, 10) // 1s to ~1024s
                });
            
            _imageCostHistogram = Metrics.CreateHistogram(
                "conduit_image_cost_dollars",
                "Cost distribution of generated images in dollars",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "provider", "model" },
                    Buckets = new[] { 0.01, 0.02, 0.03, 0.04, 0.05, 0.10, 0.20, 0.50, 1.00 }
                });
            
            _queueWaitTimeHistogram = Metrics.CreateHistogram(
                "conduit_image_generation_queue_wait_seconds",
                "Time spent waiting in queue in seconds",
                new HistogramConfiguration
                {
                    Buckets = Histogram.ExponentialBuckets(0.1, 2, 10) // 0.1s to ~102.4s
                });
            
            // Initialize summary
            _responseTimeSummary = Metrics.CreateSummary(
                "conduit_image_generation_response_time_ms",
                "Response time summary for image generation in milliseconds",
                new SummaryConfiguration
                {
                    LabelNames = new[] { "provider", "model" },
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.5, 0.05),   // p50
                        new QuantileEpsilonPair(0.9, 0.01),   // p90
                        new QuantileEpsilonPair(0.95, 0.01),  // p95
                        new QuantileEpsilonPair(0.99, 0.001)  // p99
                    }
                });
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Prometheus image generation metrics exporter is disabled");
                return Task.CompletedTask;
            }
            
            _logger.LogInformation(
                "Prometheus image generation metrics exporter started - Export interval: {Interval} seconds",
                _options.ExportIntervalSeconds);
            
            // Start the metrics server if configured
            if (_options.StartHttpListener)
            {
                try
                {
                    var server = new MetricServer(hostname: _options.HttpListenerHostname, port: _options.HttpListenerPort);
                    server.Start();
                    
                    _logger.LogInformation(
                        "Prometheus metrics server started on {Hostname}:{Port}",
                        _options.HttpListenerHostname, _options.HttpListenerPort);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start Prometheus metrics server");
                }
            }
            
            // Start export timer
            _exportTimer = new Timer(
                async _ => await ExportMetricsAsync(stoppingToken),
                null,
                TimeSpan.FromSeconds(_options.InitialDelaySeconds),
                TimeSpan.FromSeconds(_options.ExportIntervalSeconds));
            
            return Task.CompletedTask;
        }

        private async Task ExportMetricsAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Get current metrics snapshot
                var snapshot = await _metricsCollector.GetMetricsSnapshotAsync(cancellationToken);
                
                // Export gauge metrics
                ExportGaugeMetrics(snapshot);
                
                // Get provider-specific metrics for the export window
                var providers = snapshot.ProviderStatuses.Keys;
                foreach (var provider in providers)
                {
                    var providerMetrics = await _metricsCollector.GetProviderMetricsAsync(
                        provider, 
                        _options.MetricsWindowMinutes, 
                        cancellationToken);
                    
                    ExportProviderMetrics(providerMetrics);
                }
                
                // Export SLA compliance
                var slaCompliance = await _metricsCollector.GetSlaComplianceAsync(
                    _options.SlaWindowHours, 
                    cancellationToken);
                
                ExportSlaMetrics(slaCompliance);
                
                stopwatch.Stop();
                _logger.LogDebug(
                    "Exported image generation metrics to Prometheus in {Duration}ms",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting image generation metrics to Prometheus");
            }
        }

        private void ExportGaugeMetrics(ImageGenerationMetricsSnapshot snapshot)
        {
            // Active generations
            _activeGenerationsGauge.Set(snapshot.ActiveGenerations);
            
            // System success rate
            _systemSuccessRateGauge.Set(snapshot.SuccessRate);
            
            // Queue metrics
            _queueDepthGauge.WithLabels("normal").Set(snapshot.QueueMetrics.TotalDepth);
            
            if (snapshot.QueueMetrics.QueueDepthByPriority != null)
            {
                foreach (var (priority, depth) in snapshot.QueueMetrics.QueueDepthByPriority)
                {
                    _queueDepthGauge.WithLabels(priority).Set(depth);
                }
            }
            
            // Provider health and response times
            foreach (var (provider, status) in snapshot.ProviderStatuses)
            {
                _providerHealthGauge.WithLabels(provider).Set(status.HealthScore);
                
                if (status.AverageResponseTimeMs > 0)
                {
                    _providerResponseTimeGauge.WithLabels(provider).Set(status.AverageResponseTimeMs);
                }
            }
        }

        private void ExportProviderMetrics(ProviderMetricsSummary providerMetrics)
        {
            var provider = providerMetrics.ProviderName;
            
            // Export counters (these would normally be incremented in real-time)
            // For batch export, we track the deltas
            
            foreach (var (model, modelMetrics) in providerMetrics.ModelBreakdown)
            {
                // Generation counts
                _totalGenerationsCounter
                    .WithLabels(provider, model, "all")
                    .IncTo(modelMetrics.RequestCount);
                
                // Success/failure breakdown
                var successCount = (int)(modelMetrics.RequestCount * (providerMetrics.SuccessRate / 100));
                var failureCount = modelMetrics.RequestCount - successCount;
                
                _successfulGenerationsCounter
                    .WithLabels(provider, model)
                    .IncTo(successCount);
                
                // Cost tracking
                _totalCostCounter
                    .WithLabels(provider, model)
                    .IncTo((double)modelMetrics.TotalCost);
                
                // Response time histogram and summary
                if (modelMetrics.AverageResponseTimeMs > 0)
                {
                    _generationDurationHistogram
                        .WithLabels(provider, model)
                        .Observe(modelMetrics.AverageResponseTimeMs / 1000.0); // Convert to seconds
                    
                    _responseTimeSummary
                        .WithLabels(provider, model)
                        .Observe(modelMetrics.AverageResponseTimeMs);
                }
                
                // Cost histogram
                if (modelMetrics.TotalImages > 0)
                {
                    var avgCostPerImage = modelMetrics.TotalCost / modelMetrics.TotalImages;
                    _imageCostHistogram
                        .WithLabels(provider, model)
                        .Observe((double)avgCostPerImage);
                }
                
                // Image counts by size
                foreach (var (size, count) in modelMetrics.SizeDistribution)
                {
                    _totalImagesCounter
                        .WithLabels(provider, model, size)
                        .IncTo(count);
                }
            }
            
            // Error breakdown
            foreach (var (errorType, count) in providerMetrics.ErrorBreakdown)
            {
                _failedGenerationsCounter
                    .WithLabels(provider, "all", errorType)
                    .IncTo(count);
            }
        }

        private void ExportSlaMetrics(SlaComplianceSummary slaCompliance)
        {
            // Create SLA-specific gauges if needed
            var slaAvailabilityGauge = Metrics.CreateGauge(
                "conduit_image_generation_sla_availability_percent",
                "SLA availability percentage");
            
            var slaResponseTimeGauge = Metrics.CreateGauge(
                "conduit_image_generation_sla_p95_response_time_ms",
                "SLA P95 response time in milliseconds");
            
            var slaErrorRateGauge = Metrics.CreateGauge(
                "conduit_image_generation_sla_error_rate_percent",
                "SLA error rate percentage");
            
            var slaViolationsGauge = Metrics.CreateGauge(
                "conduit_image_generation_sla_violations_total",
                "Total number of SLA violations",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "type" }
                });
            
            // Export SLA metrics
            slaAvailabilityGauge.Set(slaCompliance.AvailabilityPercent);
            slaResponseTimeGauge.Set(slaCompliance.P95ResponseTimeMs);
            slaErrorRateGauge.Set(slaCompliance.ErrorRatePercent);
            
            // Export violations by type
            var violationsByType = slaCompliance.Violations
                .GroupBy(v => v.ViolationType)
                .ToDictionary(g => g.Key, g => g.Count());
            
            foreach (var (violationType, count) in violationsByType)
            {
                slaViolationsGauge.WithLabels(violationType).Set(count);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Prometheus image generation metrics exporter is stopping");
            
            _exportTimer?.Change(Timeout.Infinite, 0);
            _exportTimer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Configuration options for Prometheus image generation metrics.
    /// </summary>
    public class PrometheusImageGenerationOptions
    {
        public bool Enabled { get; set; } = true;
        public int ExportIntervalSeconds { get; set; } = 60;
        public int InitialDelaySeconds { get; set; } = 10;
        public int MetricsWindowMinutes { get; set; } = 60;
        public int SlaWindowHours { get; set; } = 24;
        public bool StartHttpListener { get; set; } = true;
        public string HttpListenerHostname { get; set; } = "localhost";
        public int HttpListenerPort { get; set; } = 9091;
    }
}