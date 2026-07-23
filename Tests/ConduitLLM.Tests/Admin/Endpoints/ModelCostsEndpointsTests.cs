using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class ModelCostsEndpointsTests
{
    private const int ModelCostId = 42;

    [Fact]
    public async Task ValidatePricingRules_UsesModelOverrideAndIgnoresCallerSchema()
    {
        using var host = CreateHost(new ModelSeed(
            1,
            "override-model",
            """{"quality":{"type":"select","options":[{"value":"hd","label":"HD"}]}}""",
            """{"resolution":{"type":"select","options":[{"value":"1080p","label":"1080p"}]}}"""));

        var result = await ValidateAsync(
            host,
            CreatePricingConfiguration("quality", "hd"),
            ParseObject("""{"quality":{"type":"select","options":[{"value":"standard","label":"Standard"}]}}"""));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidatePricingRulesStandalone_UsesCallerSchema()
    {
        using var host = CreateHost();
        var response = await host.Client.PostAsJsonAsync(
            "/v1/admin/model-costs/validate-pricing-rules",
            new ValidatePricingRulesRequest
            {
                PricingConfiguration = CreatePricingConfiguration("quality", "hd"),
                ParameterSchema =
                    ParseObject("""{"quality":{"type":"select","options":[{"value":"standard","label":"Standard"}]}}""")
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ValidationResult>(
            await response.Content.ReadFromJsonAsync<ValidationResult>());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == "quality");
    }

    [Fact]
    public async Task ValidatePricingRules_FallsBackToSeriesSchema()
    {
        using var host = CreateHost(new ModelSeed(
            1,
            "series-model",
            null,
            """{"resolution":{"type":"select","options":[{"value":"720p","label":"720p"}]}}"""));

        var result = await ValidateAsync(host, CreatePricingConfiguration("resolution", "1080p"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Field == "resolution"
            && error.Message.Contains("series-model", StringComparison.Ordinal)
            && error.Message.Contains("not in allowed options", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidatePricingRules_SharedCostMustValidateForEveryDistinctModel()
    {
        using var host = CreateHost(
            new ModelSeed(
                1,
                "first-model",
                """{"quality":{"type":"select","options":[{"value":"hd","label":"HD"}]}}""",
                "{}"),
            new ModelSeed(
                2,
                "incompatible-model",
                """{"quality":{"type":"select","options":[{"value":"standard","label":"Standard"}]}}""",
                "{}"));

        var result = await ValidateAsync(host, CreatePricingConfiguration("quality", "hd"));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("incompatible-model", result.Errors[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("first-model", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidatePricingRules_UnknownParameterNamesAreModelSpecificWarnings()
    {
        using var host = CreateHost(new ModelSeed(
            1,
            "known-schema-model",
            null,
            """{"resolution":{"type":"string"}}"""));

        var result = await ValidateAsync(host, CreatePricingConfiguration("resoluton", "1080p"));

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, warning =>
            warning.Contains("known-schema-model", StringComparison.Ordinal)
            && warning.Contains("Condition 'resoluton' not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidatePricingRules_MissingAssociationsIsAnError()
    {
        using var host = CreateHost();

        var result = await ValidateAsync(host, CreatePricingConfiguration("resolution", "1080p"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Field == "modelCostId"
            && error.Message.Contains("no associated models", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{}", "empty")]
    [InlineData("{ invalid json }", "invalid JSON")]
    public async Task ValidatePricingRules_EmptyOrInvalidPersistedSchemaIsAnError(
        string persistedSchema,
        string expectedMessage)
    {
        using var host = CreateHost(new ModelSeed(1, "bad-schema-model", persistedSchema, "{}"));

        var result = await ValidateAsync(host, CreatePricingConfiguration("resolution", "1080p"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Field == "parameterSchema"
            && error.Message.Contains("bad-schema-model", StringComparison.Ordinal)
            && error.Message.Contains(expectedMessage, StringComparison.Ordinal));
    }

    private static AdminEndpointTestHost CreateHost(params ModelSeed[] models)
    {
        var database = new SqliteTestDatabase();
        var options = database.Options;

        using (var dbContext = new ConduitDbContext(options))
        {
            var cost = new ModelCost { Id = ModelCostId, CostName = "Shared pricing" };
            dbContext.ModelCosts.Add(cost);

            foreach (var seed in models)
            {
                var author = new ModelAuthor
                {
                    Id = 1000 + seed.Id,
                    Name = $"Author {seed.Id}"
                };
                var series = new ModelSeries
                {
                    Id = 100 + seed.Id,
                    AuthorId = author.Id,
                    Author = author,
                    Name = $"Series {seed.Id}",
                    Parameters = seed.SeriesParameters
                };
                var model = new Model
                {
                    Id = seed.Id,
                    Name = seed.Name,
                    ModelSeriesId = series.Id,
                    Series = series,
                    ModelParameters = seed.ModelParameters
                };
                var association = new ModelProviderTypeAssociation
                {
                    Id = 200 + seed.Id,
                    Identifier = $"model-{seed.Id}",
                    ModelId = model.Id,
                    Model = model,
                    ModelCostId = cost.Id,
                    ModelCost = cost
                };

                dbContext.ModelProviderTypeAssociations.Add(association);
            }

            dbContext.SaveChanges();
        }

        var modelCostService = new Mock<IAdminModelCostService>();
        modelCostService.Setup(service => service.GetModelCostByIdAsync(ModelCostId))
            .ReturnsAsync(new ModelCostDto { Id = ModelCostId, CostName = "Shared pricing" });

        var factory = new TestDbContextFactory(options);
        return AdminEndpointTestHost.Create(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSingleton(modelCostService.Object);
            services.AddSingleton<IDbContextFactory<ConduitDbContext>>(factory);
            services.AddSingleton<IPricingRulesValidator, PricingRulesValidator>();
            services.AddScoped<ModelCostsEndpoints>();
        }, endpoints => ModelCostsEndpoints.MapModelCostsEndpoints(endpoints), database);
    }

    private static async Task<ValidationResult> ValidateAsync(
        AdminEndpointTestHost host,
        Dictionary<string, JsonElement> pricingConfiguration,
        Dictionary<string, JsonElement>? callerSchema = null)
    {
        var response = await host.Client.PostAsJsonAsync(
            $"/v1/admin/model-costs/{ModelCostId}/validate-pricing-rules",
            new ValidatePricingRulesRequest
            {
                PricingConfiguration = pricingConfiguration,
                ParameterSchema = callerSchema
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ValidationResult>(
            await response.Content.ReadFromJsonAsync<ValidationResult>());
    }

    private static Dictionary<string, JsonElement> CreatePricingConfiguration(string parameter, object value) =>
        ParseObject(JsonSerializer.Serialize(new
        {
            version = "1.0",
            pricingType = "per_unit",
            unitField = "ImageCount",
            defaultRate = 1m,
            rules = new[]
            {
                new
                {
                    priority = 1,
                    conditions = new Dictionary<string, object> { [parameter] = value },
                    rate = 1m
                }
            }
        }));

    private static Dictionary<string, JsonElement> ParseObject(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private sealed record ModelSeed(
        int Id,
        string Name,
        string? ModelParameters,
        string SeriesParameters);

    private sealed class TestDbContextFactory(DbContextOptions<ConduitDbContext> options)
        : IDbContextFactory<ConduitDbContext>
    {
        public ConduitDbContext CreateDbContext() => new(options);

        public Task<ConduitDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
