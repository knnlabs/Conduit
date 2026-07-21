using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.PromptCaching;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class PromptCachingEndpointsTests : IDisposable
{
    private readonly Mock<IAdminGlobalSettingService> _settings = new();
    private readonly Mock<IGlobalSettingsCacheService> _cache = new();
    private readonly AdminEndpointTestHost _host;

    public PromptCachingEndpointsTests()
    {
        _host = AdminEndpointTestHost.Create(services =>
        {
            services.AddSingleton(_settings.Object);
            services.AddSingleton(_cache.Object);
        }, endpoints => endpoints.MapPromptCachingEndpoints());
    }

    [Fact]
    public async Task GetConfig_Missing_ReturnsDisabledV3()
    {
        _cache.Setup(cache => cache.GetSettingValueAsync("PromptCaching.Config"))
            .ReturnsAsync((string?)null);
        var response = await _host.Client.GetAsync("/api/prompt-caching/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PromptCachingConfigDto>();
        Assert.Equal(3, dto!.SchemaVersion);
        Assert.False(dto.Enabled);
    }

    [Fact]
    public async Task GetConfig_LegacyShape_ReturnsConflict()
    {
        _cache.Setup(cache => cache.GetSettingValueAsync("PromptCaching.Config"))
            .ReturnsAsync("{\"auto_inject_enabled\":true}");
        var response = await _host.Client.GetAsync("/api/prompt-caching/config");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfig_ValidRule_PersistsV3()
    {
        _settings.Setup(settings => settings.GetSettingByKeyAsync("PromptCaching.Config"))
            .ReturnsAsync(new GlobalSettingDto { Id = 1, Key = "PromptCaching.Config", Value = "{}" });
        var input = new UpdatePromptCachingConfigDto
        {
            Enabled = true,
            Rules = [new PromptCachingRuleDto
            {
                Name = "Claude",
                Provider = "OpenRouter",
                ModelPattern = "anthropic/*",
                Strategy = "Automatic",
                Ttl = "5m"
            }]
        };
        var response = await _host.Client.PutAsJsonAsync("/api/prompt-caching/config", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _settings.Verify(settings => settings.UpdateSettingByKeyAsync(
            It.Is<UpdateGlobalSettingByKeyDto>(setting => setting.Value.Contains("schema_version"))), Times.Once);
        _cache.Verify(cache => cache.InvalidateSettingAsync("PromptCaching.Config"), Times.Once);
    }

    [Fact]
    public async Task UpdateConfig_UnsupportedProvider_ReturnsBadRequest()
    {
        var response = await _host.Client.PutAsJsonAsync("/api/prompt-caching/config",
            new UpdatePromptCachingConfigDto
            {
                Enabled = true,
                Rules = [new PromptCachingRuleDto
                {
                    Name = "Unsafe",
                    Provider = "Replicate",
                    ModelPattern = "*",
                    Strategy = "Automatic"
                }]
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCapabilities_IncludesManagedAndProviderManagedEntries()
    {
        var response = await _host.Client.GetAsync("/api/prompt-caching/capabilities");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(document.RootElement.EnumerateArray(), item =>
            item.GetProperty("modelPattern").GetString() == "anthropic/*"
            && !item.GetProperty("providerManaged").GetBoolean());
        Assert.Contains(document.RootElement.EnumerateArray(), item =>
            item.GetProperty("providerManaged").GetBoolean());
    }

    public void Dispose() => _host.Dispose();
}
