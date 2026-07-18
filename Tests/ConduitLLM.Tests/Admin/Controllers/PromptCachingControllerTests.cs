using System.Text.Json;

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
    private readonly Mock<IAdminGlobalSettingService> _mockSettingService;
    private readonly Mock<IGlobalSettingsCacheService> _mockCacheService;
    private readonly PromptCachingController _controller;

    public PromptCachingControllerTests()
    {
        _mockSettingService = new Mock<IAdminGlobalSettingService>();
        _mockCacheService = new Mock<IGlobalSettingsCacheService>();
        var mockLogger = new Mock<ILogger<PromptCachingController>>();

        _controller = new PromptCachingController(
            _mockSettingService.Object,
            _mockCacheService.Object,
            mockLogger.Object);

        // Set up HttpContext for audit logging
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetConfig_NoSetting_ReturnsDefaults()
    {
        // Arrange
        _mockCacheService.Setup(x => x.GetSettingValueAsync("PromptCaching.Config"))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.GetConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var config = okResult.Value.Should().BeOfType<PromptCachingConfigDto>().Subject;
        config.AutoInjectEnabled.Should().BeFalse();
        config.InjectionPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfig_ExistingSetting_ReturnsConfig()
    {
        // Arrange
        var json = """{"auto_inject_enabled":true,"injection_points":[{"role":"system","index":0}]}""";
        _mockCacheService.Setup(x => x.GetSettingValueAsync("PromptCaching.Config"))
            .ReturnsAsync(json);

        // Act
        var result = await _controller.GetConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var config = okResult.Value.Should().BeOfType<PromptCachingConfigDto>().Subject;
        config.AutoInjectEnabled.Should().BeTrue();
        config.InjectionPoints.Should().HaveCount(1);
        config.InjectionPoints[0].Role.Should().Be("system");
        config.InjectionPoints[0].Index.Should().Be(0);
    }

    [Fact]
    public async Task UpdateConfig_ValidInput_SavesAndInvalidatesCache()
    {
        // Arrange
        var dto = new UpdatePromptCachingConfigDto
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPointDto>
            {
                new() { Role = "system", Index = 0 },
                new() { Role = "user", Index = -1 }
            }
        };

        _mockSettingService.Setup(x => x.GetSettingByKeyAsync("PromptCaching.Config"))
            .ReturnsAsync(new GlobalSettingDto { Id = 1, Key = "PromptCaching.Config", Value = "{}" });

        _mockSettingService.Setup(x => x.UpdateSettingByKeyAsync(It.IsAny<UpdateGlobalSettingByKeyDto>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateConfig(dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var config = okResult.Value.Should().BeOfType<PromptCachingConfigDto>().Subject;
        config.AutoInjectEnabled.Should().BeTrue();
        config.InjectionPoints.Should().HaveCount(2);

        // Verify cache was invalidated
        _mockCacheService.Verify(x => x.InvalidateSettingAsync("PromptCaching.Config"), Times.Once);

        // Verify setting was updated (not created)
        _mockSettingService.Verify(x => x.UpdateSettingByKeyAsync(It.Is<UpdateGlobalSettingByKeyDto>(
            s => s.Key == "PromptCaching.Config")), Times.Once);
        _mockSettingService.Verify(x => x.CreateSettingAsync(It.IsAny<CreateGlobalSettingDto>()), Times.Never);
    }

    [Fact]
    public async Task UpdateConfig_NoExistingSetting_CreatesNew()
    {
        // Arrange
        var dto = new UpdatePromptCachingConfigDto
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPointDto>
            {
                new() { Role = "system", Index = 0 }
            }
        };

        _mockSettingService.Setup(x => x.GetSettingByKeyAsync("PromptCaching.Config"))
            .ReturnsAsync((GlobalSettingDto?)null);

        _mockSettingService.Setup(x => x.CreateSettingAsync(It.IsAny<CreateGlobalSettingDto>()))
            .ReturnsAsync(new GlobalSettingDto { Id = 1, Key = "PromptCaching.Config", Value = "{}" });

        // Act
        var result = await _controller.UpdateConfig(dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify setting was created (not updated)
        _mockSettingService.Verify(x => x.CreateSettingAsync(It.Is<CreateGlobalSettingDto>(
            s => s.Key == "PromptCaching.Config")), Times.Once);
        _mockSettingService.Verify(x => x.UpdateSettingByKeyAsync(It.IsAny<UpdateGlobalSettingByKeyDto>()), Times.Never);

        // Verify cache was invalidated
        _mockCacheService.Verify(x => x.InvalidateSettingAsync("PromptCaching.Config"), Times.Once);
    }

    [Fact]
    public void UpdateConfig_TooManyInjectionPoints_FailsValidation()
    {
        // Arrange — 5 injection points exceeds the MaxLength(4) limit
        var dto = new UpdatePromptCachingConfigDto
        {
            AutoInjectEnabled = true,
            InjectionPoints = new List<CacheInjectionPointDto>
            {
                new() { Role = "system", Index = 0 },
                new() { Role = "user", Index = -1 },
                new() { Role = "user", Index = -2 },
                new() { Role = "assistant", Index = -1 },
                new() { Role = "system", Index = 1 }
            }
        };

        // Act — validate using DataAnnotations
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(dto);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dto, context, results, true);

        // Assert
        isValid.Should().BeFalse();
        results.Should().Contain(r => r.ErrorMessage!.Contains("Maximum 4 injection points"));
    }

    [Fact]
    public void UpdateConfig_InvalidRole_FailsValidation()
    {
        // Arrange — "admin" is not a valid role
        var point = new CacheInjectionPointDto
        {
            Role = "admin",
            Index = 0
        };

        // Act — validate the injection point
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(point);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(point, context, results, true);

        // Assert
        isValid.Should().BeFalse();
        results.Should().Contain(r => r.ErrorMessage!.Contains("Role must be system, user, or assistant"));
    }
}
