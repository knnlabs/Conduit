using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;

using Moq;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class ProviderKeyCredentialRepositoryTests
    {
        [Fact]
        public async Task SetPrimaryKeyAsync_WithExistingPrimary_ShouldUpdateCorrectly()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Providers.Add(provider);

            var existingPrimaryKey = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "key1",
                IsPrimary = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var newKey = new ProviderKeyCredential
            {
                Id = 2,
                ProviderId = 1,
                ApiKey = "key2",
                IsPrimary = false,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProviderKeyCredentials.AddRange(existingPrimaryKey, newKey);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.SetPrimaryKeyAsync(1, 2);

            // Assert
            Assert.True(result);

            using var verifyContext = CreateVerificationContext();
            var keys = await verifyContext.ProviderKeyCredentials
                .Where(k => k.ProviderId == 1)
                .ToListAsync();

            Assert.Equal(2, keys.Count);
            Assert.False(keys.First(k => k.Id == 1).IsPrimary);
            Assert.True(keys.First(k => k.Id == 2).IsPrimary);
        }

        [Fact]
        public async Task SetPrimaryKeyAsync_WithNoPrimary_ShouldSetPrimary()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Providers.Add(provider);

            var key = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "key1",
                IsPrimary = false,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProviderKeyCredentials.Add(key);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.SetPrimaryKeyAsync(1, 1);

            // Assert
            Assert.True(result);

            using var verifyContext = CreateVerificationContext();
            var updatedKey = await verifyContext.ProviderKeyCredentials.FindAsync(1);
            Assert.True(updatedKey!.IsPrimary);
        }

        [Fact]
        public async Task SetPrimaryKeyAsync_WithNonExistentKey_ShouldReturnFalse()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.SetPrimaryKeyAsync(1, 999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SetPrimaryKeyAsync_WithWrongProvider_ShouldReturnFalse()
        {
            // Arrange
            var provider1 = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var provider2 = new Provider
            {
                Id = 2,
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Providers.AddRange(provider1, provider2);

            var existingPrimary = new ProviderKeyCredential
            {
                Id = 2,
                ProviderId = 1,
                ApiKey = "provider1-key",
                IsPrimary = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var key = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 2, // Belongs to provider 2
                ApiKey = "key1",
                IsPrimary = false,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProviderKeyCredentials.AddRange(existingPrimary, key);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.SetPrimaryKeyAsync(1, 1); // Try to set for provider 1

            // Assert
            Assert.False(result);
            using var verifyContext = CreateVerificationContext();
            Assert.True((await verifyContext.ProviderKeyCredentials
                .SingleAsync(item => item.Id == existingPrimary.Id)).IsPrimary);
        }

        [Fact]
        public async Task Database_ShouldRejectMultiplePrimaryKeysForOneProvider()
        {
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.MiniMax,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Providers.Add(provider);

            var key1 = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "key1",
                IsPrimary = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var key2 = new ProviderKeyCredential
            {
                Id = 2,
                ProviderId = 1,
                ApiKey = "key2",
                IsPrimary = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ProviderKeyCredentials.AddRange(key1, key2);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => _context.SaveChangesAsync());
        }

        [Fact]
        public async Task SetPrimaryKeyAsync_WithTransactionFailure_ShouldRollback()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Groq,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Providers.Add(provider);

            var existingPrimaryKey = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "key1",
                IsPrimary = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var replacement = new ProviderKeyCredential
            {
                Id = 2,
                ProviderId = 1,
                ApiKey = "key2",
                IsPrimary = false,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProviderKeyCredentials.AddRange(existingPrimaryKey, replacement);
            await _context.SaveChangesAsync();

            _saveFailure.Arm(failOnCall: 2);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => _repository.SetPrimaryKeyAsync(1, 2));

            _saveFailure.Disarm();
            await using var verifyContext = CreateVerificationContext();
            var keys = await verifyContext.ProviderKeyCredentials
                .AsNoTracking()
                .Where(key => key.ProviderId == 1)
                .OrderBy(key => key.Id)
                .ToListAsync();
            Assert.True(keys[0].IsPrimary);
            Assert.False(keys[1].IsPrimary);
        }

        [Fact]
        public async Task SetPrimaryKeyAsync_WithMissingTarget_ShouldPreserveExistingPrimary()
        {
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };
            _context.AddRange(
                provider,
                new ProviderKeyCredential
                {
                    Id = 1,
                    ProviderId = 1,
                    ApiKey = "key1",
                    IsPrimary = true,
                    IsEnabled = true
                });
            await _context.SaveChangesAsync();

            var result = await _repository.SetPrimaryKeyAsync(1, 999);

            Assert.False(result);
            await using var verifyContext = CreateVerificationContext();
            Assert.True((await verifyContext.ProviderKeyCredentials.SingleAsync()).IsPrimary);
        }

        [Fact]
        public async Task SetPrimaryKeyAsync_ShouldUpdateTimestamps()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAICompatible,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Providers.Add(provider);

            var originalTime = DateTime.UtcNow.AddDays(-1);
            var key = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "key1",
                IsPrimary = false,
                IsEnabled = true,
                CreatedAt = originalTime,
                UpdatedAt = originalTime
            };

            _context.ProviderKeyCredentials.Add(key);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.SetPrimaryKeyAsync(1, 1);

            // Assert
            Assert.True(result);

            using var verifyContext = CreateVerificationContext();
            var updatedKey = await verifyContext.ProviderKeyCredentials.FindAsync(1);
            Assert.True(updatedKey!.UpdatedAt > originalTime);
            Assert.Equal(originalTime, updatedKey.CreatedAt); // CreatedAt should not change
        }
    }
}
