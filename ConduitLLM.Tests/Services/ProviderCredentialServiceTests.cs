using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class ProviderCredentialServiceTests
    {
        private readonly Mock<ILogger<ProviderCredentialService>> _loggerMock;
        private readonly Mock<IProviderCredentialRepository> _repositoryMock;
        private readonly ProviderCredentialService _service;

        public ProviderCredentialServiceTests()
        {
            _loggerMock = new Mock<ILogger<ProviderCredentialService>>();
            _repositoryMock = new Mock<IProviderCredentialRepository>();
            _service = new ProviderCredentialService(_loggerMock.Object, _repositoryMock.Object);
        }

        [Fact]
        public async Task AddCredentialAsync_ValidCredential_CallsRepository()
        {
            // Arrange
            var credential = new ProviderCredential
            {
                ProviderName = "OpenAI",
                ApiKey = "test-key",
                IsEnabled = true
            };

            _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<ProviderCredential>(), default))
                .ReturnsAsync(1);

            // Act
            await _service.AddCredentialAsync(credential);

            // Assert
            _repositoryMock.Verify(r => r.CreateAsync(credential, default), Times.Once);
        }

        [Fact]
        public async Task AddCredentialAsync_NullCredential_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.AddCredentialAsync(null!));
        }

        [Fact]
        public async Task DeleteCredentialAsync_ExistingId_CallsRepository()
        {
            // Arrange
            var id = 123;
            _repositoryMock.Setup(r => r.DeleteAsync(id, default))
                .ReturnsAsync(true);

            // Act
            await _service.DeleteCredentialAsync(id);

            // Assert
            _repositoryMock.Verify(r => r.DeleteAsync(id, default), Times.Once);
        }

        [Fact]
        public async Task GetAllCredentialsAsync_ReturnsAllCredentials()
        {
            // Arrange
            var expectedCredentials = new List<ProviderCredential>
            {
                new ProviderCredential { Id = 1, ProviderName = "OpenAI" },
                new ProviderCredential { Id = 2, ProviderName = "Anthropic" }
            };

            _repositoryMock.Setup(r => r.GetAllAsync(default))
                .ReturnsAsync(expectedCredentials);

            // Act
            var result = await _service.GetAllCredentialsAsync();

            // Assert
            Assert.Equal(expectedCredentials, result);
            _repositoryMock.Verify(r => r.GetAllAsync(default), Times.Once);
        }

        [Fact]
        public async Task GetCredentialByIdAsync_ExistingId_ReturnsCredential()
        {
            // Arrange
            var id = 123;
            var expectedCredential = new ProviderCredential { Id = id, ProviderName = "OpenAI" };

            _repositoryMock.Setup(r => r.GetByIdAsync(id, default))
                .ReturnsAsync(expectedCredential);

            // Act
            var result = await _service.GetCredentialByIdAsync(id);

            // Assert
            Assert.Equal(expectedCredential, result);
            _repositoryMock.Verify(r => r.GetByIdAsync(id, default), Times.Once);
        }

        [Fact]
        public async Task GetCredentialByIdAsync_NonExistingId_ReturnsNull()
        {
            // Arrange
            var id = 999;
            _repositoryMock.Setup(r => r.GetByIdAsync(id, default))
                .ReturnsAsync((ProviderCredential?)null);

            // Act
            var result = await _service.GetCredentialByIdAsync(id);

            // Assert
            Assert.Null(result);
            _repositoryMock.Verify(r => r.GetByIdAsync(id, default), Times.Once);
        }

        [Fact]
        public async Task GetCredentialByProviderNameAsync_ExistingName_ReturnsCredential()
        {
            // Arrange
            var providerName = "OpenAI";
            var expectedCredential = new ProviderCredential { Id = 1, ProviderName = providerName };

            _repositoryMock.Setup(r => r.GetByProviderNameAsync(providerName, default))
                .ReturnsAsync(expectedCredential);

            // Act
            var result = await _service.GetCredentialByProviderNameAsync(providerName);

            // Assert
            Assert.Equal(expectedCredential, result);
            _repositoryMock.Verify(r => r.GetByProviderNameAsync(providerName, default), Times.Once);
        }

        [Fact]
        public async Task GetCredentialByProviderNameAsync_NullName_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetCredentialByProviderNameAsync(null!));
        }

        [Fact]
        public async Task GetCredentialByProviderNameAsync_EmptyName_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetCredentialByProviderNameAsync(""));
        }

        [Fact]
        public async Task UpdateCredentialAsync_ValidCredential_CallsRepository()
        {
            // Arrange
            var credential = new ProviderCredential
            {
                Id = 1,
                ProviderName = "OpenAI",
                ApiKey = "updated-key",
                UpdatedAt = DateTime.UtcNow.AddDays(-1) // Old timestamp
            };

            _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProviderCredential>(), default))
                .ReturnsAsync(true);

            var beforeUpdate = credential.UpdatedAt;

            // Act
            await _service.UpdateCredentialAsync(credential);

            // Assert
            Assert.True(credential.UpdatedAt > beforeUpdate); // UpdatedAt should be updated
            _repositoryMock.Verify(r => r.UpdateAsync(credential, default), Times.Once);
        }

        [Fact]
        public async Task UpdateCredentialAsync_NullCredential_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpdateCredentialAsync(null!));
        }

        [Fact]
        public async Task AddCredentialAsync_RepositoryThrows_LogsErrorAndRethrows()
        {
            // Arrange
            var credential = new ProviderCredential { ProviderName = "OpenAI" };
            var exception = new InvalidOperationException("Database error");

            _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<ProviderCredential>(), default))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddCredentialAsync(credential));
            
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error adding credential")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}