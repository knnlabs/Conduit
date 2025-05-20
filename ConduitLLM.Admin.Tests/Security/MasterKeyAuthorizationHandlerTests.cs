using ConduitLLM.Admin.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace ConduitLLM.Admin.Tests.Security
{
    public class MasterKeyAuthorizationHandlerTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<MasterKeyAuthorizationHandler>> _mockLogger;
        private readonly TestMasterKeyAuthorizationHandler _handler;
        private readonly AuthorizationHandlerContext _context;
        private readonly DefaultHttpContext _httpContext;

        public MasterKeyAuthorizationHandlerTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<MasterKeyAuthorizationHandler>>();
            _handler = new TestMasterKeyAuthorizationHandler(_mockConfiguration.Object, _mockLogger.Object);
            
            // Set up a mock HTTP context
            _httpContext = new DefaultHttpContext();
            
            // Create a requirements collection with the MasterKeyRequirement
            var requirements = new[] { new MasterKeyRequirement() };
            
            // Create an empty user (not relevant for this authorization type)
            var user = new ClaimsPrincipal(new ClaimsIdentity());
            
            // Create the authorization context
            _context = new AuthorizationHandlerContext(requirements, user, _httpContext);
        }

        [Fact]
        public async Task HandleRequirementAsync_ValidMasterKey_SucceedsRequirement()
        {
            // Arrange
            string configKey = "AdminApi:MasterKey";
            string masterKey = "test-master-key";
            
            _mockConfiguration
                .Setup(c => c[configKey])
                .Returns(masterKey);
            
            _httpContext.Request.Headers["X-API-Key"] = masterKey;

            // Act
            await _handler.TestHandleRequirementAsync(_context, new MasterKeyRequirement());

            // Assert
            Assert.True(_context.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_InvalidMasterKey_DoesNotSucceedRequirement()
        {
            // Arrange
            string configKey = "AdminApi:MasterKey";
            string masterKey = "test-master-key";
            string invalidKey = "invalid-key";
            
            _mockConfiguration
                .Setup(c => c[configKey])
                .Returns(masterKey);
            
            _httpContext.Request.Headers["X-API-Key"] = invalidKey;

            // Act
            await _handler.TestHandleRequirementAsync(_context, new MasterKeyRequirement());

            // Assert
            Assert.False(_context.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_MissingMasterKeyHeader_DoesNotSucceedRequirement()
        {
            // Arrange
            string configKey = "AdminApi:MasterKey";
            string masterKey = "test-master-key";
            
            _mockConfiguration
                .Setup(c => c[configKey])
                .Returns(masterKey);
            
            // No header added to the request

            // Act
            await _handler.TestHandleRequirementAsync(_context, new MasterKeyRequirement());

            // Assert
            Assert.False(_context.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_EmptyMasterKeyConfig_DoesNotSucceedRequirement()
        {
            // Arrange
            string configKey = "AdminApi:MasterKey";
            string emptyKey = "";
            
            _mockConfiguration
                .Setup(c => c[configKey])
                .Returns(emptyKey);
            
            _httpContext.Request.Headers["X-API-Key"] = "any-key";

            // Act
            await _handler.TestHandleRequirementAsync(_context, new MasterKeyRequirement());

            // Assert
            Assert.False(_context.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_NullMasterKeyConfig_DoesNotSucceedRequirement()
        {
            // Arrange
            string configKey = "AdminApi:MasterKey";
            
            _mockConfiguration
                .Setup(c => c[configKey])
                .Returns((string)null);
            
            _httpContext.Request.Headers["X-API-Key"] = "any-key";

            // Act
            await _handler.TestHandleRequirementAsync(_context, new MasterKeyRequirement());

            // Assert
            Assert.False(_context.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_NoHttpContext_DoesNotSucceedRequirement()
        {
            // Arrange - create a context without HTTP context
            var requirements = new[] { new MasterKeyRequirement() };
            var user = new ClaimsPrincipal(new ClaimsIdentity());
            var contextWithoutHttp = new AuthorizationHandlerContext(requirements, user, null);

            // Act
            await _handler.TestHandleRequirementAsync(contextWithoutHttp, new MasterKeyRequirement());

            // Assert
            Assert.False(contextWithoutHttp.HasSucceeded);
        }
    }
}