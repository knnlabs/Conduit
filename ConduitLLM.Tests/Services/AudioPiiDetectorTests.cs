using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for AudioPiiDetector to ensure accurate PII detection and redaction.
    /// </summary>
    public class AudioPiiDetectorTests
    {
        private readonly Mock<ILogger<AudioPiiDetector>> _mockLogger;
        private readonly Mock<IAudioAuditLogger> _mockAuditLogger;
        private readonly AudioPiiDetector _service;

        public AudioPiiDetectorTests()
        {
            _mockLogger = new Mock<ILogger<AudioPiiDetector>>();
            _mockAuditLogger = new Mock<IAudioAuditLogger>();
            _service = new AudioPiiDetector(_mockLogger.Object, _mockAuditLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new AudioPiiDetector(null, _mockAuditLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullAuditLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new AudioPiiDetector(_mockLogger.Object, null));
        }

        [Fact]
        public async Task DetectPiiAsync_WithEmptyText_ReturnsEmptyResult()
        {
            // Act
            var result = await _service.DetectPiiAsync("");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.ContainsPii);
            Assert.Empty(result.Entities);
            Assert.Equal(0, result.RiskScore);
        }

        [Fact]
        public async Task DetectPiiAsync_WithNullText_ReturnsEmptyResult()
        {
            // Act
            var result = await _service.DetectPiiAsync(null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.ContainsPii);
            Assert.Empty(result.Entities);
            Assert.Equal(0, result.RiskScore);
        }

        [Fact]
        public async Task DetectPiiAsync_WithCleanText_ReturnsNoPii()
        {
            // Arrange
            var text = "This is a normal conversation about weather and sports. No sensitive information here.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.ContainsPii);
            Assert.Empty(result.Entities);
            Assert.Equal(0, result.RiskScore);
        }

        [Fact]
        public async Task DetectPiiAsync_WithSSN_DetectsCorrectly()
        {
            // Arrange
            var text = "My social security number is 123-45-6789 and I need help.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Single(result.Entities);
            
            var entity = result.Entities.First();
            Assert.Equal(PiiType.SSN, entity.Type);
            Assert.Equal("123-45-6789", entity.Text);
            Assert.True(entity.StartIndex >= 0);
            Assert.True(entity.EndIndex > entity.StartIndex);
            Assert.True(entity.Confidence > 0);
        }

        [Fact]
        public async Task DetectPiiAsync_WithSSNNoHyphens_DetectsCorrectly()
        {
            // Arrange
            var text = "SSN: 123456789 for verification";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Single(result.Entities);
            
            var entity = result.Entities.First();
            Assert.Equal(PiiType.SSN, entity.Type);
            Assert.Equal("123456789", entity.Text);
        }

        [Fact]
        public async Task DetectPiiAsync_WithCreditCard_DetectsCorrectly()
        {
            // Arrange
            var text = "Please charge my card 4532-1234-5678-9012 for the service.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Single(result.Entities);
            
            var entity = result.Entities.First();
            Assert.Equal(PiiType.CreditCard, entity.Type);
            Assert.Equal("4532-1234-5678-9012", entity.Text);
        }

        [Fact]
        public async Task DetectPiiAsync_WithEmail_DetectsCorrectly()
        {
            // Arrange
            var text = "Send the report to john.doe@example.com when ready.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Single(result.Entities);
            
            var entity = result.Entities.First();
            Assert.Equal(PiiType.Email, entity.Type);
            Assert.Equal("john.doe@example.com", entity.Text);
        }

        [Fact]
        public async Task DetectPiiAsync_WithPhoneNumber_DetectsCorrectly()
        {
            // Arrange
            var text = "Call me at (555) 123-4567 tomorrow.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Single(result.Entities);
            
            var entity = result.Entities.First();
            Assert.Equal(PiiType.Phone, entity.Type);
            Assert.Equal("(555) 123-4567", entity.Text);
        }

        [Fact]
        public async Task DetectPiiAsync_WithDateOfBirth_DetectsCorrectly()
        {
            // Arrange
            var text = "My birthday is 05/15/1985 if you need it.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Single(result.Entities);
            
            var entity = result.Entities.First();
            Assert.Equal(PiiType.DateOfBirth, entity.Type);
            Assert.Equal("05/15/1985", entity.Text);
        }

        [Fact]
        public async Task DetectPiiAsync_WithMultiplePiiTypes_DetectsAll()
        {
            // Arrange
            var text = "Contact John at john@test.com or call 555-123-4567. His SSN is 123-45-6789.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Equal(3, result.Entities.Count);
            
            Assert.Contains(result.Entities, e => e.Type == PiiType.Email);
            Assert.Contains(result.Entities, e => e.Type == PiiType.Phone);
            Assert.Contains(result.Entities, e => e.Type == PiiType.SSN);
            
            // Entities should be ordered by start index
            Assert.True(result.Entities[0].StartIndex <= result.Entities[1].StartIndex);
            Assert.True(result.Entities[1].StartIndex <= result.Entities[2].StartIndex);
        }

        [Fact]
        public async Task DetectPiiAsync_WithPii_LogsWarning()
        {
            // Arrange
            var text = "My SSN is 123-45-6789";

            // Act
            await _service.DetectPiiAsync(text);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Detected") && v.ToString().Contains("PII entities")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RedactPiiAsync_WithNoPii_ReturnsOriginalText()
        {
            // Arrange
            var text = "This is clean text with no PII.";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = false,
                Entities = new List<PiiEntity>()
            };

            // Act
            var result = await _service.RedactPiiAsync(text, detectionResult);

            // Assert
            Assert.Equal(text, result);
        }

        [Fact]
        public async Task RedactPiiAsync_WithMaskMethod_MasksCorrectly()
        {
            // Arrange
            var text = "My SSN is 123-45-6789 for reference.";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = true,
                Entities = new List<PiiEntity>
                {
                    new PiiEntity
                    {
                        Type = PiiType.SSN,
                        Text = "123-45-6789",
                        StartIndex = 10,
                        EndIndex = 21
                    }
                }
            };
            var options = new PiiRedactionOptions
            {
                Method = RedactionMethod.Mask,
                PreserveLength = true,
                MaskCharacter = '*'
            };

            // Act
            var result = await _service.RedactPiiAsync(text, detectionResult, options);

            // Assert
            Assert.Equal("My SSN is *********** for reference.", result);
        }

        [Fact]
        public async Task RedactPiiAsync_WithPlaceholderMethod_ReplacesWithPlaceholder()
        {
            // Arrange
            var text = "Email me at john@test.com please.";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = true,
                Entities = new List<PiiEntity>
                {
                    new PiiEntity
                    {
                        Type = PiiType.Email,
                        Text = "john@test.com",
                        StartIndex = 12,
                        EndIndex = 25
                    }
                }
            };
            var options = new PiiRedactionOptions
            {
                Method = RedactionMethod.Placeholder
            };

            // Act
            var result = await _service.RedactPiiAsync(text, detectionResult, options);

            // Assert
            Assert.Equal("Email me at [Email] please.", result);
        }

        [Fact]
        public async Task RedactPiiAsync_WithRemoveMethod_RemovesText()
        {
            // Arrange
            var text = "Call (555) 123-4567 now.";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = true,
                Entities = new List<PiiEntity>
                {
                    new PiiEntity
                    {
                        Type = PiiType.Phone,
                        Text = "(555) 123-4567",
                        StartIndex = 5,
                        EndIndex = 19
                    }
                }
            };
            var options = new PiiRedactionOptions
            {
                Method = RedactionMethod.Remove
            };

            // Act
            var result = await _service.RedactPiiAsync(text, detectionResult, options);

            // Assert
            Assert.Equal("Call  now.", result);
        }

        [Fact]
        public async Task RedactPiiAsync_WithCustomMethod_UsesCustomReplacement()
        {
            // Arrange
            var text = "My SSN is 123-45-6789.";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = true,
                Entities = new List<PiiEntity>
                {
                    new PiiEntity
                    {
                        Type = PiiType.SSN,
                        Text = "123-45-6789",
                        StartIndex = 10,
                        EndIndex = 21
                    }
                }
            };
            var options = new PiiRedactionOptions
            {
                Method = RedactionMethod.Custom,
                CustomReplacements = new Dictionary<PiiType, string>
                {
                    { PiiType.SSN, "[REDACTED_SSN]" }
                }
            };

            // Act
            var result = await _service.RedactPiiAsync(text, detectionResult, options);

            // Assert
            Assert.Equal("My SSN is [REDACTED_SSN].", result);
        }

        [Fact]
        public async Task RedactPiiAsync_WithMultipleEntities_RedactsAll()
        {
            // Arrange
            var text = "Contact john@test.com or call 555-123-4567.";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = true,
                Entities = new List<PiiEntity>
                {
                    new PiiEntity
                    {
                        Type = PiiType.Email,
                        Text = "john@test.com",
                        StartIndex = 8,
                        EndIndex = 21
                    },
                    new PiiEntity
                    {
                        Type = PiiType.Phone,
                        Text = "555-123-4567",
                        StartIndex = 30,
                        EndIndex = 42
                    }
                }
            };
            var options = new PiiRedactionOptions
            {
                Method = RedactionMethod.Placeholder
            };

            // Act
            var result = await _service.RedactPiiAsync(text, detectionResult, options);

            // Assert
            Assert.Equal("Contact [Email] or call [Phone].", result);
        }

        [Fact]
        public async Task RedactPiiAsync_LogsInformation()
        {
            // Arrange
            var text = "SSN: 123-45-6789";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = true,
                Entities = new List<PiiEntity>
                {
                    new PiiEntity
                    {
                        Type = PiiType.SSN,
                        Text = "123-45-6789",
                        StartIndex = 5,
                        EndIndex = 16
                    }
                }
            };

            // Act
            await _service.RedactPiiAsync(text, detectionResult);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Redacted") && v.ToString().Contains("PII entities")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task DetectPiiAsync_WithCancellationToken_SupportsCorrectSignature()
        {
            // Arrange
            var text = "Test text for cancellation";
            var cts = new CancellationTokenSource();

            // Act - should complete before cancellation
            var result = await _service.DetectPiiAsync(text, cts.Token);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task DetectPiiAsync_CalculatesRiskScore()
        {
            // Arrange
            var text = "SSN: 123-45-6789, Email: test@test.com, Phone: 555-123-4567";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.True(result.RiskScore > 0);
            Assert.Equal(3, result.Entities.Count);
        }

        [Theory]
        [InlineData("123-45-6789")]      // SSN with hyphens
        [InlineData("123456789")]        // SSN without hyphens
        [InlineData("4532123456789012")] // Credit card
        [InlineData("test@email.com")]   // Email
        [InlineData("555-123-4567")]     // Phone
        [InlineData("05/15/1985")]       // Date of birth
        public async Task DetectPiiAsync_DetectsVariousPiiFormats(string piiText)
        {
            // Arrange
            var text = $"Here is some PII: {piiText} in the text.";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.NotEmpty(result.Entities);
            Assert.Contains(result.Entities, e => e.Text == piiText);
        }

        [Fact]
        public async Task RedactPiiAsync_WithDefaultOptions_UsesMaskMethod()
        {
            // Arrange
            var text = "My SSN is 123-45-6789.";
            var detectionResult = new PiiDetectionResult
            {
                ContainsPii = true,
                Entities = new List<PiiEntity>
                {
                    new PiiEntity
                    {
                        Type = PiiType.SSN,
                        Text = "123-45-6789",
                        StartIndex = 10,
                        EndIndex = 21
                    }
                }
            };

            // Act - using default options (null)
            var result = await _service.RedactPiiAsync(text, detectionResult, null);

            // Assert
            Assert.NotEqual(text, result);
            Assert.Contains("****", result); // Default mask
        }

        [Fact]
        public async Task DetectPiiAsync_CaseInsensitive_DetectsCorrectly()
        {
            // Arrange
            var text = "EMAIL ME AT JOHN@TEST.COM PLEASE";

            // Act
            var result = await _service.DetectPiiAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsPii);
            Assert.Single(result.Entities);
            
            var entity = result.Entities.First();
            Assert.Equal(PiiType.Email, entity.Type);
            Assert.Equal("JOHN@TEST.COM", entity.Text);
        }
    }
}