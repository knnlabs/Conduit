using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for the VirtualKeyGroupsController class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class VirtualKeyGroupsControllerTests
    {
        private readonly Mock<IVirtualKeyGroupRepository> _mockGroupRepository;
        private readonly Mock<IVirtualKeyRepository> _mockKeyRepository;
        private readonly Mock<IConfigurationDbContext> _mockContext;
        private readonly Mock<IRefundService> _mockRefundService;
        private readonly Mock<ILogger<VirtualKeyGroupsController>> _mockLogger;
        private readonly VirtualKeyGroupsController _controller;
        private readonly ITestOutputHelper _output;

        public VirtualKeyGroupsControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockGroupRepository = new Mock<IVirtualKeyGroupRepository>();
            _mockKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockContext = new Mock<IConfigurationDbContext>();
            _mockRefundService = new Mock<IRefundService>();
            _mockLogger = new Mock<ILogger<VirtualKeyGroupsController>>();

            _controller = new VirtualKeyGroupsController(
                _mockGroupRepository.Object,
                _mockKeyRepository.Object,
                _mockContext.Object,
                _mockRefundService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetKeysInGroup_ShouldReturnVirtualKeys_WhenGroupExists()
        {
            // Arrange
            var groupId = 1;
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey
                {
                    Id = 1,
                    KeyName = "Test Key 1",
                    KeyHash = "hash123456789",
                    VirtualKeyGroupId = groupId,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1),
                    AllowedModels = "gpt-4,gpt-3.5-turbo",
                    Description = "Test description"
                },
                new VirtualKey
                {
                    Id = 2,
                    KeyName = "Test Key 2",
                    KeyHash = "hash987654321",
                    VirtualKeyGroupId = groupId,
                    IsEnabled = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow,
                    AllowedModels = "claude-3-sonnet",
                    Description = "Another test key"
                }
            };

            var group = new VirtualKeyGroup
            {
                Id = groupId,
                GroupName = "Test Group",
                Balance = 100.50m,
                LifetimeCreditsAdded = 200.00m,
                LifetimeSpent = 99.50m,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                VirtualKeys = virtualKeys
            };

            _mockGroupRepository.Setup(r => r.GetByIdWithKeysAsync(groupId))
                              .ReturnsAsync(group);

            // Act
            var result = await _controller.GetKeysInGroup(groupId);

            // Assert - Controller returns IActionResult, not ActionResult<T>
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var keys = okResult.Value.Should().BeOfType<List<VirtualKeyDto>>().Subject;

            Assert.Equal(2, keys.Count);
            Assert.Equal("Test Key 1", keys[0].KeyName);
            Assert.Equal("hash123456...", keys[0].KeyPrefix);
            Assert.True(keys[0].IsEnabled);
            Assert.Equal("Test Key 2", keys[1].KeyName);
            Assert.Equal("hash987654...", keys[1].KeyPrefix);
            Assert.False(keys[1].IsEnabled);

            // Verify the correct repository method was called
            _mockGroupRepository.Verify(r => r.GetByIdWithKeysAsync(groupId), Times.Once);
            _mockGroupRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetKeysInGroup_ShouldReturnNotFound_WhenGroupDoesNotExist()
        {
            // Arrange
            var groupId = 999;
            _mockGroupRepository.Setup(r => r.GetByIdWithKeysAsync(groupId))
                              .ReturnsAsync((VirtualKeyGroup?)null);

            // Act
            var result = await _controller.GetKeysInGroup(groupId);

            // Assert - ExecuteWithNotFoundAsync returns NotFoundObjectResult with ErrorResponseDto
            result.Should().BeOfType<NotFoundObjectResult>();

            // Verify the correct repository method was called
            _mockGroupRepository.Verify(r => r.GetByIdWithKeysAsync(groupId), Times.Once);
        }

        [Fact]
        public async Task GetKeysInGroup_ShouldReturnEmptyList_WhenGroupHasNoKeys()
        {
            // Arrange
            var groupId = 1;
            var group = new VirtualKeyGroup
            {
                Id = groupId,
                GroupName = "Empty Group",
                Balance = 0,
                LifetimeCreditsAdded = 0,
                LifetimeSpent = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                VirtualKeys = new List<VirtualKey>()
            };

            _mockGroupRepository.Setup(r => r.GetByIdWithKeysAsync(groupId))
                              .ReturnsAsync(group);

            // Act
            var result = await _controller.GetKeysInGroup(groupId);

            // Assert - Controller returns IActionResult, not ActionResult<T>
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var keys = okResult.Value.Should().BeOfType<List<VirtualKeyDto>>().Subject;

            Assert.Empty(keys);

            // Verify the correct repository method was called
            _mockGroupRepository.Verify(r => r.GetByIdWithKeysAsync(groupId), Times.Once);
        }

        [Fact]
        public async Task GetKeysInGroup_ShouldReturnBadRequest_WhenInvalidOperationExceptionOccurs()
        {
            // Arrange
            var groupId = 1;
            _mockGroupRepository.Setup(r => r.GetByIdWithKeysAsync(groupId))
                              .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            var result = await _controller.GetKeysInGroup(groupId);

            // Assert - ExceptionToResponseMapper maps InvalidOperationException to 400 Bad Request
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;

            var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            Assert.Equal("Database error", errorResponse.error);
            Assert.Equal("invalid_operation", errorResponse.Code);

            // Verify the repository method was called
            _mockGroupRepository.Verify(r => r.GetByIdWithKeysAsync(groupId), Times.Once);
        }

        [Fact]
        public async Task GetKeysInGroup_ShouldHandleNullVirtualKeysCollection()
        {
            // Arrange
            var groupId = 1;
            var group = new VirtualKeyGroup
            {
                Id = groupId,
                GroupName = "Group with null keys",
                Balance = 50.00m,
                LifetimeCreditsAdded = 100.00m,
                LifetimeSpent = 50.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                VirtualKeys = null! // Simulating null collection
            };

            _mockGroupRepository.Setup(r => r.GetByIdWithKeysAsync(groupId))
                              .ReturnsAsync(group);

            // Act
            var result = await _controller.GetKeysInGroup(groupId);

            // Assert - Controller returns IActionResult, not ActionResult<T>
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var keys = okResult.Value.Should().BeOfType<List<VirtualKeyDto>>().Subject;

            Assert.Empty(keys);

            // Verify the correct repository method was called
            _mockGroupRepository.Verify(r => r.GetByIdWithKeysAsync(groupId), Times.Once);
        }
    }
}