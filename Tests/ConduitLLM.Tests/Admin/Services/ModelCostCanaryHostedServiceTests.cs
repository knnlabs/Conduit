using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestInfrastructure;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Admin.Services;

[Trait("Category", "Unit")]
[Trait("Component", "Admin")]
public sealed class ModelCostCanaryHostedServiceTests
{
    [Fact]
    public async Task RunOnceAsync_ChecksOnlyActiveMappings()
    {
        using var fixture = new CanaryFixture();
        fixture.AddMapping("active", enabled: true, cost: StandardCost(1));
        fixture.AddMapping("disabled", enabled: false, cost: StandardCost(2));
        fixture.AddMapping("provider-disabled", enabled: true, cost: StandardCost(3), providerEnabled: false);
        fixture.AddMapping("association-disabled", enabled: true, cost: StandardCost(4), associationEnabled: false);
        fixture.CostService
            .Setup(service => service.CalculateCostByIdAsync(1, It.IsAny<Usage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2m);

        var result = await fixture.CreateService().RunOnceAsync();

        result.Should().Be(new ModelCostCanaryRunResult(1, 0));
        fixture.CostService.Verify(service => service.CalculateCostByIdAsync(
            1, It.IsAny<Usage>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.CostService.VerifyNoOtherCalls();
        fixture.AuditService.Verify(service => service.LogBillingEventAsync(It.IsAny<BillingAuditEvent>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_MissingCost_WritesActionableAuditEvent()
    {
        using var fixture = new CanaryFixture();
        fixture.AddMapping("unpriced", enabled: true, cost: null);

        var result = await fixture.CreateService().RunOnceAsync();

        result.Should().Be(new ModelCostCanaryRunResult(1, 1));
        fixture.CostService.VerifyNoOtherCalls();
        fixture.AuditService.Verify(service => service.LogBillingEventAsync(It.Is<BillingAuditEvent>(audit =>
            audit.EventType == BillingAuditEventType.ModelCostCanaryFailed &&
            audit.Model == "unpriced" &&
            audit.FailureReason!.Contains("No ModelCost") &&
            audit.MetadataJson!.Contains("missing_cost"))), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_ZeroCalculatedCost_IsFailure()
    {
        using var fixture = new CanaryFixture();
        fixture.AddMapping("zero", enabled: true, cost: StandardCost(1));
        fixture.CostService
            .Setup(service => service.CalculateCostByIdAsync(1, It.IsAny<Usage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var result = await fixture.CreateService().RunOnceAsync();

        result.FailedCount.Should().Be(1);
        fixture.AuditService.Verify(service => service.LogBillingEventAsync(It.Is<BillingAuditEvent>(audit =>
            audit.MetadataJson!.Contains("zero_cost"))), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_InvalidPricingJson_FailsBeforeCalculation()
    {
        using var fixture = new CanaryFixture();
        fixture.AddMapping("bad-image", enabled: true, cost: new ModelCost
        {
            Id = 1,
            CostName = "bad-image",
            PricingModel = PricingModel.PerImage,
            PricingConfiguration = "{}",
            IsActive = true
        });

        var result = await fixture.CreateService().RunOnceAsync();

        result.FailedCount.Should().Be(1);
        fixture.CostService.VerifyNoOtherCalls();
        fixture.AuditService.Verify(service => service.LogBillingEventAsync(It.Is<BillingAuditEvent>(audit =>
            audit.MetadataJson!.Contains("invalid_configuration"))), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_AuditSinkFailure_DoesNotSkipRemainingModels()
    {
        using var fixture = new CanaryFixture();
        fixture.AddMapping("first", enabled: true, cost: StandardCost(1));
        fixture.AddMapping("second", enabled: true, cost: StandardCost(2));
        fixture.CostService
            .Setup(service => service.CalculateCostByIdAsync(It.IsAny<int>(), It.IsAny<Usage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        fixture.AuditService
            .Setup(service => service.LogBillingEventAsync(It.IsAny<BillingAuditEvent>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        var result = await fixture.CreateService().RunOnceAsync();

        result.Should().Be(new ModelCostCanaryRunResult(2, 2));
        fixture.CostService.Verify(service => service.CalculateCostByIdAsync(
            It.IsAny<int>(), It.IsAny<Usage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void CreateSyntheticUsage_PerVideo_UsesConfiguredRateKey()
    {
        var cost = new ModelCost
        {
            PricingModel = PricingModel.PerVideo,
            PricingConfiguration = "{\"rates\":{\"1080p_6\":0.5}}"
        };

        var usage = ModelCostCanaryHostedService.CreateSyntheticUsage(cost);

        usage.VideoResolution.Should().Be("1080p");
        usage.VideoDurationSeconds.Should().Be(6);
    }

    [Theory]
    [InlineData(PricingModel.Standard)]
    [InlineData(PricingModel.PerSecondVideo)]
    [InlineData(PricingModel.InferenceSteps)]
    [InlineData(PricingModel.TieredTokens)]
    [InlineData(PricingModel.PerImage)]
    [InlineData(PricingModel.RulesBased)]
    public void CreateSyntheticUsage_AllPricingStrategiesHavePositiveBillableDimensions(PricingModel pricingModel)
    {
        var usage = ModelCostCanaryHostedService.CreateSyntheticUsage(new ModelCost { PricingModel = pricingModel });

        usage.PromptTokens.Should().BePositive();
        usage.CompletionTokens.Should().BePositive();
        usage.ImageCount.Should().BePositive();
        usage.VideoDurationSeconds.Should().BePositive();
        usage.InferenceSteps.Should().BePositive();
        usage.SearchUnits.Should().BePositive();
    }

    private static ModelCost StandardCost(int id) => new()
    {
        Id = id,
        CostName = $"cost-{id}",
        PricingModel = PricingModel.Standard,
        InputCostPerMillionTokens = 1m,
        OutputCostPerMillionTokens = 1m,
        IsActive = true,
        EffectiveDate = DateTime.UtcNow.AddDays(-1)
    };

    private sealed class CanaryFixture : IDisposable
    {
        private readonly SqliteTestDatabase _database = new();

        internal Mock<ICostCalculationService> CostService { get; } = new(MockBehavior.Strict);
        internal Mock<IBillingAuditService> AuditService { get; } = new(MockBehavior.Strict);

        internal CanaryFixture()
        {
            AuditService.Setup(service => service.LogBillingEventAsync(It.IsAny<BillingAuditEvent>()))
                .Returns(Task.CompletedTask);
        }

        internal void AddMapping(
            string alias,
            bool enabled,
            ModelCost? cost,
            bool providerEnabled = true,
            bool associationEnabled = true)
        {
            using var context = _database.CreateContext();
            var id = context.ModelProviderMappings.Count() + 1;
            var provider = new Provider
            {
                Id = id,
                ProviderName = $"provider-{id}",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = providerEnabled
            };
            var author = new ModelAuthor
            {
                Id = id,
                Name = $"canary-author-{id}"
            };
            var series = new ModelSeries
            {
                Id = id,
                Author = author,
                Name = $"canary-series-{id}",
                Parameters = "{}"
            };
            var model = new Model
            {
                Id = id,
                Name = $"model-{id}",
                Series = series
            };
            if (cost != null)
                context.ModelCosts.Add(cost);
            context.Providers.Add(provider);
            context.Models.Add(model);
            context.ModelProviderTypeAssociations.Add(new ModelProviderTypeAssociation
            {
                Id = id,
                ModelId = id,
                Identifier = alias,
                IsEnabled = associationEnabled,
                ModelCostId = cost?.Id
            });
            context.ModelProviderMappings.Add(new ModelProviderMapping
            {
                Id = id,
                ModelAlias = alias,
                ProviderModelId = alias,
                ProviderId = id,
                ModelProviderTypeAssociationId = id,
                IsEnabled = enabled
            });
            context.SaveChanges();
        }

        internal ModelCostCanaryHostedService CreateService()
        {
            var factory = new Mock<IDbContextFactory<ConduitDbContext>>();
            factory.Setup(item => item.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _database.CreateContext());

            var services = new ServiceCollection();
            services.AddSingleton(factory.Object);
            services.AddScoped(_ => CostService.Object);
            services.AddScoped(_ => AuditService.Object);
            var provider = services.BuildServiceProvider();

            return new ModelCostCanaryHostedService(
                provider,
                Options.Create(new BillingCostCanaryOptions()),
                Mock.Of<ILogger<ModelCostCanaryHostedService>>());
        }

        public void Dispose() => _database.Dispose();
    }
}
