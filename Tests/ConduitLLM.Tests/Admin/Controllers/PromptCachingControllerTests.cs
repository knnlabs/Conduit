using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.PromptCaching;
using ConduitLLM.Configuration.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.Controllers;

public class PromptCachingControllerTests
{
    private readonly Mock<IAdminGlobalSettingService> _settings = new();
    private readonly Mock<IGlobalSettingsCacheService> _cache = new();
    private readonly PromptCachingController _controller;

    public PromptCachingControllerTests()
    {
        _controller = new PromptCachingController(_settings.Object, _cache.Object,
            Mock.Of<ILogger<PromptCachingController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task GetConfig_Missing_ReturnsDisabledV2()
    {
        _cache.Setup(x => x.GetSettingValueAsync("PromptCaching.Config")).ReturnsAsync((string?)null);

        var result = await _controller.GetConfig();

        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<PromptCachingConfigDto>().Subject;
        dto.SchemaVersion.Should().Be(2);
        dto.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetConfig_LegacyShape_ReturnsConflict()
    {
        _cache.Setup(x => x.GetSettingValueAsync("PromptCaching.Config"))
            .ReturnsAsync("{\"auto_inject_enabled\":true}");

        (await _controller.GetConfig()).Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UpdateConfig_ValidRule_PersistsV2()
    {
        _settings.Setup(x => x.GetSettingByKeyAsync("PromptCaching.Config"))
            .ReturnsAsync(new GlobalSettingDto { Id = 1, Key = "PromptCaching.Config", Value = "{}" });
        var input = new UpdatePromptCachingConfigDto
        {
            Enabled = true,
            Rules = [new PromptCachingRuleDto
            {
                Name = "Claude",
                Provider = "OpenRouter",
                ModelPattern = "anthropic/*",
                Strategy = "OpenRouterAutomatic",
                Ttl = "5m"
            }]
        };

        var result = await _controller.UpdateConfig(input);

        result.Should().BeOfType<OkObjectResult>();
        _settings.Verify(x => x.UpdateSettingByKeyAsync(It.Is<UpdateGlobalSettingByKeyDto>(s =>
            s.Key == "PromptCaching.Config" && s.Value.Contains("schema_version"))), Times.Once);
        _cache.Verify(x => x.InvalidateSettingAsync("PromptCaching.Config"), Times.Once);
    }

    [Fact]
    public async Task UpdateConfig_UnsupportedProvider_ReturnsBadRequest()
    {
        var input = new UpdatePromptCachingConfigDto
        {
            Enabled = true,
            Rules = [new PromptCachingRuleDto
            {
                Name = "Unsafe",
                Provider = "Replicate",
                ModelPattern = "*",
                Strategy = "OpenRouterAutomatic"
            }]
        };

        (await _controller.UpdateConfig(input)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GetCapabilities_IncludesManagedAndProviderManagedEntries()
    {
        var result = _controller.GetCapabilities();
        var values = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<PromptCachingCapabilityDto>>().Subject;
        values.Should().Contain(x => x.ModelPattern == "anthropic/*" && !x.ProviderManaged);
        values.Should().Contain(x => x.ProviderManaged);
    }
}
