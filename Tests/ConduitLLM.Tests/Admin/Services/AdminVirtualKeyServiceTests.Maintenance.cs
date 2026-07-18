using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        #region PerformMaintenanceAsync Tests

        [Fact]
        public async Task PerformMaintenanceAsync_ProcessesExpiredKeys()
        {
            // Arrange
            var now = DateTime.UtcNow;
            await using (var seed = await _dbContextFactory.CreateDbContextAsync())
            {
                seed.VirtualKeyGroups.Add(new VirtualKeyGroup
                {
                    Id = 1,
                    GroupName = "Test Group"
                });
                seed.VirtualKeys.AddRange(
                    new VirtualKey
                    {
                        Id = 1,
                        KeyName = "Expired Key",
                        KeyHash = "hash-expired",
                        IsEnabled = true,
                        ExpiresAt = now.AddDays(-1),
                        VirtualKeyGroupId = 1
                    },
                    new VirtualKey
                    {
                        Id = 2,
                        KeyName = "Valid Key",
                        KeyHash = "hash-valid",
                        IsEnabled = true,
                        ExpiresAt = now.AddDays(30),
                        VirtualKeyGroupId = 1
                    });
                await seed.SaveChangesAsync();
            }

            // Act
            await _service.PerformMaintenanceAsync();

            // Assert — bulk update disables the expired key in the DB; the valid key is untouched.
            await using var verify = await _dbContextFactory.CreateDbContextAsync();
            var expired = await verify.VirtualKeys.FindAsync(1);
            var valid = await verify.VirtualKeys.FindAsync(2);

            Assert.NotNull(expired);
            Assert.NotNull(valid);
            Assert.False(expired!.IsEnabled);
            Assert.True(valid!.IsEnabled);
        }

        #endregion
    }
}
