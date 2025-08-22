using System.Text.Json;

using ConduitLLM.Admin.Models.ModelCapabilities;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Tests for CapabilitiesDto serialization and deserialization behavior.
    /// These tests ensure API contract stability and catch breaking changes.
    /// </summary>
    public partial class CapabilitiesDtoTests
    {
        [Fact]
        public void ModelCapabilitiesDto_Should_Serialize_All_Boolean_Flags()
        {
            // Arrange
            var dto = new ModelCapabilitiesDto
            {
                Id = 1,
                SupportsChat = true,
                SupportsVision = false,
                SupportsFunctionCalling = true,
                SupportsStreaming = false,
                SupportsAudioTranscription = true,
                SupportsTextToSpeech = false,
                SupportsRealtimeAudio = true,
                SupportsImageGeneration = false,
                SupportsVideoGeneration = true,
                SupportsEmbeddings = false,
                MaxTokens = 128000,
                MinTokens = 1,
                TokenizerType = TokenizerType.Cl100KBase,
                SupportedVoices = "alloy,echo",
                SupportedLanguages = "en,es,fr",
                SupportedFormats = "text,json"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelCapabilitiesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.SupportsChat.Should().BeTrue();
            deserialized.SupportsVision.Should().BeFalse();
            deserialized.SupportsFunctionCalling.Should().BeTrue();
            deserialized.SupportsStreaming.Should().BeFalse();
            deserialized.SupportsAudioTranscription.Should().BeTrue();
            deserialized.SupportsTextToSpeech.Should().BeFalse();
            deserialized.SupportsRealtimeAudio.Should().BeTrue();
            deserialized.SupportsImageGeneration.Should().BeFalse();
            deserialized.SupportsVideoGeneration.Should().BeTrue();
            deserialized.SupportsEmbeddings.Should().BeFalse();
        }

        [Fact]
        public void ModelCapabilitiesDto_Should_Serialize_Token_Limits()
        {
            // Arrange
            var testCases = new[]
            {
                (min: 1, max: 4096),
                (min: 0, max: 128000),
                (min: 100, max: 200000),
                (min: 1, max: int.MaxValue),
                (min: 0, max: 0) // Edge case
            };

            foreach (var (min, max) in testCases)
            {
                // Arrange
                var dto = new ModelCapabilitiesDto
                {
                    Id = 1,
                    MinTokens = min,
                    MaxTokens = max,
                    TokenizerType = TokenizerType.BPE
                };

                // Act
                var json = JsonSerializer.Serialize(dto);
                var deserialized = JsonSerializer.Deserialize<ModelCapabilitiesDto>(json);

                // Assert
                deserialized.Should().NotBeNull();
                deserialized!.MinTokens.Should().Be(min);
                deserialized!.MaxTokens.Should().Be(max);
            }
        }

        [Fact]
        public void CapabilitiesDto_Should_Handle_Null_String_Properties()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportsChat = true,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.BPE,
                SupportedVoices = null,
                SupportedLanguages = null,
                SupportedFormats = null
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<CapabilitiesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.SupportedVoices.Should().BeNull();
            deserialized.SupportedLanguages.Should().BeNull();
            deserialized.SupportedFormats.Should().BeNull();
        }

        [Fact]
        public void CapabilitiesDto_Should_Serialize_Comma_Separated_Values()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportsTextToSpeech = true,
                SupportedVoices = "alloy,echo,fable,onyx,nova,shimmer",
                SupportedLanguages = "en,es,fr,de,it,pt,ru,zh,ja,ko",
                SupportedFormats = "mp3,opus,aac,flac,wav,pcm",
                TokenizerType = TokenizerType.BPE,
                MaxTokens = 4096,
                MinTokens = 1
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<CapabilitiesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.SupportedVoices.Should().Be("alloy,echo,fable,onyx,nova,shimmer");
            deserialized.SupportedLanguages.Should().Be("en,es,fr,de,it,pt,ru,zh,ja,ko");
            deserialized.SupportedFormats.Should().Be("mp3,opus,aac,flac,wav,pcm");
        }

        [Fact]
        public void CreateCapabilitiesDto_Should_Serialize_Without_Id()
        {
            // Arrange
            var dto = new CreateCapabilitiesDto
            {
                SupportsChat = true,
                SupportsVision = true,
                MaxTokens = 128000,
                MinTokens = 1,
                TokenizerType = TokenizerType.Cl100KBase
            };

            // Act
            var json = JsonSerializer.Serialize(dto);

            // Assert
            json.Should().NotContain("\"Id\"");
            json.Should().Contain("\"SupportsChat\":true");
            json.Should().Contain("\"SupportsVision\":true");
            json.Should().Contain("\"MaxTokens\":128000");
            json.Should().Contain("\"MinTokens\":1");
        }

        [Fact]
        public void UpdateCapabilitiesDto_Should_Handle_Partial_Updates()
        {
            // Arrange
            var dto = new UpdateCapabilitiesDto
            {
                Id = 5,
                SupportsChat = true,
                SupportsVision = null, // Don't update
                MaxTokens = 200000, // Update
                MinTokens = null, // Don't update
                TokenizerType = null // Don't update
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<UpdateCapabilitiesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(5);
            deserialized.SupportsChat.Should().BeTrue();
            deserialized.SupportsVision.Should().BeNull();
            deserialized.MaxTokens.Should().Be(200000);
            deserialized.MinTokens.Should().BeNull();
            deserialized.TokenizerType.Should().BeNull();
        }

        [Theory]
        [InlineData(TokenizerType.Cl100KBase)]
        [InlineData(TokenizerType.P50KBase)]
        [InlineData(TokenizerType.P50KEdit)]
        [InlineData(TokenizerType.R50KBase)]
        [InlineData(TokenizerType.Claude)]
        [InlineData(TokenizerType.O200KBase)]
        [InlineData(TokenizerType.LLaMA)]
        [InlineData(TokenizerType.BPE)]
        public void CapabilitiesDto_Should_Serialize_All_TokenizerTypes(TokenizerType tokenizerType)
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                TokenizerType = tokenizerType,
                MaxTokens = 4096,
                MinTokens = 1
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<CapabilitiesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.TokenizerType.Should().Be(tokenizerType);
        }

        [Fact]
        public void CapabilitiesDto_Should_Handle_Empty_String_Properties()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportedVoices = "",
                SupportedLanguages = "",
                SupportedFormats = "",
                TokenizerType = TokenizerType.BPE,
                MaxTokens = 4096,
                MinTokens = 1
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<CapabilitiesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.SupportedVoices.Should().BeEmpty();
            deserialized.SupportedLanguages.Should().BeEmpty();
            deserialized.SupportedFormats.Should().BeEmpty();
        }

        [Fact]
        public void ModelCapabilitiesDto_Should_Be_Assignable_To_CapabilitiesDto()
        {
            // Arrange
            var modelCapDto = new ModelCapabilitiesDto
            {
                Id = 1,
                SupportsChat = true,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.Cl100KBase
            };

            // Act - Should be able to treat as base type
            CapabilitiesDto baseDto = modelCapDto;

            // Assert
            baseDto.Should().NotBeNull();
            baseDto.Id.Should().Be(1);
            baseDto.SupportsChat.Should().BeTrue();
            baseDto.Should().BeOfType<ModelCapabilitiesDto>();
        }

        [Fact]
        public void CapabilitiesDto_Should_Handle_Unicode_In_Supported_Languages()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportedLanguages = "中文,日本語,한국어,العربية,עברית,русский",
                TokenizerType = TokenizerType.BPE,
                MaxTokens = 4096,
                MinTokens = 1
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<CapabilitiesDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.SupportedLanguages.Should().Be("中文,日本語,한국어,العربية,עברית,русский");
        }

        [Fact]
        public void CapabilitiesDto_Should_Preserve_Property_Names_In_Json()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportsChat = true,
                SupportsVision = false,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.BPE
            };

            // Act
            var json = JsonSerializer.Serialize(dto);

            // Assert - ensure JSON property names match expectations for API compatibility
            json.Should().Contain("\"Id\":");
            json.Should().Contain("\"SupportsChat\":");
            json.Should().Contain("\"SupportsVision\":");
            json.Should().Contain("\"MaxTokens\":");
            json.Should().Contain("\"MinTokens\":");
            json.Should().Contain("\"TokenizerType\":");
        }
    }
}