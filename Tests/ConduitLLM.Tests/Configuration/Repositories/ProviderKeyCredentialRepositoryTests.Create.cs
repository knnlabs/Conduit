using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class ProviderKeyCredentialRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_WhenFirstEnabledKey_ShouldAutomaticallySetAsPrimary()
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

            var keyCredential = new ProviderKeyCredential
            {
                ProviderId = 1,
                ApiKey = "test-key",
                KeyName = "Test Key",
                IsPrimary = false, // Explicitly set to false
                IsEnabled = true
            };

            // Act
            var resultId = await _repository.CreateAsync(keyCredential);

            // Assert
            Assert.True(keyCredential.IsPrimary, "First enabled key should automatically be set as primary");
            Assert.True(resultId > 0, "Should return the created ID");
        }

        [Fact]
        public async Task CreateAsync_WhenNotFirstEnabledKey_ShouldNotAutomaticallySetAsPrimary()
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

            // Create first enabled key
            var firstKey = new ProviderKeyCredential
            {
                ProviderId = 1,
                ApiKey = "first-key",
                KeyName = "First Key",
                IsPrimary = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ProviderKeyCredentials.Add(firstKey);
            await _context.SaveChangesAsync();

            var secondKeyCredential = new ProviderKeyCredential
            {
                ProviderId = 1,
                ApiKey = "second-key",
                KeyName = "Second Key",
                IsPrimary = false,
                IsEnabled = true
            };

            // Act
            var resultId = await _repository.CreateAsync(secondKeyCredential);

            // Assert
            Assert.False(secondKeyCredential.IsPrimary, "Second enabled key should not automatically be set as primary");
            Assert.True(resultId > 0, "Should return the created ID");
        }

        [Fact]
        public async Task CreateAsync_WhenDisabled_ShouldNotAutomaticallySetAsPrimary()
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

            var keyCredential = new ProviderKeyCredential
            {
                ProviderId = 1,
                ApiKey = "test-key",
                KeyName = "Test Key",
                IsPrimary = false,
                IsEnabled = false // Disabled
            };

            // Act
            var resultId = await _repository.CreateAsync(keyCredential);

            // Assert
            Assert.False(keyCredential.IsPrimary, "Disabled key should not automatically be set as primary");
            Assert.True(resultId > 0, "Should return the created ID");
        }

        [Fact]
        public async Task CreateAsync_WhenExplicitlySetAsPrimary_ShouldStayPrimary()
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

            var keyCredential = new ProviderKeyCredential
            {
                ProviderId = 1,
                ApiKey = "test-key",
                KeyName = "Test Key",
                IsPrimary = true, // Explicitly set as primary
                IsEnabled = true
            };

            // Act
            var resultId = await _repository.CreateAsync(keyCredential);

            // Assert
            Assert.True(keyCredential.IsPrimary, "Explicitly set primary should remain primary");
            Assert.True(resultId > 0, "Should return the created ID");
        }
    }
}
