using System;
using System.Collections.Generic;
using System.Linq;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConduitLLM.Tests.WebUI.Controllers
{
    public class AuthControllerTests : IDisposable
    {
        private readonly Mock<IGlobalSettingService> _mockSettingService;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly Mock<ISecurityService> _mockSecurityService;
        private readonly AuthController _controller;
        private readonly DefaultHttpContext _httpContext;

        public AuthControllerTests()
        {
            _mockSettingService = new Mock<IGlobalSettingService>();
            _mockLogger = new Mock<ILogger<AuthController>>();
            _mockSecurityService = new Mock<ISecurityService>();
            
            _controller = new AuthController(
                _mockSettingService.Object,
                _mockLogger.Object,
                _mockSecurityService.Object
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
            var json = JsonConvert.SerializeObject(okResult.Value);
            var response = JObject.Parse(json);
            Assert.True(response["success"]!.Value<bool>());
            Assert.Equal("/", response["redirectUrl"]!.Value<string>());
            _mockSecurityService.Verify(x => x.ClearFailedLoginAttemptsAsync("127.0.0.1"), Times.Once);
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
            var json = JsonConvert.SerializeObject(okResult.Value);
            var response = JObject.Parse(json);
            Assert.True(response["success"]!.Value<bool>());
            Assert.Equal("/dashboard", response["redirectUrl"]!.Value<string>());
            _mockSecurityService.Verify(x => x.ClearFailedLoginAttemptsAsync("127.0.0.1"), Times.Once);
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
            var json = JsonConvert.SerializeObject(unauthorizedResult.Value);
            var response = JObject.Parse(json);
            Assert.Equal("Invalid master key", response["message"]!.Value<string>());
            _mockSecurityService.Verify(x => x.RecordFailedLoginAsync("127.0.0.1"), Times.Once);
            _mockSecurityService.Verify(x => x.ClearFailedLoginAttemptsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_WithBannedIP_ReturnsTooManyRequests()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", "test-key");
            var request = new LoginRequest { MasterKey = "test-key" };
            _mockSecurityService.Setup(x => x.IsIpBannedAsync("127.0.0.1")).ReturnsAsync(true);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(429, statusResult.StatusCode);
            var json = JsonConvert.SerializeObject(statusResult.Value);
            var response = JObject.Parse(json);
            Assert.Equal("Too many failed login attempts. Please try again later.", response["message"]!.Value<string>());
            _mockSecurityService.Verify(x => x.RecordFailedLoginAsync(It.IsAny<string>()), Times.Never);
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
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var response = JObject.Parse(json);
            Assert.Equal("Master key is required", response["message"]!.Value<string>());
            _mockSecurityService.Verify(x => x.RecordFailedLoginAsync(It.IsAny<string>()), Times.Never);
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
            _mockSecurityService.Verify(x => x.RecordFailedLoginAsync("192.168.1.100"), Times.Once);
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
            _mockSecurityService.Verify(x => x.RecordFailedLoginAsync("192.168.1.200"), Times.Once);
        }

        [Fact]
        public async Task Login_WithStoredHash_ValidatesSuccessfully()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", null);
            Environment.SetEnvironmentVariable("CONDUIT_MASTER_KEY", null);
            
            // SHA256 hash of "test"
            var hashedPassword = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
            _mockSettingService.Setup(x => x.GetMasterKeyHashAsync())
                .ReturnsAsync(hashedPassword);
            _mockSettingService.Setup(x => x.GetMasterKeyHashAlgorithmAsync())
                .ReturnsAsync("SHA256");
            
            var request = new LoginRequest { MasterKey = "test" };

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonConvert.SerializeObject(okResult.Value);
            var response = JObject.Parse(json);
            Assert.True(response["success"]!.Value<bool>());
        }

        [Fact]
        public async Task Logout_ReturnsSuccess()
        {
            // Act
            var result = await _controller.Logout();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonConvert.SerializeObject(okResult.Value);
            var response = JObject.Parse(json);
            Assert.True(response["success"]!.Value<bool>());
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
            var json = JsonConvert.SerializeObject(okResult.Value);
            var response = JObject.Parse(json);
            Assert.True(response["isAuthenticated"]!.Value<bool>());
            Assert.Equal("Admin", response["username"]!.Value<string>());
            var roles = response["roles"]!.ToObject<List<string>>();
            Assert.NotNull(roles);
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
            var json = JsonConvert.SerializeObject(okResult.Value);
            var response = JObject.Parse(json);
            Assert.False(response["isAuthenticated"]!.Value<bool>());
        }

        public void Dispose()
        {
            // Clean up environment variables
            Environment.SetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY", null);
            Environment.SetEnvironmentVariable("CONDUIT_MASTER_KEY", null);
        }
    }
}