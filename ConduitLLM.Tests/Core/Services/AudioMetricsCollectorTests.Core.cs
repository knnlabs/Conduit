using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioMetricsCollectorTests : IDisposable
    {
        private readonly Mock<ILogger<AudioMetricsCollector>> _loggerMock;
        private readonly Mock<IAudioAlertingService> _alertingServiceMock;
        private readonly AudioMetricsOptions _options;
        private readonly AudioMetricsCollector _collector;

        public AudioMetricsCollectorTests()
        {
            _loggerMock = new Mock<ILogger<AudioMetricsCollector>>();
            _alertingServiceMock = new Mock<IAudioAlertingService>();
            _options = new AudioMetricsOptions
            {
                AggregationInterval = TimeSpan.FromMilliseconds(100),
                RetentionPeriod = TimeSpan.FromMinutes(5),
                TranscriptionLatencyThreshold = 5000,
                RealtimeLatencyThreshold = 200
            };

            _collector = new AudioMetricsCollector(
                _loggerMock.Object,
                Options.Create(_options),
                _alertingServiceMock.Object);
        }

        public void Dispose()
        {
            _collector.Dispose();
        }
    }
}