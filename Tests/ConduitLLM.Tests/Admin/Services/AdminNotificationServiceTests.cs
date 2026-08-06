using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

public class AdminNotificationServiceTests
{
    private readonly Mock<INotificationRepository> _mockNotificationRepository;
    private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
    private readonly Mock<ILogger<AdminNotificationService>> _mockLogger;
    private readonly AdminNotificationService _service;

    public AdminNotificationServiceTests()
    {
        _mockNotificationRepository = new Mock<INotificationRepository>();
        _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
        _mockLogger = new Mock<ILogger<AdminNotificationService>>();

        _service = new AdminNotificationService(
            _mockNotificationRepository.Object,
            _mockVirtualKeyRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetNotificationByIdAsync_WithExistingId_ShouldReturnDto()
    {
        // Arrange
        var entity = new Notification
        {
            Id = 1,
            VirtualKeyId = null,
            Type = NotificationType.System,
            Severity = NotificationSeverity.Info,
            Message = "System notification",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _mockNotificationRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _service.GetNotificationByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Be("System notification");
        result.Type.Should().Be(NotificationType.System);
        result.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetNotificationByIdAsync_WithVirtualKey_ShouldIncludeKeyName()
    {
        // Arrange
        var entity = new Notification
        {
            Id = 1,
            VirtualKeyId = 42,
            Type = NotificationType.BudgetWarning,
            Severity = NotificationSeverity.Warning,
            Message = "Budget exceeded",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        var virtualKey = new VirtualKey { Id = 42, KeyName = "Production Key" };

        _mockNotificationRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(virtualKey);

        // Act
        var result = await _service.GetNotificationByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.VirtualKeyName.Should().Be("Production Key");
        result.VirtualKeyId.Should().Be(42);
    }

    [Fact]
    public async Task GetNotificationByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        _mockNotificationRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        // Act
        var result = await _service.GetNotificationByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateNotificationAsync_WithValidData_ShouldCreateAndReturnDto()
    {
        // Arrange
        var createDto = new CreateNotificationDto
        {
            VirtualKeyId = null,
            Type = NotificationType.System,
            Severity = NotificationSeverity.Info,
            Message = "New notification"
        };
        var createdEntity = new Notification
        {
            Id = 1,
            VirtualKeyId = null,
            Type = NotificationType.System,
            Severity = NotificationSeverity.Info,
            Message = "New notification",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockNotificationRepository.Setup(x => x.CreateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockNotificationRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEntity);

        // Act
        var result = await _service.CreateNotificationAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("New notification");
        result.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task CreateNotificationAsync_WithInvalidVirtualKeyId_ShouldThrowArgumentException()
    {
        // Arrange
        var createDto = new CreateNotificationDto
        {
            VirtualKeyId = 999,
            Type = NotificationType.BudgetWarning,
            Severity = NotificationSeverity.Warning,
            Message = "Warning"
        };

        _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VirtualKey?)null);

        // Act
        var act = () => _service.CreateNotificationAsync(createDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*999*not found*");
    }

    [Fact]
    public async Task UpdateNotificationAsync_WithExistingId_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var existing = new Notification
        {
            Id = 1,
            Type = NotificationType.System,
            Severity = NotificationSeverity.Info,
            Message = "Old message",
            IsRead = false
        };

        _mockNotificationRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mockNotificationRepository.Setup(x => x.UpdateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var updateDto = new UpdateNotificationDto { Id = 1, IsRead = true, Message = "Updated message" };

        // Act
        var result = await _service.UpdateNotificationAsync(updateDto);

        // Assert
        result.Should().BeTrue();
        existing.IsRead.Should().BeTrue();
        existing.Message.Should().Be("Updated message");
    }

    [Fact]
    public async Task UpdateNotificationAsync_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        _mockNotificationRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var updateDto = new UpdateNotificationDto { Id = 999, IsRead = true };

        // Act
        var result = await _service.UpdateNotificationAsync(updateDto);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkNotificationAsReadAsync_ShouldDelegateToRepository()
    {
        // Arrange
        _mockNotificationRepository.Setup(x => x.MarkAsReadAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.MarkNotificationAsReadAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteNotificationAsync_ShouldDelegateToRepository()
    {
        // Arrange
        _mockNotificationRepository.Setup(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteNotificationAsync(1);

        // Assert
        result.Should().BeTrue();
    }
}
