using System;
using System.Collections.Generic;
using System.Text.Json;
using ConduitLLM.Core.Models;
using Xunit;

namespace ConduitLLM.Tests.Core.Models
{
    /// <summary>
    /// Tests for the multimodal content capabilities in the Message model.
    /// </summary>
    public class MultimodalContentTests
    {
        [Fact]
        public void Message_WithTextContentPart_SerializesCorrectly()
        {
            // Arrange
            var message = new Message
            {
                Role = "user",
                Content = new List<TextContentPart>
                {
                    new TextContentPart
                    {
                        Text = "Hello, world!"
                    }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(message);
            var deserializedMessage = JsonSerializer.Deserialize<Message>(json);

            // Assert
            Assert.NotNull(deserializedMessage);
            Assert.Equal("user", deserializedMessage.Role);
            
            // We expect Content to be deserialized as JsonElement
            Assert.IsType<JsonElement>(deserializedMessage.Content);
            var content = (JsonElement)deserializedMessage.Content;
            
            // It should be an array with one element
            Assert.Equal(JsonValueKind.Array, content.ValueKind);
            var elements = content.EnumerateArray();
            
            // First element should have type=text and the correct text
            var firstElement = elements.First();
            Assert.Equal("text", firstElement.GetProperty("type").GetString());
            Assert.Equal("Hello, world!", firstElement.GetProperty("text").GetString());
        }

        [Fact]
        public void Message_WithImageContentPart_SerializesCorrectly()
        {
            // Arrange
            var message = new Message
            {
                Role = "user",
                Content = new List<object>
                {
                    new ImageUrlContentPart
                    {
                        ImageUrl = new ImageUrl
                        {
                            Url = "https://example.com/image.jpg",
                            Detail = "high"
                        }
                    }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(message);
            var deserializedMessage = JsonSerializer.Deserialize<Message>(json);

            // Assert
            Assert.NotNull(deserializedMessage);
            Assert.Equal("user", deserializedMessage.Role);
            
            // We expect Content to be deserialized as JsonElement
            Assert.IsType<JsonElement>(deserializedMessage.Content);
            var content = (JsonElement)deserializedMessage.Content;
            
            // It should be an array with one element
            Assert.Equal(JsonValueKind.Array, content.ValueKind);
            var elements = content.EnumerateArray();
            
            // First element should have type=image_url and the correct image_url properties
            var firstElement = elements.First();
            Assert.Equal("image_url", firstElement.GetProperty("type").GetString());
            var imageUrl = firstElement.GetProperty("image_url");
            Assert.Equal("https://example.com/image.jpg", imageUrl.GetProperty("url").GetString());
            Assert.Equal("high", imageUrl.GetProperty("detail").GetString());
        }

        [Fact]
        public void Message_WithMixedContentParts_SerializesCorrectly()
        {
            // Arrange
            var message = new Message
            {
                Role = "user",
                Content = new List<object>
                {
                    new TextContentPart { Text = "What's in this image?" },
                    new ImageUrlContentPart
                    {
                        ImageUrl = new ImageUrl
                        {
                            Url = "https://example.com/image.jpg",
                            Detail = "auto"
                        }
                    },
                    new TextContentPart { Text = "Please describe it in detail." }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(message);
            var deserializedMessage = JsonSerializer.Deserialize<Message>(json);

            // Assert
            Assert.NotNull(deserializedMessage);
            Assert.Equal("user", deserializedMessage.Role);
            
            // We expect Content to be deserialized as JsonElement
            Assert.IsType<JsonElement>(deserializedMessage.Content);
            var content = (JsonElement)deserializedMessage.Content;
            
            // It should be an array with three elements
            Assert.Equal(JsonValueKind.Array, content.ValueKind);
            
            var elements = content.EnumerateArray().ToArray();
            Assert.Equal(3, elements.Length);
            
            // First element should be text
            Assert.Equal("text", elements[0].GetProperty("type").GetString());
            Assert.Equal("What's in this image?", elements[0].GetProperty("text").GetString());
            
            // Second element should be image_url
            Assert.Equal("image_url", elements[1].GetProperty("type").GetString());
            Assert.Equal("https://example.com/image.jpg", elements[1].GetProperty("image_url").GetProperty("url").GetString());
            Assert.Equal("auto", elements[1].GetProperty("image_url").GetProperty("detail").GetString());
            
            // Third element should be text
            Assert.Equal("text", elements[2].GetProperty("type").GetString());
            Assert.Equal("Please describe it in detail.", elements[2].GetProperty("text").GetString());
        }

        [Fact]
        public void Message_WithStringContent_StillWorks()
        {
            // Arrange
            var message = new Message
            {
                Role = "user",
                Content = "This is plain text content"
            };

            // Act
            var json = JsonSerializer.Serialize(message);
            var deserializedMessage = JsonSerializer.Deserialize<Message>(json);

            // Assert
            Assert.NotNull(deserializedMessage);
            Assert.Equal("user", deserializedMessage.Role);
            
            // For string content, we expect it to be deserialized as string
            Assert.IsType<JsonElement>(deserializedMessage.Content);
            var content = (JsonElement)deserializedMessage.Content;
            Assert.Equal(JsonValueKind.String, content.ValueKind);
            Assert.Equal("This is plain text content", content.GetString());
        }

        [Fact]
        public void StringContent_JsonElement_CanBeConvertedToString()
        {
            // Arrange
            var message = new Message
            {
                Role = "user",
                Content = "This is plain text content"
            };

            // Act
            var json = JsonSerializer.Serialize(message);
            var deserializedMessage = JsonSerializer.Deserialize<Message>(json);
            
            // Convert JsonElement to string using extension method
            string? textContent = null;
            
            if (deserializedMessage != null && 
                deserializedMessage.Content is JsonElement element && 
                element.ValueKind == JsonValueKind.String)
            {
                textContent = element.GetString();
            }

            // Assert
            Assert.NotNull(textContent);
            Assert.Equal("This is plain text content", textContent);
        }
    }
}
