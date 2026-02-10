using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

public class AdminGlobalSettingServiceTests
{
    private readonly Mock<IGlobalSettingRepository> _mockGlobalSettingRepository;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<AdminGlobalSettingService>> _mockLogger;
    private readonly AdminGlobalSettingService _service;

    public AdminGlobalSettingServiceTests()
    {
        _mockGlobalSettingRepository = new Mock<IGlobalSettingRepository>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<AdminGlobalSettingService>>();

        _service = new AdminGlobalSettingService(
            _mockGlobalSettingRepository.Object,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetAllSettingsAsync_ShouldReturnMappedDtos()
    {
        // Arrange
        var entities = new List<GlobalSetting>
        {
            new() { Id = 1, Key = "setting1", Value = "value1", Description = "desc1" },
            new() { Id = 2, Key = "setting2", Value = "value2", Description = null }
        };
        _mockGlobalSettingRepository.Setup(x => x.GetAllUnboundedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = (await _service.GetAllSettingsAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Key.Should().Be("setting1");
        result[0].Value.Should().Be("value1");
        result[1].Key.Should().Be("setting2");
    }

    [Fact]
    public async Task GetSettingByIdAsync_WithExistingId_ShouldReturnDto()
    {
        // Arrange
        var entity = new GlobalSetting { Id = 1, Key = "test-key", Value = "test-value" };
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _service.GetSettingByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be("test-key");
        result.Value.Should().Be("test-value");
    }

    [Fact]
    public async Task GetSettingByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalSetting?)null);

        // Act
        var result = await _service.GetSettingByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSettingByKeyAsync_WithExistingKey_ShouldReturnDto()
    {
        // Arrange
        var entity = new GlobalSetting { Id = 1, Key = "my-key", Value = "my-value" };
        _mockGlobalSettingRepository.Setup(x => x.GetByKeyAsync("my-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _service.GetSettingByKeyAsync("my-key");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be("my-value");
    }

    [Fact]
    public async Task CreateSettingAsync_WithUniqueKey_ShouldCreateAndReturnDto()
    {
        // Arrange
        var createDto = new CreateGlobalSettingDto { Key = "new-key", Value = "new-value", Description = "desc" };
        var createdEntity = new GlobalSetting { Id = 1, Key = "new-key", Value = "new-value", Description = "desc" };

        _mockGlobalSettingRepository.Setup(x => x.GetByKeyAsync("new-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalSetting?)null);
        _mockGlobalSettingRepository.Setup(x => x.CreateAsync(It.IsAny<GlobalSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEntity);

        // Act
        var result = await _service.CreateSettingAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("new-key");
        result.Value.Should().Be("new-value");
    }

    [Fact]
    public async Task CreateSettingAsync_WithDuplicateKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var existing = new GlobalSetting { Id = 1, Key = "existing-key", Value = "old-value" };
        _mockGlobalSettingRepository.Setup(x => x.GetByKeyAsync("existing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var createDto = new CreateGlobalSettingDto { Key = "existing-key", Value = "new-value" };

        // Act
        var act = () => _service.CreateSettingAsync(createDto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateSettingAsync_WithExistingId_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var existing = new GlobalSetting { Id = 1, Key = "key", Value = "old-value", Description = "old-desc" };
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mockGlobalSettingRepository.Setup(x => x.UpdateAsync(It.IsAny<GlobalSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var updateDto = new UpdateGlobalSettingDto { Id = 1, Value = "new-value", Description = "new-desc" };

        // Act
        var result = await _service.UpdateSettingAsync(updateDto);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSettingAsync_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalSetting?)null);

        var updateDto = new UpdateGlobalSettingDto { Id = 999, Value = "new-value" };

        // Act
        var result = await _service.UpdateSettingAsync(updateDto);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSettingAsync_WithNoChanges_ShouldReturnTrueWithoutCallingUpdate()
    {
        // Arrange
        var existing = new GlobalSetting { Id = 1, Key = "key", Value = "same-value", Description = "same-desc" };
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var updateDto = new UpdateGlobalSettingDto { Id = 1, Value = "same-value", Description = "same-desc" };

        // Act
        var result = await _service.UpdateSettingAsync(updateDto);

        // Assert
        result.Should().BeTrue();
        _mockGlobalSettingRepository.Verify(
            x => x.UpdateAsync(It.IsAny<GlobalSetting>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSettingAsync_WithExistingId_ShouldDeleteAndReturnTrue()
    {
        // Arrange
        var entity = new GlobalSetting { Id = 1, Key = "to-delete", Value = "val" };
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _mockGlobalSettingRepository.Setup(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteSettingAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSettingAsync_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        _mockGlobalSettingRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalSetting?)null);

        // Act
        var result = await _service.DeleteSettingAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSettingByKeyAsync_WithExistingKey_ShouldDeleteAndReturnTrue()
    {
        // Arrange
        var entity = new GlobalSetting { Id = 1, Key = "to-delete", Value = "val" };
        _mockGlobalSettingRepository.Setup(x => x.GetByKeyAsync("to-delete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _mockGlobalSettingRepository.Setup(x => x.DeleteByKeyAsync("to-delete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteSettingByKeyAsync("to-delete");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSettingByKeyAsync_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        _mockGlobalSettingRepository.Setup(x => x.GetByKeyAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalSetting?)null);

        // Act
        var result = await _service.DeleteSettingByKeyAsync("missing");

        // Assert
        result.Should().BeFalse();
    }
}
