using System.Text.Json;

using ConduitLLM.Admin.Models.ModelAuthors;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.ModelAuthors
{
    /// <summary>
    /// Tests for ModelAuthorDto serialization and deserialization behavior.
    /// These tests ensure API contract stability and catch breaking changes.
    /// </summary>
    public partial class ModelAuthorDtoTests
    {
        [Fact]
        public void ModelAuthorDto_Should_Serialize_And_Deserialize_Correctly()
        {
            // Arrange
            var dto = new ModelAuthorDto
            {
                Id = 5,
                Name = "OpenAI",
                WebsiteUrl = "https://openai.com",
                Description = "Creator of GPT models and DALL-E"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(dto.Id);
            deserialized.Name.Should().Be(dto.Name);
            deserialized.WebsiteUrl.Should().Be(dto.WebsiteUrl);
            deserialized.Description.Should().Be(dto.Description);
        }

        [Fact]
        public void ModelAuthorDto_Should_Handle_Null_Properties()
        {
            // Arrange
            var dto = new ModelAuthorDto
            {
                Id = 1,
                Name = "Test Author",
                WebsiteUrl = null,
                Description = null
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().Be("Test Author");
            deserialized.WebsiteUrl.Should().BeNull();
            deserialized.Description.Should().BeNull();
        }

        [Fact]
        public void CreateModelAuthorDto_Should_Serialize_Without_Id()
        {
            // Arrange
            var dto = new CreateModelAuthorDto
            {
                Name = "Anthropic",
                WebsiteUrl = "https://anthropic.com",
                Description = "AI safety company, creator of Claude"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);

            // Assert
            json.Should().NotContain("\"Id\"");
            json.Should().Contain("\"Name\":\"Anthropic\"");
            json.Should().Contain("\"WebsiteUrl\":\"https://anthropic.com\"");
            json.Should().Contain("\"Description\"");
        }

        [Fact]
        public void UpdateModelAuthorDto_Should_Handle_Partial_Updates()
        {
            // Arrange
            var dto = new UpdateModelAuthorDto
            {
                Id = 3,
                Name = "Updated Name",
                WebsiteUrl = null, // Don't update
                Description = "New description"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<UpdateModelAuthorDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(3);
            deserialized.Name.Should().Be("Updated Name");
            deserialized.WebsiteUrl.Should().BeNull();
            deserialized.Description.Should().Be("New description");
        }

        [Fact]
        public void ModelAuthorDto_Should_Preserve_Property_Names_In_Json()
        {
            // Arrange
            var dto = new ModelAuthorDto
            {
                Id = 1,
                Name = "Test",
                WebsiteUrl = "https://example.com",
                Description = "Test description"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);

            // Assert - ensure JSON property names match expectations for API compatibility
            json.Should().Contain("\"Id\":");
            json.Should().Contain("\"Name\":");
            json.Should().Contain("\"WebsiteUrl\":");
            json.Should().Contain("\"Description\":");
        }

        [Theory]
        [InlineData("https://openai.com")]
        [InlineData("http://example.com")]
        [InlineData("https://sub.domain.example.org/path")]
        [InlineData("https://example.com:8080")]
        [InlineData("https://192.168.1.1")]
        [InlineData("ftp://files.example.com")] // Different protocol
        [InlineData("")] // Empty
        public void ModelAuthorDto_Should_Accept_Various_Url_Formats(string url)
        {
            // Arrange & Act
            var dto = new ModelAuthorDto
            {
                Id = 1,
                Name = "Test",
                WebsiteUrl = url,
                Description = "Test"
            };

            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.WebsiteUrl.Should().Be(url);
        }

        [Fact]
        public void ModelAuthorDto_Should_Handle_Unicode_In_Name_And_Description()
        {
            // Arrange
            var dto = new ModelAuthorDto
            {
                Id = 1,
                Name = "百度 Baidu 🤖",
                WebsiteUrl = "https://baidu.com",
                Description = "中国科技公司 - Chinese tech company 技術 회사"
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().Be("百度 Baidu 🤖");
            deserialized.Description.Should().Be("中国科技公司 - Chinese tech company 技術 회사");
        }

        [Fact]
        public void ModelAuthorDto_Should_Handle_Special_Characters_In_Name()
        {
            // Arrange
            var specialNames = new[]
            {
                "O'Reilly Media",
                "Barnes & Noble",
                "AT&T",
                "Company, Inc.",
                "3M",
                "21st Century Fox",
                "Author (Subsidiary)",
                "Name/Alias",
                "Company\\Division"
            };

            foreach (var name in specialNames)
            {
                // Arrange
                var dto = new ModelAuthorDto
                {
                    Id = 1,
                    Name = name,
                    WebsiteUrl = "https://example.com",
                    Description = "Test"
                };

                // Act
                var json = JsonSerializer.Serialize(dto);
                var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

                // Assert
                deserialized.Should().NotBeNull();
                deserialized!.Name.Should().Be(name);
            }
        }

        [Fact]
        public void ModelAuthorDto_Should_Handle_Very_Long_Description()
        {
            // Arrange
            var longDescription = new string('x', 10000); // 10k characters
            
            var dto = new ModelAuthorDto
            {
                Id = 1,
                Name = "Test",
                WebsiteUrl = "https://example.com",
                Description = longDescription
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Description.Should().Be(longDescription);
            deserialized.Description.Should().HaveLength(10000);
        }

        [Fact]
        public void ModelAuthorDto_Should_Handle_Empty_Strings()
        {
            // Arrange
            var dto = new ModelAuthorDto
            {
                Id = 1,
                Name = "",
                WebsiteUrl = "",
                Description = ""
            };

            // Act
            var json = JsonSerializer.Serialize(dto);
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().BeEmpty();
            deserialized.WebsiteUrl.Should().BeEmpty();
            deserialized.Description.Should().BeEmpty();
        }

        [Fact]
        public void ModelAuthorDto_Should_Be_Case_Sensitive_For_Json_Properties()
        {
            // Arrange
            var json = @"{
                ""id"": 1,
                ""name"": ""test"",
                ""websiteUrl"": ""https://example.com"",
                ""description"": ""test""
            }";

            // Act - deserialize with wrong casing (camelCase instead of PascalCase)
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json, options);

            // Assert - properties should be default values due to case mismatch
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(0); // Default value
            deserialized.Name.Should().Be(string.Empty); // Default value
            deserialized.WebsiteUrl.Should().BeNull(); // Default value
            deserialized.Description.Should().BeNull(); // Default value
        }

        [Fact]
        public void ModelAuthorDto_Should_Handle_Extra_Json_Properties_Gracefully()
        {
            // Arrange - JSON with extra properties that don't exist in DTO
            var json = @"{
                ""Id"": 1,
                ""Name"": ""OpenAI"",
                ""WebsiteUrl"": ""https://openai.com"",
                ""Description"": ""AI research"",
                ""Founded"": ""2015"",
                ""Headquarters"": ""San Francisco"",
                ""Employees"": 500
            }";

            // Act
            var deserialized = JsonSerializer.Deserialize<ModelAuthorDto>(json);

            // Assert - should deserialize known properties and ignore extras
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(1);
            deserialized.Name.Should().Be("OpenAI");
            deserialized.WebsiteUrl.Should().Be("https://openai.com");
            deserialized.Description.Should().Be("AI research");
        }
    }
}