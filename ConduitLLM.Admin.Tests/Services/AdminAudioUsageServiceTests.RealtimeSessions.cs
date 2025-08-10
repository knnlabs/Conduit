using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        #region Realtime Session Tests

        [Fact]
        public async Task GetRealtimeSessionMetricsAsync_WithActiveSessions_ShouldReturnMetrics()
        {
            // Arrange
            var sessions = CreateSampleRealtimeSessions(5);
            _mockSessionStore.Setup(x => x.GetActiveSessionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessions);

            // Act
            var result = await _service.GetRealtimeSessionMetricsAsync();

            // Assert
            result.Should().NotBeNull();
            result.ActiveSessions.Should().Be(5);
            result.SessionsByProvider.Should().ContainKey("openai");
            result.SessionsByProvider["openai"].Should().Be(3);
            result.SessionsByProvider["ultravox"].Should().Be(2);
            result.SuccessRate.Should().Be(80); // 4 successful out of 5
            result.AverageTurnsPerSession.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetRealtimeSessionMetricsAsync_WithNoSessionStore_ShouldReturnEmptyMetrics()
        {
            // Arrange
            var mockScopedProvider = new Mock<IServiceProvider>();
            mockScopedProvider.Setup(x => x.GetService(typeof(IRealtimeSessionStore)))
                .Returns(null);
            
            var mockScope = new Mock<IServiceScope>();
            mockScope.Setup(x => x.ServiceProvider).Returns(mockScopedProvider.Object);
            
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            
            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(mockScopeFactory.Object);

            // Act
            var result = await _service.GetRealtimeSessionMetricsAsync();

            // Assert
            result.ActiveSessions.Should().Be(0);
            result.SessionsByProvider.Should().BeEmpty();
            result.SuccessRate.Should().Be(100);
        }

        [Fact]
        public async Task GetActiveSessionsAsync_ShouldReturnSessionDtos()
        {
            // Arrange
            var sessions = CreateSampleRealtimeSessions(3);
            _mockSessionStore.Setup(x => x.GetActiveSessionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessions);

            // Act
            var result = await _service.GetActiveSessionsAsync();

            // Assert
            result.Should().HaveCount(3);
            result.First().SessionId.Should().Be("session-1");
            result.First().ProviderId.Should().Be(18); // First session (i=0) uses provider ID 18 based on CreateSampleRealtimeSessions logic
            result.First().State.Should().Be(SessionState.Connected.ToString());
        }

        [Fact]
        public async Task GetSessionDetailsAsync_WithValidSessionId_ShouldReturnSession()
        {
            // Arrange
            var sessionId = "session-123";
            var session = CreateRealtimeSession(sessionId, "openai");
            
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            // Act
            var result = await _service.GetSessionDetailsAsync(sessionId);

            // Assert
            result.Should().NotBeNull();
            result!.SessionId.Should().Be(sessionId);
            result.ProviderId.Should().Be(1); // Provider ID 1 for OpenAI
        }

        [Fact]
        public async Task GetSessionDetailsAsync_WithInvalidSessionId_ShouldReturnNull()
        {
            // Arrange
            var sessionId = "non-existent";
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RealtimeSession?)null);

            // Act
            var result = await _service.GetSessionDetailsAsync(sessionId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task TerminateSessionAsync_WithValidSession_ShouldTerminate()
        {
            // Arrange
            var sessionId = "session-to-terminate";
            var session = CreateRealtimeSession(sessionId, "openai");
            
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _mockSessionStore.Setup(x => x.UpdateSessionAsync(It.IsAny<RealtimeSession>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockSessionStore.Setup(x => x.RemoveSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.TerminateSessionAsync(sessionId);

            // Assert
            result.Should().BeTrue();
            _mockSessionStore.Verify(x => x.UpdateSessionAsync(It.Is<RealtimeSession>(s => 
                s.State == SessionState.Closed), It.IsAny<CancellationToken>()), Times.Once);
            _mockSessionStore.Verify(x => x.RemoveSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TerminateSessionAsync_WithNonExistentSession_ShouldReturnFalse()
        {
            // Arrange
            var sessionId = "non-existent";
            _mockSessionStore.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RealtimeSession?)null);

            // Act
            var result = await _service.TerminateSessionAsync(sessionId);

            // Assert
            result.Should().BeFalse();
            _mockSessionStore.Verify(x => x.RemoveSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion
    }
}