using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Admin.Tests.Controllers
{
    public class SystemInfoControllerTests
    {
        private readonly Mock<IAdminSystemInfoService> _mockSystemInfoService;
        private readonly Mock<ILogger<SystemInfoController>> _mockLogger;
        private readonly SystemInfoController _controller;

        public SystemInfoControllerTests()
        {
            _mockSystemInfoService = new Mock<IAdminSystemInfoService>();
            _mockLogger = new Mock<ILogger<SystemInfoController>>();
            _controller = new SystemInfoController(_mockSystemInfoService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetSystemInfo_Success_ReturnsOkWithSystemInfo()
        {
            // Arrange
            var systemInfo = new SystemInfoDto
            {
                Version = new VersionInfo
                {
                    AppVersion = "1.0.0",
                    BuildDate = new DateTime(2023, 5, 1)
                },
                OperatingSystem = new OsInfo
                {
                    Description = "Linux 5.10.0",
                    Architecture = "x64"
                },
                Database = new DatabaseInfo
                {
                    Provider = "PostgreSQL",
                    Version = "14.0",
                    Connected = true
                },
                Runtime = new RuntimeInfo
                {
                    RuntimeVersion = "7.0.5",
                    StartTime = DateTime.UtcNow.AddHours(-1),
                    Uptime = TimeSpan.FromHours(1)
                }
            };

            _mockSystemInfoService
                .Setup(s => s.GetSystemInfoAsync())
                .ReturnsAsync(systemInfo);

            // Act
            var result = await _controller.GetSystemInfo();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            var returnValue = Assert.IsType<SystemInfoDto>(okResult.Value);

            Assert.Equal("1.0.0", returnValue.Version.AppVersion);
            Assert.Equal(new DateTime(2023, 5, 1), returnValue.Version.BuildDate);
            Assert.Equal("Linux 5.10.0", returnValue.OperatingSystem.Description);
            Assert.Equal("x64", returnValue.OperatingSystem.Architecture);
            Assert.Equal("PostgreSQL", returnValue.Database.Provider);
            Assert.True(returnValue.Database.Connected);
            Assert.Equal("7.0.5", returnValue.Runtime.RuntimeVersion);
        }

        [Fact]
        public async Task GetSystemInfo_Exception_ReturnsInternalServerError()
        {
            // Arrange
            _mockSystemInfoService
                .Setup(s => s.GetSystemInfoAsync())
                .ThrowsAsync(new Exception("Failed to get system info"));

            // Act
            var result = await _controller.GetSystemInfo();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            Assert.Equal("An unexpected error occurred.", statusCodeResult.Value);
        }

        [Fact]
        public async Task GetHealthStatus_Success_ReturnsOkWithHealthStatus()
        {
            // Arrange
            var healthStatus = new HealthStatusDto
            {
                Status = "Healthy",
                Components = new Dictionary<string, ComponentHealth>
                {
                    ["Database"] = new ComponentHealth
                    {
                        Status = "Healthy",
                        Description = "Database is connected and operational",
                        Data = new Dictionary<string, string>
                        {
                            ["ResponseTime"] = "5ms"
                        }
                    },
                    ["API"] = new ComponentHealth
                    {
                        Status = "Healthy",
                        Description = "API is responsive",
                        Data = new Dictionary<string, string>
                        {
                            ["ActiveRequests"] = "3"
                        }
                    }
                }
            };

            _mockSystemInfoService
                .Setup(s => s.GetHealthStatusAsync())
                .ReturnsAsync(healthStatus);

            // Act
            var result = await _controller.GetHealthStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            var returnValue = Assert.IsType<HealthStatusDto>(okResult.Value);

            Assert.Equal("Healthy", returnValue.Status);
            Assert.Equal(2, returnValue.Components.Count);
            Assert.True(returnValue.Components.ContainsKey("Database"));
            Assert.True(returnValue.Components.ContainsKey("API"));
            Assert.Equal("Healthy", returnValue.Components["Database"].Status);
            Assert.Equal("5ms", returnValue.Components["Database"].Data["ResponseTime"]);
        }

        [Fact]
        public async Task GetHealthStatus_Exception_ReturnsInternalServerError()
        {
            // Arrange
            _mockSystemInfoService
                .Setup(s => s.GetHealthStatusAsync())
                .ThrowsAsync(new Exception("Failed to get health status"));

            // Act
            var result = await _controller.GetHealthStatus();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            Assert.Equal("An unexpected error occurred.", statusCodeResult.Value);
        }
    }
}
