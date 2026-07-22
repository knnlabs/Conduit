using System.Globalization;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

public partial class AdminModelCostServiceTests
{
    [Fact]
    public async Task CsvExportImport_RoundTripsQuotedJsonNewlinesAndInvariantDecimals()
    {
        const string pricingConfiguration = """
            {
              "baseRate": 0.09,
              "resolutionMultipliers": { "720p": 1.0, "1080p": 1.5 }
            }
            """;
        var source = new ModelCost
        {
            CostName = "Video, Premium \"Tier\"",
            PricingModel = PricingModel.PerSecondVideo,
            PricingConfiguration = pricingConfiguration,
            InputCostPerMillionTokens = 1.25m,
            OutputCostPerMillionTokens = 2.5m,
            EmbeddingCostPerMillionTokens = 0.125m,
            BatchProcessingMultiplier = 0.75m,
            SupportsBatchProcessing = true,
            CostPerSearchUnit = 0.0125m,
            CachedInputCostPerMillionTokens = 0.625m,
            CachedInputWriteCostPerMillionTokens = 0.875m
        };
        _mockModelCostRepository
            .Setup(x => x.GetPaginatedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModelCost> { source }, 1));

        ModelCost? imported = null;
        _mockModelCostRepository
            .Setup(x => x.GetByCostNameAsync(source.CostName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelCost?)null);
        _mockModelCostRepository
            .Setup(x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()))
            .Callback<ModelCost, CancellationToken>((modelCost, _) => imported = modelCost)
            .ReturnsAsync(123);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var csv = await _service.ExportModelCostsAsync("csv");
            var result = await _service.ImportModelCostsAsync(csv, "csv");

            csv.Should().Contain("1.250000");
            result.SuccessCount.Should().Be(1);
            result.FailureCount.Should().Be(0);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        imported.Should().NotBeNull();
        imported!.CostName.Should().Be(source.CostName);
        imported.PricingModel.Should().Be(source.PricingModel);
        imported.PricingConfiguration.Should().Be(pricingConfiguration);
        imported.InputCostPerMillionTokens.Should().Be(1.25m);
        imported.OutputCostPerMillionTokens.Should().Be(2.5m);
        imported.EmbeddingCostPerMillionTokens.Should().Be(0.125m);
        imported.BatchProcessingMultiplier.Should().Be(0.75m);
        imported.SupportsBatchProcessing.Should().BeTrue();
        imported.CostPerSearchUnit.Should().Be(0.0125m);
        imported.CachedInputCostPerMillionTokens.Should().Be(0.625m);
        imported.CachedInputWriteCostPerMillionTokens.Should().Be(0.875m);
    }

    [Fact]
    public async Task CsvImport_WithInvalidRequiredDecimal_FailsInsteadOfSubstitutingZero()
    {
        const string csv = """
            Cost Name,Pricing Model,Pricing Configuration,Input Cost (per million tokens),Output Cost (per million tokens),Embedding Cost (per million tokens),Batch Processing Multiplier,Supports Batch Processing,Search Unit Cost (per 1K units),Cached Input Cost (per million tokens),Cached Write Cost (per million tokens)
            broken,Standard,,not-a-number,2.000000,,,No,,,
            """;

        var result = await _service.ImportModelCostsAsync(csv, "csv");

        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(1);
        result.Errors.Should().ContainSingle(error => error.Contains("input cost", StringComparison.OrdinalIgnoreCase));
        _mockModelCostRepository.Verify(
            x => x.CreateAsync(It.IsAny<ModelCost>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
