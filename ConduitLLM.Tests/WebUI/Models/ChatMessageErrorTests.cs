using System;
using ConduitLLM.WebUI.Models;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Models
{
    /// <summary>
    /// Tests for error handling in ChatMessage
    /// </summary>
    public class ChatMessageErrorTests
    {
        [Fact]
        public void ChatMessage_WithErrorMessage_IsErrorReturnsTrue()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = "assistant",
                Content = "Error occurred",
                ErrorMessage = "API returned an error: 404 NotFound"
            };

            // Act & Assert
            Assert.True(message.IsError);
            Assert.Equal("API returned an error: 404 NotFound", message.ErrorMessage);
        }

        [Fact]
        public void ChatMessage_WithoutErrorMessage_IsErrorReturnsFalse()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = "assistant",
                Content = "This is a normal response"
            };

            // Act & Assert
            Assert.False(message.IsError);
            Assert.Null(message.ErrorMessage);
        }

        [Fact]
        public void ChatMessage_WithEmptyErrorMessage_IsErrorReturnsFalse()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = "assistant",
                Content = "Normal content",
                ErrorMessage = ""
            };

            // Act & Assert
            Assert.False(message.IsError);
        }

        [Theory]
        [InlineData("No endpoints found that support tool use")]
        [InlineData("Model not found")]
        [InlineData("Rate limit exceeded")]
        [InlineData("Invalid API key")]
        public void ChatMessage_ErrorScenarios_HandledCorrectly(string errorMessage)
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = "assistant",
                Content = "I encountered an error while processing your request.",
                ErrorMessage = errorMessage,
                Timestamp = DateTime.Now
            };

            // Act & Assert
            Assert.True(message.IsError);
            Assert.Equal(errorMessage, message.ErrorMessage);
            Assert.Equal("assistant", message.Role);
            Assert.NotNull(message.Content);
        }
    }
}