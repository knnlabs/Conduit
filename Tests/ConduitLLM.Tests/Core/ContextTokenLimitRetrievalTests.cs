using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core
{
    /// <summary>
    /// Regression test to ensure that Conduit correctly retrieves MaxInputTokens
    /// from Model and ModelProviderTypeAssociation entities instead of looking
    /// for a non-existent MaxContextTokens property.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Core")]
    public class ContextTokenLimitRetrievalTests
    {
        [Fact]
        public async Task Conduit_Should_Use_MaxInputTokens_For_Context_Management()
        {
            // Arrange
            const int expectedMaxInputTokens = 128000;
            const string modelAlias = "test-model";
            
            var clientFactoryMock = new Mock<ILLMClientFactory>();
            var mappingServiceMock = new Mock<IModelProviderMappingService>();
            var contextManagerMock = new Mock<IContextManager>();
            var contextOptionsMock = new Mock<IOptions<ContextManagementOptions>>();
            var loggerMock = new Mock<ILogger<Conduit>>();
            var clientMock = new Mock<ILLMClient>();

            // Setup context options (no hardcoded defaults)
            var contextOptions = new ContextManagementOptions
            {
                EnableAutomaticContextManagement = true
            };
            contextOptionsMock.Setup(x => x.Value).Returns(contextOptions);

            // Create a model with MaxInputTokens
            var model = new Model
            {
                Id = 1,
                Name = "Test Model",
                MaxInputTokens = expectedMaxInputTokens,  // This should be used
                MaxOutputTokens = 4096
            };

            // Create association without override (so it falls back to model's MaxInputTokens)
            var association = new ModelProviderTypeAssociation
            {
                Id = 1,
                ModelId = 1,
                Model = model,
                Identifier = "test-model-id",
                MaxInputTokens = null,  // No override, should use model's value
                MaxOutputTokens = null
            };

            // Create mapping
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = modelAlias,
                ProviderModelId = "test-model-id",
                ProviderId = 1,
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = association
            };

            // Setup mocks
            mappingServiceMock.Setup(x => x.GetMappingByModelAliasAsync(modelAlias))
                .ReturnsAsync(mapping);

            clientFactoryMock.Setup(x => x.GetClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(clientMock.Object);

            var request = new ChatCompletionRequest
            {
                Model = modelAlias,
                Messages = new List<Message> { new Message { Role = MessageRole.User, Content = "test" } }
            };

            // Setup context manager to capture the token limit passed to it
            int? capturedTokenLimit = null;
            contextManagerMock.Setup(x => x.ManageContextAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<int?>()))
                .Callback<ChatCompletionRequest, int?>((req, limit) => capturedTokenLimit = limit)
                .ReturnsAsync(request);

            clientMock.Setup(x => x.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatCompletionResponse
                {
                    Id = "test",
                    Object = "chat.completion",
                    Created = 0,
                    Model = modelAlias,
                    Choices = new List<Choice> { new Choice { Index = 0, FinishReason = "stop", Message = new Message { Role = MessageRole.Assistant, Content = "response" } } }
                });

            // Create Conduit instance
            var conduit = new Conduit(
                clientFactoryMock.Object,
                loggerMock.Object,
                contextManagerMock.Object,
                mappingServiceMock.Object,
                contextOptionsMock.Object
            );

            // Act
            await conduit.CreateChatCompletionAsync(request);

            // Assert - verify that the correct MaxInputTokens value was used
            Assert.NotNull(capturedTokenLimit);
            Assert.Equal(expectedMaxInputTokens, capturedTokenLimit.Value);
            
            // Verify context manager was called with the model's MaxInputTokens
            contextManagerMock.Verify(
                x => x.ManageContextAsync(It.IsAny<ChatCompletionRequest>(), expectedMaxInputTokens),
                Times.Once,
                "Context manager should be called with the model's MaxInputTokens value"
            );
        }

        [Fact]
        public async Task Conduit_Should_Use_Provider_Override_When_Available()
        {
            // Arrange
            const int modelMaxInputTokens = 100000;
            const int providerOverrideTokens = 128000;  // Provider has higher limit
            const string modelAlias = "test-model";
            
            var clientFactoryMock = new Mock<ILLMClientFactory>();
            var mappingServiceMock = new Mock<IModelProviderMappingService>();
            var contextManagerMock = new Mock<IContextManager>();
            var contextOptionsMock = new Mock<IOptions<ContextManagementOptions>>();
            var loggerMock = new Mock<ILogger<Conduit>>();
            var clientMock = new Mock<ILLMClient>();

            // Setup context options (no hardcoded defaults)
            var contextOptions = new ContextManagementOptions
            {
                EnableAutomaticContextManagement = true
            };
            contextOptionsMock.Setup(x => x.Value).Returns(contextOptions);

            // Create a model with MaxInputTokens
            var model = new Model
            {
                Id = 1,
                Name = "Test Model",
                MaxInputTokens = modelMaxInputTokens,
                MaxOutputTokens = 4096
            };

            // Create association WITH provider-specific override
            var association = new ModelProviderTypeAssociation
            {
                Id = 1,
                ModelId = 1,
                Model = model,
                Identifier = "test-model-id",
                MaxInputTokens = providerOverrideTokens,  // Provider override should be used
                MaxOutputTokens = 4096
            };

            // Create mapping
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = modelAlias,
                ProviderModelId = "test-model-id",
                ProviderId = 1,
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = association
            };

            // Setup mocks
            mappingServiceMock.Setup(x => x.GetMappingByModelAliasAsync(modelAlias))
                .ReturnsAsync(mapping);

            clientFactoryMock.Setup(x => x.GetClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(clientMock.Object);

            var request = new ChatCompletionRequest
            {
                Model = modelAlias,
                Messages = new List<Message> { new Message { Role = MessageRole.User, Content = "test" } }
            };

            // Setup context manager to capture the token limit passed to it
            int? capturedTokenLimit = null;
            contextManagerMock.Setup(x => x.ManageContextAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<int?>()))
                .Callback<ChatCompletionRequest, int?>((req, limit) => capturedTokenLimit = limit)
                .ReturnsAsync(request);

            clientMock.Setup(x => x.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatCompletionResponse
                {
                    Id = "test",
                    Object = "chat.completion",
                    Created = 0,
                    Model = modelAlias,
                    Choices = new List<Choice> { new Choice { Index = 0, FinishReason = "stop", Message = new Message { Role = MessageRole.Assistant, Content = "response" } } }
                });

            // Create Conduit instance
            var conduit = new Conduit(
                clientFactoryMock.Object,
                loggerMock.Object,
                contextManagerMock.Object,
                mappingServiceMock.Object,
                contextOptionsMock.Object
            );

            // Act
            await conduit.CreateChatCompletionAsync(request);

            // Assert - verify that the provider override was used, not the model's base value
            Assert.NotNull(capturedTokenLimit);
            Assert.Equal(providerOverrideTokens, capturedTokenLimit.Value);
            
            // Verify context manager was called with the provider's override value
            contextManagerMock.Verify(
                x => x.ManageContextAsync(It.IsAny<ChatCompletionRequest>(), providerOverrideTokens),
                Times.Once,
                "Context manager should be called with the provider's override MaxInputTokens value"
            );
        }

        [Fact]
        public async Task Conduit_Should_Not_Apply_Context_Management_When_No_Limits_Available()
        {
            // Arrange
            const string modelAlias = "test-model";
            
            var clientFactoryMock = new Mock<ILLMClientFactory>();
            var mappingServiceMock = new Mock<IModelProviderMappingService>();
            var contextManagerMock = new Mock<IContextManager>();
            var contextOptionsMock = new Mock<IOptions<ContextManagementOptions>>();
            var loggerMock = new Mock<ILogger<Conduit>>();
            var clientMock = new Mock<ILLMClient>();

            // Setup context options (no more default fallback)
            var contextOptions = new ContextManagementOptions
            {
                EnableAutomaticContextManagement = true
            };
            contextOptionsMock.Setup(x => x.Value).Returns(contextOptions);

            // Create a model WITHOUT MaxInputTokens
            var model = new Model
            {
                Id = 1,
                Name = "Test Model",
                MaxInputTokens = null,  // No model limit
                MaxOutputTokens = null
            };

            // Create association without override
            var association = new ModelProviderTypeAssociation
            {
                Id = 1,
                ModelId = 1,
                Model = model,
                Identifier = "test-model-id",
                MaxInputTokens = null,  // No provider override
                MaxOutputTokens = null
            };

            // Create mapping
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = modelAlias,
                ProviderModelId = "test-model-id",
                ProviderId = 1,
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = association
            };

            // Setup mocks
            mappingServiceMock.Setup(x => x.GetMappingByModelAliasAsync(modelAlias))
                .ReturnsAsync(mapping);

            clientFactoryMock.Setup(x => x.GetClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(clientMock.Object);

            var request = new ChatCompletionRequest
            {
                Model = modelAlias,
                Messages = new List<Message> { new Message { Role = MessageRole.User, Content = "test" } }
            };

            clientMock.Setup(x => x.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatCompletionResponse
                {
                    Id = "test",
                    Object = "chat.completion",
                    Created = 0,
                    Model = modelAlias,
                    Choices = new List<Choice> { new Choice { Index = 0, FinishReason = "stop", Message = new Message { Role = MessageRole.Assistant, Content = "response" } } }
                });

            // Create Conduit instance
            var conduit = new Conduit(
                clientFactoryMock.Object,
                loggerMock.Object,
                contextManagerMock.Object,
                mappingServiceMock.Object,
                contextOptionsMock.Object
            );

            // Act
            await conduit.CreateChatCompletionAsync(request);

            // Assert - verify that context management was NOT applied (no hardcoded fallback)
            contextManagerMock.Verify(
                x => x.ManageContextAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<int?>()),
                Times.Never,
                "Context manager should NOT be called when no model or provider limits are available in the database"
            );
        }
    }
}