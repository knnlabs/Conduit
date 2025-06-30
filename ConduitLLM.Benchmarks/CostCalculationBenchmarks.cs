using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class CostCalculationBenchmarks
    {
        private CostCalculationService _costCalculationService = null!;
        private AudioCostCalculationService _audioCostCalculationService = null!;
        private Mock<IModelCostService> _modelCostServiceMock = null!;
        private Mock<IServiceProvider> _serviceProviderMock = null!;
        
        private const string ModelId = "openai/gpt-4";
        private const string AudioProvider = "openai";
        private const string AudioModel = "whisper-1";
        
        private readonly Usage _simpleUsage = new()
        {
            PromptTokens = 1000,
            CompletionTokens = 500,
            TotalTokens = 1500
        };
        
        private readonly Usage _complexUsage = new()
        {
            PromptTokens = 10000,
            CompletionTokens = 5000,
            TotalTokens = 15000,
            ImageCount = 5,
            VideoDurationSeconds = 30,
            VideoResolution = "1920x1080"
        };
        
        private readonly ModelCostInfo _modelCost = new()
        {
            ModelIdPattern = ModelId,
            InputTokenCost = 0.00003m,
            OutputTokenCost = 0.00006m,
            EmbeddingTokenCost = 0.0000001m,
            ImageCostPerImage = 0.04m,
            VideoCostPerSecond = 0.05m,
            VideoResolutionMultipliers = new Dictionary<string, decimal>
            {
                ["1920x1080"] = 1.5m,
                ["1280x720"] = 1.0m
            }
        };

        [GlobalSetup]
        public void Setup()
        {
            // Setup mocks
            _modelCostServiceMock = new Mock<IModelCostService>();
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_modelCost);
            
            var loggerMock = new Mock<ILogger<CostCalculationService>>();
            _costCalculationService = new CostCalculationService(_modelCostServiceMock.Object, loggerMock.Object);
            
            // Setup audio service
            _serviceProviderMock = new Mock<IServiceProvider>();
            var audioLoggerMock = new Mock<ILogger<AudioCostCalculationService>>();
            _audioCostCalculationService = new AudioCostCalculationService(_serviceProviderMock.Object, audioLoggerMock.Object);
        }

        [Benchmark(Baseline = true)]
        public async Task<decimal> CostCalculation_SimpleUsage()
        {
            return await _costCalculationService.CalculateCostAsync(ModelId, _simpleUsage);
        }
        
        [Benchmark]
        public async Task<decimal> CostCalculation_ComplexUsage()
        {
            return await _costCalculationService.CalculateCostAsync(ModelId, _complexUsage);
        }
        
        [Benchmark]
        public async Task<decimal> CostCalculation_EmbeddingOnly()
        {
            var embeddingUsage = new Usage
            {
                PromptTokens = 5000,
                CompletionTokens = 0,
                TotalTokens = 5000
            };
            return await _costCalculationService.CalculateCostAsync(ModelId, embeddingUsage);
        }
        
        [Benchmark]
        public async Task<object> AudioCostCalculation_Transcription()
        {
            return await _audioCostCalculationService.CalculateTranscriptionCostAsync(
                AudioProvider, AudioModel, 300); // 5 minutes
        }
        
        [Benchmark]
        public async Task<object> AudioCostCalculation_TextToSpeech()
        {
            return await _audioCostCalculationService.CalculateTextToSpeechCostAsync(
                AudioProvider, "tts-1", 10000); // 10k characters
        }
        
        [Benchmark]
        public async Task<object> AudioCostCalculation_Realtime()
        {
            return await _audioCostCalculationService.CalculateRealtimeCostAsync(
                AudioProvider, "gpt-4o-realtime-preview", 180, 120, 5000, 3000);
        }
        
        // Batch processing benchmark
        [Benchmark]
        [Arguments(10)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task CostCalculation_BatchProcessing(int batchSize)
        {
            var tasks = new Task<decimal>[batchSize];
            for (int i = 0; i < batchSize; i++)
            {
                tasks[i] = _costCalculationService.CalculateCostAsync(ModelId, _simpleUsage);
            }
            await Task.WhenAll(tasks);
        }
    }
}