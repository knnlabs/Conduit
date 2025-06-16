using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using ConduitLLM.Core.Caching;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Caching
{
    public class CacheMetricsServiceTests
    {
        private readonly CacheMetricsService _metricsService;
        private readonly Mock<ILogger<CacheMetricsService>> _loggerMock;

        public CacheMetricsServiceTests()
        {
            _loggerMock = new Mock<ILogger<CacheMetricsService>>();
            _metricsService = new CacheMetricsService(_loggerMock.Object);
        }

        [Fact]
        public void RecordHit_IncrementsHitCount()
        {
            // Arrange
            double retrievalTime = 10;

            // Act
            _metricsService.RecordHit(retrievalTime);

            // Assert
            Assert.Equal(1, _metricsService.GetTotalHits());
            Assert.InRange(_metricsService.GetAverageRetrievalTimeMs(), retrievalTime - 0.0001, retrievalTime + 0.0001);
        }

        [Fact]
        public void RecordHit_WithModel_IncrementsModelSpecificMetrics()
        {
            // Arrange
            string model = "test-model";
            double retrievalTime = 10.5;

            // Act
            _metricsService.RecordHit(retrievalTime, model);

            // Assert
            var modelMetrics = _metricsService.GetMetricsForModel(model);
            Assert.NotNull(modelMetrics);
            Assert.Equal(1, modelMetrics.Hits);
            Assert.Equal(0, modelMetrics.Misses);
            Assert.Equal((long)retrievalTime, modelMetrics.TotalRetrievalTimeMs);
        }

        [Fact]
        public void RecordMiss_IncrementsMissCount()
        {
            // Act
            _metricsService.RecordMiss();

            // Assert
            Assert.Equal(1, _metricsService.GetTotalMisses());
            Assert.Equal(0, _metricsService.GetTotalHits());
        }

        [Fact]
        public void RecordMiss_WithModel_IncrementsModelSpecificMetrics()
        {
            // Arrange
            string model = "test-model";

            // Act
            _metricsService.RecordMiss(model);

            // Assert
            var modelMetrics = _metricsService.GetMetricsForModel(model);
            Assert.NotNull(modelMetrics);
            Assert.Equal(0, modelMetrics.Hits);
            Assert.Equal(1, modelMetrics.Misses);
            Assert.Equal(0, modelMetrics.TotalRetrievalTimeMs);
        }

        [Fact]
        public void GetTotalRequests_ReturnsSumOfHitsAndMisses()
        {
            // Arrange
            _metricsService.RecordHit(10);
            _metricsService.RecordHit(20);
            _metricsService.RecordMiss();

            // Act
            var totalRequests = _metricsService.GetTotalRequests();

            // Assert
            Assert.Equal(3, totalRequests);
        }

        [Fact]
        public void GetHitRate_ReturnsZero_WhenNoRequests()
        {
            // Act
            var hitRate = _metricsService.GetHitRate();

            // Assert
            Assert.Equal(0, hitRate);
        }

        [Fact]
        public void GetHitRate_ReturnsCorrectRate()
        {
            // Arrange
            _metricsService.RecordHit(10);
            _metricsService.RecordHit(20);
            _metricsService.RecordMiss();
            _metricsService.RecordMiss();

            // Act
            var hitRate = _metricsService.GetHitRate();

            // Assert
            Assert.Equal(0.5, hitRate);
        }

        [Fact]
        public void GetAverageRetrievalTimeMs_ReturnsZero_WhenNoHits()
        {
            // Act
            var avgTime = _metricsService.GetAverageRetrievalTimeMs();

            // Assert
            Assert.Equal(0, avgTime);
        }

        [Fact]
        public void GetAverageRetrievalTimeMs_ReturnsCorrectAverage()
        {
            // Arrange
            _metricsService.RecordHit(10);
            _metricsService.RecordHit(20);

            // Act
            var avgTime = _metricsService.GetAverageRetrievalTimeMs();

            // Assert
            Assert.Equal(15, avgTime);
        }

        [Fact]
        public void GetModelMetrics_ReturnsAllModelMetrics()
        {
            // Arrange
            _metricsService.RecordHit(10, "model1");
            _metricsService.RecordHit(20, "model2");
            _metricsService.RecordMiss("model1");
            _metricsService.RecordMiss("model3");

            // Act
            var modelMetrics = _metricsService.GetModelMetrics();

            // Assert
            Assert.Equal(3, modelMetrics.Count);
            Assert.True(modelMetrics.ContainsKey("model1"));
            Assert.True(modelMetrics.ContainsKey("model2"));
            Assert.True(modelMetrics.ContainsKey("model3"));

            Assert.Equal(1, modelMetrics["model1"].Hits);
            Assert.Equal(1, modelMetrics["model1"].Misses);
            Assert.Equal(10, modelMetrics["model1"].TotalRetrievalTimeMs);

            Assert.Equal(1, modelMetrics["model2"].Hits);
            Assert.Equal(0, modelMetrics["model2"].Misses);
            Assert.Equal(20, modelMetrics["model2"].TotalRetrievalTimeMs);

            Assert.Equal(0, modelMetrics["model3"].Hits);
            Assert.Equal(1, modelMetrics["model3"].Misses);
            Assert.Equal(0, modelMetrics["model3"].TotalRetrievalTimeMs);
        }

        [Fact]
        public void GetMetricsForModel_ReturnsNull_WhenModelNotTracked()
        {
            // Act
            var metrics = _metricsService.GetMetricsForModel("non-existent-model");

            // Assert
            Assert.Null(metrics);
        }

        [Fact]
        public void GetTrackedModels_ReturnsAllTrackedModels()
        {
            // Arrange
            _metricsService.RecordHit(10, "model1");
            _metricsService.RecordHit(20, "model2");
            _metricsService.RecordMiss("model3");

            // Act
            var trackedModels = _metricsService.GetTrackedModels();

            // Assert
            Assert.Equal(3, trackedModels.Count);
            Assert.Contains("model1", trackedModels);
            Assert.Contains("model2", trackedModels);
            Assert.Contains("model3", trackedModels);
        }

        [Fact]
        public void Reset_ClearsAllMetrics()
        {
            // Arrange
            _metricsService.RecordHit(10, "model1");
            _metricsService.RecordMiss("model2");

            // Act
            _metricsService.Reset();

            // Assert
            Assert.Equal(0, _metricsService.GetTotalHits());
            Assert.Equal(0, _metricsService.GetTotalMisses());
            Assert.Equal(0, _metricsService.GetTotalRequests());
            Assert.Empty(_metricsService.GetTrackedModels());
        }

        [Fact]
        public void ImportStats_ImportsBasicMetrics()
        {
            // Arrange
            long hits = 100;
            long misses = 50;
            double avgResponseTime = 15.5;

            // Act
            _metricsService.ImportStats(hits, misses, avgResponseTime);

            // Assert
            Assert.Equal(hits, _metricsService.GetTotalHits());
            Assert.Equal(misses, _metricsService.GetTotalMisses());
            Assert.Equal(hits + misses, _metricsService.GetTotalRequests());
            Assert.Equal(avgResponseTime, _metricsService.GetAverageRetrievalTimeMs());
        }

        [Fact]
        public void ImportStats_ImportsModelMetrics()
        {
            // Arrange
            long hits = 100;
            long misses = 50;
            double avgResponseTime = 15.5;

            var modelMetrics = new Dictionary<string, ModelCacheMetrics>
            {
                ["model1"] = new ModelCacheMetrics { Hits = 60, Misses = 20, TotalRetrievalTimeMs = 900 },
                ["model2"] = new ModelCacheMetrics { Hits = 40, Misses = 30, TotalRetrievalTimeMs = 650 }
            };

            // Act
            _metricsService.ImportStats(hits, misses, avgResponseTime, modelMetrics);

            // Assert
            var importedMetrics = _metricsService.GetModelMetrics();
            Assert.Equal(2, importedMetrics.Count);

            Assert.Equal(60, importedMetrics["model1"].Hits);
            Assert.Equal(20, importedMetrics["model1"].Misses);
            Assert.Equal(900, importedMetrics["model1"].TotalRetrievalTimeMs);

            Assert.Equal(40, importedMetrics["model2"].Hits);
            Assert.Equal(30, importedMetrics["model2"].Misses);
            Assert.Equal(650, importedMetrics["model2"].TotalRetrievalTimeMs);
        }

        [Fact]
        public void ImportStats_SkipsImport_WhenExistingData()
        {
            // Arrange
            _metricsService.RecordHit(10);

            long hits = 100;
            long misses = 50;
            double avgResponseTime = 15.5;

            // Act
            _metricsService.ImportStats(hits, misses, avgResponseTime);

            // Assert
            Assert.Equal(1, _metricsService.GetTotalHits()); // Should still be 1, not 100
            Assert.Equal(0, _metricsService.GetTotalMisses()); // Should still be 0, not 50
        }

        [Fact]
        public void ImportStats_SkipsImport_WhenInvalidData()
        {
            // Arrange
            long hits = -1; // Invalid
            long misses = 50;
            double avgResponseTime = 15.5;

            // Act
            _metricsService.ImportStats(hits, misses, avgResponseTime);

            // Assert
            Assert.Equal(0, _metricsService.GetTotalHits()); // Should not import
            Assert.Equal(0, _metricsService.GetTotalMisses()); // Should not import
        }

        [Fact]
        public void ModelCacheMetrics_GetHitRate_CalculatesCorrectly()
        {
            // Arrange
            var metrics = new ModelCacheMetrics
            {
                Hits = 75,
                Misses = 25
            };

            // Act
            double hitRate = metrics.GetHitRate();

            // Assert
            Assert.Equal(0.75, hitRate);
        }

        [Fact]
        public void ModelCacheMetrics_GetHitRate_ReturnsZero_WhenNoRequests()
        {
            // Arrange
            var metrics = new ModelCacheMetrics
            {
                Hits = 0,
                Misses = 0
            };

            // Act
            double hitRate = metrics.GetHitRate();

            // Assert
            Assert.Equal(0, hitRate);
        }

        [Fact]
        public void ModelCacheMetrics_GetAverageRetrievalTimeMs_CalculatesCorrectly()
        {
            // Arrange
            var metrics = new ModelCacheMetrics
            {
                Hits = 10,
                TotalRetrievalTimeMs = 250
            };

            // Act
            double avgTime = metrics.GetAverageRetrievalTimeMs();

            // Assert
            Assert.Equal(25, avgTime);
        }

        [Fact]
        public void ModelCacheMetrics_GetAverageRetrievalTimeMs_ReturnsZero_WhenNoHits()
        {
            // Arrange
            var metrics = new ModelCacheMetrics
            {
                Hits = 0,
                TotalRetrievalTimeMs = 0
            };

            // Act
            double avgTime = metrics.GetAverageRetrievalTimeMs();

            // Assert
            Assert.Equal(0, avgTime);
        }
    }
}
