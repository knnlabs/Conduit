using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class ProviderKeyCredentialRepositoryTests
    {
        [Fact]
        public async Task UpdateAsync_WhenEnablingOnlyKey_ShouldAutomaticallySetAsPrimary()
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

            // Create a disabled key
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-key",
                KeyName = "Test Key",
                IsPrimary = false,
                IsEnabled = false, // Initially disabled
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ProviderKeyCredentials.Add(keyCredential);
            await _context.SaveChangesAsync();

            // Prepare update
            var updateCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderAccountGroup = keyCredential.ProviderAccountGroup,
                ApiKey = keyCredential.ApiKey,
                BaseUrl = keyCredential.BaseUrl,
                IsPrimary = false, // Not explicitly set as primary
                IsEnabled = true // Enable the key
            };

            // Act
            var result = await _repository.UpdateAsync(updateCredential);

            // Assert
            Assert.True(result);
            var updatedKey = await _context.ProviderKeyCredentials.FindAsync(1);
            Assert.True(updatedKey.IsPrimary, "Enabling the only key should automatically set it as primary");
        }

        [Fact]
        public async Task UpdateAsync_WhenEnablingWithOtherEnabledKeys_ShouldNotAutomaticallySetAsPrimary()
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
                Id = 1,
                ProviderId = 1,
                ApiKey = "first-key",
                KeyName = "First Key",
                IsPrimary = true,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create second disabled key
            var secondKey = new ProviderKeyCredential
            {
                Id = 2,
                ProviderId = 1,
                ApiKey = "second-key",
                KeyName = "Second Key",
                IsPrimary = false,
                IsEnabled = false, // Initially disabled
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProviderKeyCredentials.AddRange(firstKey, secondKey);
            await _context.SaveChangesAsync();

            // Prepare update for second key
            var updateCredential = new ProviderKeyCredential
            {
                Id = 2,
                ProviderAccountGroup = secondKey.ProviderAccountGroup,
                ApiKey = secondKey.ApiKey,
                BaseUrl = secondKey.BaseUrl,
                IsPrimary = false, // Not explicitly set as primary
                IsEnabled = true // Enable the key
            };

            // Act
            var result = await _repository.UpdateAsync(updateCredential);

            // Assert
            Assert.True(result);
            var updatedKey = await _context.ProviderKeyCredentials.FindAsync(2);
            Assert.False(updatedKey.IsPrimary, "Enabling a key when other enabled keys exist should not automatically set it as primary");
            
            // Verify first key is still primary
            var firstKeyAfterUpdate = await _context.ProviderKeyCredentials.FindAsync(1);
            Assert.True(firstKeyAfterUpdate.IsPrimary, "First key should remain primary");
        }
    }
}