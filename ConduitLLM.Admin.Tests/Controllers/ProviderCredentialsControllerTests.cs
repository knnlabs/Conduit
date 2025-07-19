using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Admin.Tests.Controllers
{
    /// <summary>
    /// Unit tests for the ProviderCredentialsController class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ProviderCredentialsControllerTests
    {
        private readonly Mock<IAdminProviderCredentialService> _mockService;
        private readonly Mock<ILogger<ProviderCredentialsController>> _mockLogger;
        private readonly ProviderCredentialsController _controller;
        private readonly ITestOutputHelper _output;

        public ProviderCredentialsControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockService = new Mock<IAdminProviderCredentialService>();
            _mockLogger = new Mock<ILogger<ProviderCredentialsController>>();
            _controller = new ProviderCredentialsController(_mockService.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProviderCredentialsController(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProviderCredentialsController(_mockService.Object, null!));
        }

        #endregion

        #region GetAllProviderCredentials Tests

        [Fact]
        public async Task GetAllProviderCredentials_WithCredentials_ShouldReturnOkWithList()
        {
            // Arrange
            var credentials = new List<ProviderCredentialDto>
            {
                new() { Id = 1, ProviderName = "openai", IsEnabled = true },
                new() { Id = 2, ProviderName = "anthropic", IsEnabled = true },
                new() { Id = 3, ProviderName = "azure", IsEnabled = false }
            };

            _mockService.Setup(x => x.GetAllProviderCredentialsAsync())
                .ReturnsAsync(credentials);

            // Act
            var result = await _controller.GetAllProviderCredentials();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCredentials = Assert.IsAssignableFrom<IEnumerable<ProviderCredentialDto>>(okResult.Value);
            returnedCredentials.Should().HaveCount(3);
            returnedCredentials.Should().BeEquivalentTo(credentials);
        }

        [Fact]
        public async Task GetAllProviderCredentials_WithEmptyList_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockService.Setup(x => x.GetAllProviderCredentialsAsync())
                .ReturnsAsync(new List<ProviderCredentialDto>());

            // Act
            var result = await _controller.GetAllProviderCredentials();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCredentials = Assert.IsAssignableFrom<IEnumerable<ProviderCredentialDto>>(okResult.Value);
            returnedCredentials.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllProviderCredentials_WithException_ShouldReturn500()
        {
            // Arrange
            _mockService.Setup(x => x.GetAllProviderCredentialsAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllProviderCredentials();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            statusCodeResult.Value.Should().Be("An unexpected error occurred.");
            
            _mockLogger.Verify(x => x.LogError(
                It.IsAny<Exception>(),
                "Error getting all provider credentials"),
                Times.Once);
        }

        #endregion

        #region GetProviderCredentialById Tests

        [Fact]
        public async Task GetProviderCredentialById_WithExistingId_ShouldReturnOkWithCredential()
        {
            // Arrange
            var credential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "openai",
                IsEnabled = true,
                ApiBase = "https://api.openai.com"
            };

            _mockService.Setup(x => x.GetProviderCredentialByIdAsync(1))
                .ReturnsAsync(credential);

            // Act
            var result = await _controller.GetProviderCredentialById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCredential = Assert.IsType<ProviderCredentialDto>(okResult.Value);
            returnedCredential.Should().BeEquivalentTo(credential);
        }

        [Fact]
        public async Task GetProviderCredentialById_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetProviderCredentialByIdAsync(999))
                .ReturnsAsync((ProviderCredentialDto?)null);

            // Act
            var result = await _controller.GetProviderCredentialById(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value!;
            ((string)error.error).Should().Be("Provider credential not found");
            
            _mockLogger.Verify(x => x.LogWarning(
                "Provider credential not found {ProviderId}",
                999),
                Times.Once);
        }

        [Fact]
        public async Task GetProviderCredentialById_WithException_ShouldReturn500()
        {
            // Arrange
            _mockService.Setup(x => x.GetProviderCredentialByIdAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetProviderCredentialById(1);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            
            _mockLogger.Verify(x => x.LogError(
                It.IsAny<Exception>(),
                "Error getting provider credential with ID {Id}",
                1),
                Times.Once);
        }

        #endregion

        #region GetProviderCredentialByName Tests

        [Fact]
        public async Task GetProviderCredentialByName_WithExistingName_ShouldReturnOkWithCredential()
        {
            // Arrange
            var credential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "openai",
                IsEnabled = true
            };

            _mockService.Setup(x => x.GetProviderCredentialByNameAsync("openai"))
                .ReturnsAsync(credential);

            // Act
            var result = await _controller.GetProviderCredentialByName("openai");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCredential = Assert.IsType<ProviderCredentialDto>(okResult.Value);
            returnedCredential.ProviderName.Should().Be("openai");
        }

        [Fact]
        public async Task GetProviderCredentialByName_WithNonExistingName_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetProviderCredentialByNameAsync("non-existing"))
                .ReturnsAsync((ProviderCredentialDto?)null);

            // Act
            var result = await _controller.GetProviderCredentialByName("non-existing");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value!;
            ((string)error.error).Should().Be("Provider credential not found");
        }

        #endregion

        #region CreateProviderCredential Tests

        [Fact]
        public async Task CreateProviderCredential_WithValidRequest_ShouldReturnCreated()
        {
            // Arrange
            var request = new CreateProviderCredentialDto
            {
                ProviderName = "new-provider",
                ApiKey = "test-api-key",
                ApiBase = "https://api.example.com",
                IsEnabled = true
            };

            var createdDto = new ProviderCredentialDto
            {
                Id = 10,
                ProviderName = request.ProviderName,
                ApiBase = request.ApiBase ?? string.Empty,
                IsEnabled = request.IsEnabled
            };

            _mockService.Setup(x => x.CreateProviderCredentialAsync(It.IsAny<CreateProviderCredentialDto>()))
                .ReturnsAsync(createdDto);

            // Act
            var result = await _controller.CreateProviderCredential(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            createdResult.ActionName.Should().Be(nameof(ProviderCredentialsController.GetProviderCredentialById));
            createdResult.RouteValues!["id"].Should().Be(10);
            
            var returnedCredential = Assert.IsType<ProviderCredentialDto>(createdResult.Value);
            returnedCredential.Id.Should().Be(10);
            returnedCredential.ProviderName.Should().Be("new-provider");
        }

        [Fact]
        public async Task CreateProviderCredential_WithDuplicateName_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateProviderCredentialDto
            {
                ProviderName = "existing-provider",
                ApiKey = "test-key"
            };

            _mockService.Setup(x => x.CreateProviderCredentialAsync(It.IsAny<CreateProviderCredentialDto>()))
                .ThrowsAsync(new InvalidOperationException("Provider with name 'existing-provider' already exists"));

            // Act
            var result = await _controller.CreateProviderCredential(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("Provider with name 'existing-provider' already exists");
        }

        #endregion

        #region UpdateProviderCredential Tests

        [Fact]
        public async Task UpdateProviderCredential_WithValidRequest_ShouldReturnNoContent()
        {
            // Arrange
            var request = new UpdateProviderCredentialDto
            {
                Id = 1,
                ApiBase = "https://api.updated.com",
                IsEnabled = false,
                ApiKey = "new-api-key"
            };

            _mockService.Setup(x => x.UpdateProviderCredentialAsync(It.IsAny<UpdateProviderCredentialDto>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateProviderCredential(1, request);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateProviderCredential_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            var request = new UpdateProviderCredentialDto
            {
                Id = 999,
                IsEnabled = false
            };

            _mockService.Setup(x => x.UpdateProviderCredentialAsync(It.IsAny<UpdateProviderCredentialDto>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateProviderCredential(999, request);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value!;
            ((string)error.error).Should().Be("Provider credential not found");
        }

        #endregion

        #region DeleteProviderCredential Tests

        [Fact]
        public async Task DeleteProviderCredential_WithExistingId_ShouldReturnNoContent()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteProviderCredentialAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteProviderCredential(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteProviderCredential_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteProviderCredentialAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteProviderCredential(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value!;
            ((string)error.error).Should().Be("Provider credential not found");
        }

        [Fact]
        public async Task DeleteProviderCredential_WithProviderInUse_ShouldReturnConflict()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteProviderCredentialAsync(1))
                .ThrowsAsync(new InvalidOperationException("Cannot delete provider credential that is in use"));

            // Act
            var result = await _controller.DeleteProviderCredential(1);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            dynamic error = conflictResult.Value!;
            ((string)error.error).Should().Contain("Cannot delete");
        }

        #endregion

        #region TestProviderConnection Tests

        [Fact]
        public async Task TestProviderConnection_WithValidProvider_ShouldReturnOk()
        {
            // Arrange
            var testResult = new ProviderConnectionTestResultDto
            {
                Success = true,
                Message = "Connection successful",
                ProviderName = "openai"
            };

            _mockService.Setup(x => x.TestProviderConnectionAsync(It.IsAny<ProviderCredentialDto>()))
                .ReturnsAsync(testResult);

            // Act
            var result = await _controller.TestProviderConnection(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResult = Assert.IsType<ProviderConnectionTestResultDto>(okResult.Value);
            returnedResult.Success.Should().BeTrue();
            returnedResult.Message.Should().Be("Connection successful");
        }

        [Fact]
        public async Task TestProviderConnection_WithFailedConnection_ShouldReturnOkWithFailure()
        {
            // Arrange
            var testResult = new ProviderConnectionTestResultDto
            {
                Success = false,
                Message = "Connection failed: Invalid API key",
                ErrorDetails = "401 Unauthorized"
            };

            _mockService.Setup(x => x.TestProviderConnectionAsync(It.IsAny<ProviderCredentialDto>()))
                .ReturnsAsync(testResult);

            // Act
            var result = await _controller.TestProviderConnection(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResult = Assert.IsType<ProviderConnectionTestResultDto>(okResult.Value);
            returnedResult.Success.Should().BeFalse();
            returnedResult.Message.Should().Contain("Invalid API key");
        }

        #endregion
    }
}