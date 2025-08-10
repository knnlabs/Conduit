using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Realtime;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class RealtimeControllerTests
    {
        #region GetConnections Tests

        [Fact]
        public async Task GetConnections_WithValidKey_ShouldReturnConnections()
        {
            // Arrange
            var virtualKey = "condt_valid_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "gpt-4o-realtime-preview"
            };

            var expectedConnections = new List<ConduitLLM.Core.Models.Realtime.ConnectionInfo>
            {
                new ConduitLLM.Core.Models.Realtime.ConnectionInfo 
                { 
                    ConnectionId = "conn-1", 
                    Model = "gpt-4o-realtime-preview",
                    ConnectedAt = DateTime.UtcNow,
                    VirtualKey = virtualKey
                },
                new ConduitLLM.Core.Models.Realtime.ConnectionInfo 
                { 
                    ConnectionId = "conn-2", 
                    Model = "gpt-4o-realtime-preview", 
                    ConnectedAt = DateTime.UtcNow,
                    VirtualKey = virtualKey
                }
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.GetActiveConnectionsAsync(keyEntity.Id))
                .ReturnsAsync(expectedConnections);

            // Act  
            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            var result = await _controller.GetConnections();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as ConduitLLM.Http.Controllers.ConnectionStatusResponse;
            Assert.NotNull(response);
            Assert.Equal(keyEntity.Id, response.VirtualKeyId);
            Assert.Equal(expectedConnections, response.ActiveConnections);
        }

        [Fact]
        public async Task GetConnections_WithInvalidKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var virtualKey = "condt_invalid_key";

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync((VirtualKey)null);

            // Act
            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            var result = await _controller.GetConnections();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Invalid virtual key", error.error.ToString());
        }

        [Fact]
        public async Task GetConnections_WithMissingKey_ShouldReturnUnauthorized()
        {
            // Act
            _controller.ControllerContext = CreateControllerContext();
            var result = await _controller.GetConnections();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Virtual key required", error.error.ToString());
        }

        #endregion
    }
}