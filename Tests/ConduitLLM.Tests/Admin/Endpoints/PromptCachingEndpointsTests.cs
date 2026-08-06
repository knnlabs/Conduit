using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;

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
        var response = await _host.Client.GetAsync("/v1/admin/prompt-cache-settings/config");
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
        var response = await _host.Client.GetAsync("/v1/admin/prompt-cache-settings/config");
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
            Rules = [new PromptCachingRule
            {
                Name = "Claude",
                Provider = "OpenRouter",
                ModelPattern = "anthropic/*",
                Strategy = PromptCachingStrategy.Automatic,
                Ttl = "5m"
            }]
        };
        var response = await _host.Client.PutAsJsonAsync("/v1/admin/prompt-cache-settings/config", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _settings.Verify(settings => settings.UpdateSettingByKeyAsync(
            It.Is<UpdateGlobalSettingByKeyDto>(setting => setting.Value.Contains("schema_version"))), Times.Once);
        _cache.Verify(cache => cache.InvalidateSettingAsync("PromptCaching.Config"), Times.Once);
    }

    [Fact]
    public async Task UpdateConfig_UnsupportedProvider_ReturnsBadRequest()
    {
        var response = await _host.Client.PutAsJsonAsync("/v1/admin/prompt-cache-settings/config",
            new UpdatePromptCachingConfigDto
            {
                Enabled = true,
                Rules = [new PromptCachingRule
                {
                    Name = "Unsafe",
                    Provider = "Replicate",
                    ModelPattern = "*",
                    Strategy = PromptCachingStrategy.Automatic
                }]
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCapabilities_IncludesManagedAndProviderManagedEntries()
    {
        var response = await _host.Client.GetAsync("/v1/admin/prompt-cache-settings/capabilities");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var capabilities = document.RootElement.GetProperty("data").EnumerateArray();
        Assert.Contains(capabilities, item =>
            item.GetProperty("modelPattern").GetString() == "anthropic/*"
            && !item.GetProperty("providerManaged").GetBoolean());
        Assert.Contains(document.RootElement.GetProperty("data").EnumerateArray(), item =>
            item.GetProperty("providerManaged").GetBoolean());
    }

    public void Dispose() => _host.Dispose();
}
