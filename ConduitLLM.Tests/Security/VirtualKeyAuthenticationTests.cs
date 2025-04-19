using System;
using System.Security.Claims;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Http.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Security
{
    public class VirtualKeyAuthenticationTests
    {
        private DbContextOptions<ConfigurationDbContext> GetDbOptions()
        {
            return new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }
        
        [Fact]
        public async Task ValidateVirtualKey_ReturnsFalse_WhenKeyDoesNotExist()
        {
            // Arrange
            var options = GetDbOptions();
            using var context = new ConfigurationDbContext(options);
            context.IsTestEnvironment = true;
            var service = new VirtualKeyService(context);
            
            // Act
            var result = await service.ValidateVirtualKeyAsync("nonexistent_key");
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task ValidateVirtualKey_ReturnsFalse_WhenKeyIsDisabled()
        {
            // Arrange
            var options = GetDbOptions();
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                context.VirtualKeys.Add(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Disabled Key",
                    KeyHash = "vk_disabled",
                    IsEnabled = false
                });
                await context.SaveChangesAsync();
            }
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                var service = new VirtualKeyService(context);
                
                // Act
                var result = await service.ValidateVirtualKeyAsync("vk_disabled");
                
                // Assert
                Assert.False(result);
            }
        }
        
        [Fact]
        public async Task ValidateVirtualKey_ReturnsFalse_WhenKeyIsExpired()
        {
            // Arrange
            var options = GetDbOptions();
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                context.VirtualKeys.Add(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Expired Key",
                    KeyHash = "vk_expired",
                    IsEnabled = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(-1)
                });
                await context.SaveChangesAsync();
            }
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                var service = new VirtualKeyService(context);
                
                // Act
                var result = await service.ValidateVirtualKeyAsync("vk_expired");
                
                // Assert
                Assert.False(result);
            }
        }
        
        [Fact]
        public async Task ValidateVirtualKey_ReturnsFalse_WhenBudgetExceeded()
        {
            // Arrange
            var options = GetDbOptions();
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                context.VirtualKeys.Add(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Over Budget Key",
                    KeyHash = "vk_over_budget",
                    IsEnabled = true,
                    MaxBudget = 10.0m,
                    CurrentSpend = 10.5m, // Over the limit
                    BudgetDuration = "monthly"
                });
                await context.SaveChangesAsync();
            }
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                var service = new VirtualKeyService(context);
                
                // Act
                var result = await service.ValidateVirtualKeyAsync("vk_over_budget");
                
                // Assert
                Assert.False(result);
            }
        }
        
        [Fact]
        public async Task ValidateVirtualKey_ReturnsTrue_ForValidKey()
        {
            // Arrange
            var options = GetDbOptions();
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                context.VirtualKeys.Add(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Valid Key",
                    KeyHash = "vk_valid",
                    IsEnabled = true,
                    MaxBudget = 10.0m,
                    CurrentSpend = 5.0m,
                    BudgetDuration = "monthly",
                    ExpiresAt = DateTime.UtcNow.AddDays(30)
                });
                await context.SaveChangesAsync();
            }
            
            using (var context = new ConfigurationDbContext(options))
            {
                context.IsTestEnvironment = true;
                var service = new VirtualKeyService(context);
                
                // Act
                var result = await service.ValidateVirtualKeyAsync("vk_valid");
                
                // Assert
                Assert.True(result);
            }
        }
        
        [Fact]
        public async Task MasterKeyAuthorizationHandler_Succeeds_WithValidMasterKey()
        {
            // Arrange
            var mockGlobalSettingService = new Mock<IGlobalSettingService>();
            mockGlobalSettingService
                .Setup(s => s.GetSettingAsync("MasterKey"))
                .ReturnsAsync("master_key_123");
                
            var mockLogger = new Mock<ILogger<MasterKeyAuthorizationHandler>>();
            
            var handler = new MasterKeyAuthorizationHandler(
                mockGlobalSettingService.Object,
                mockLogger.Object);
                
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Master-Key"] = "master_key_123";
            
            var context = new AuthorizationHandlerContext(
                new[] { new MasterKeyRequirement() },
                new ClaimsPrincipal(),
                httpContext);
                
            // Act
            await handler.HandleAsync(context);
            
            // Assert
            Assert.True(context.HasSucceeded);
        }
        
        [Fact]
        public async Task MasterKeyAuthorizationHandler_Fails_WithInvalidMasterKey()
        {
            // Arrange
            var mockGlobalSettingService = new Mock<IGlobalSettingService>();
            mockGlobalSettingService
                .Setup(s => s.GetSettingAsync("MasterKey"))
                .ReturnsAsync("correct_master_key");
                
            var mockLogger = new Mock<ILogger<MasterKeyAuthorizationHandler>>();
            
            var handler = new MasterKeyAuthorizationHandler(
                mockGlobalSettingService.Object,
                mockLogger.Object);
                
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Master-Key"] = "wrong_master_key";
            
            var context = new AuthorizationHandlerContext(
                new[] { new MasterKeyRequirement() },
                new ClaimsPrincipal(),
                httpContext);
                
            // Act
            await handler.HandleAsync(context);
            
            // Assert
            Assert.False(context.HasSucceeded);
        }
    }
}
