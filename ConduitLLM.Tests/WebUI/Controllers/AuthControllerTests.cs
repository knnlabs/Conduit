using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.WebUI.Controllers;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.Tests.WebUI.Controllers
{
    public class AuthControllerTests : IDisposable
    {
        private readonly Mock<IGlobalSettingService> _mockSettingService;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly Mock<IFailedLoginTrackingService> _mockFailedLoginTracking;
        private readonly AuthController _controller;
        private readonly DefaultHttpContext _httpContext;

        public AuthControllerTests()
        {
            _mockSettingService = new Mock<IGlobalSettingService>();
            _mockLogger = new Mock<ILogger<AuthController>>();
            _mockFailedLoginTracking = new Mock<IFailedLoginTrackingService>();
            
            _controller = new AuthController(
                _mockSettingService.Object,
                _mockLogger.Object,
                _mockFailedLoginTracking.Object
            );

            // Setup HTTP context with authentication service
            var serviceProvider = new Mock<IServiceProvider>();
            var authService = new Mock<IAuthenticationService>();
            serviceProvider
                .Setup(sp => sp.GetService(typeof(IAuthenticationService)))
                .Returns(authService.Object);

            _httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider.Object,
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
            };
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            };
        }

        [Fact]
        public async Task Login_WithValidWebUIKey_ReturnsSuccess()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", "test-webui-key");
            var request = new LoginRequest 
            { 
                MasterKey = "test-webui-key",
                RememberMe = false,
                ReturnUrl = "/"
            };

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.True(response.success);
            Assert.Equal("/", response.redirectUrl);
            _mockFailedLoginTracking.Verify(x => x.ClearFailedAttempts("127.0.0.1"), Times.Once);
        }

        [Fact]
        public async Task Login_WithValidMasterKeyWhenWebUIKeyNotSet_ReturnsSuccess()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", null);
            Environment.SetEnvironmentVariable("CONDUIT_MASTER_KEY", "test-master-key");
            var request = new LoginRequest 
            { 
                MasterKey = "test-master-key",
                RememberMe = true,
                ReturnUrl = "/dashboard"
            };

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.True(response.success);
            Assert.Equal("/dashboard", response.redirectUrl);
            _mockFailedLoginTracking.Verify(x => x.ClearFailedAttempts("127.0.0.1"), Times.Once);
        }

        [Fact]
        public async Task Login_WithInvalidKey_ReturnsUnauthorizedAndTracksFailure()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", "correct-key");
            var request = new LoginRequest { MasterKey = "wrong-key" };

            // Act
            var result = await _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            dynamic response = unauthorizedResult.Value;
            Assert.Equal("Invalid master key", response.message);
            _mockFailedLoginTracking.Verify(x => x.RecordFailedLogin("127.0.0.1"), Times.Once);
            _mockFailedLoginTracking.Verify(x => x.ClearFailedAttempts(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_WithBannedIP_ReturnsTooManyRequests()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", "test-key");
            var request = new LoginRequest { MasterKey = "test-key" };
            _mockFailedLoginTracking.Setup(x => x.IsBanned("127.0.0.1")).Returns(true);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(429, statusResult.StatusCode);
            dynamic response = statusResult.Value;
            Assert.Equal("Too many failed login attempts. Please try again later.", response.message);
            _mockFailedLoginTracking.Verify(x => x.RecordFailedLogin(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_WithEmptyMasterKey_ReturnsBadRequest()
        {
            // Arrange
            var request = new LoginRequest { MasterKey = "" };

            // Act
            var result = await _controller.Login(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic response = badRequestResult.Value;
            Assert.Equal("Master key is required", response.message);
            _mockFailedLoginTracking.Verify(x => x.RecordFailedLogin(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_ExtractsIPFromXForwardedForHeader()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", "test-key");
            _httpContext.Request.Headers["X-Forwarded-For"] = "192.168.1.100, 10.0.0.1";
            var request = new LoginRequest { MasterKey = "wrong-key" };

            // Act
            await _controller.Login(request);

            // Assert
            _mockFailedLoginTracking.Verify(x => x.RecordFailedLogin("192.168.1.100"), Times.Once);
        }

        [Fact]
        public async Task Login_ExtractsIPFromXRealIPHeader()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", "test-key");
            _httpContext.Request.Headers["X-Real-IP"] = "192.168.1.200";
            var request = new LoginRequest { MasterKey = "wrong-key" };

            // Act
            await _controller.Login(request);

            // Assert
            _mockFailedLoginTracking.Verify(x => x.RecordFailedLogin("192.168.1.200"), Times.Once);
        }

        [Fact]
        public async Task Login_WithStoredHash_ValidatesSuccessfully()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", null);
            Environment.SetEnvironmentVariable("CONDUIT_MASTER_KEY", null);
            
            // SHA256 hash of "test-password"
            var hashedPassword = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
            _mockSettingService.Setup(x => x.GetMasterKeyHashAsync())
                .ReturnsAsync(hashedPassword);
            _mockSettingService.Setup(x => x.GetMasterKeyHashAlgorithmAsync())
                .ReturnsAsync("SHA256");
            
            var request = new LoginRequest { MasterKey = "test-password" };

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.True(response.success);
        }

        [Fact]
        public async Task Logout_ReturnsSuccess()
        {
            // Act
            var result = await _controller.Logout();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.True(response.success);
        }

        [Fact]
        public void GetCurrentUser_WhenAuthenticated_ReturnsUserInfo()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "Admin"),
                new Claim(ClaimTypes.Role, "Administrator"),
                new Claim(ClaimTypes.Role, "User")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            _httpContext.User = principal;

            // Act
            var result = _controller.GetCurrentUser();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.True(response.isAuthenticated);
            Assert.Equal("Admin", response.username);
            var rolesList = response.roles as IEnumerable<object>;
            Assert.NotNull(rolesList);
            var roles = rolesList.Select(r => r.ToString()).ToList();
            Assert.Contains("Administrator", roles);
            Assert.Contains("User", roles);
        }

        [Fact]
        public void GetCurrentUser_WhenNotAuthenticated_ReturnsNotAuthenticated()
        {
            // Arrange
            _httpContext.User = new ClaimsPrincipal();

            // Act
            var result = _controller.GetCurrentUser();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.False(response.isAuthenticated);
        }

        public void Dispose()
        {
            // Clean up environment variables
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", null);
            Environment.SetEnvironmentVariable("CONDUIT_MASTER_KEY", null);
        }
    }
}