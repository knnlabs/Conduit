using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Moq;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Extension methods for setting up ITokenCounter mocks.
    /// </summary>
    public static class TokenCounterMockExtensions
    {
        /// <summary>
        /// Sets up token counting for text strings.
        /// </summary>
        public static void SetupTokenCount(this Mock<ITokenCounter> mock, string modelName, string text, int tokenCount)
        {
            mock.Setup(x => x.EstimateTokenCountAsync(modelName, text))
                .ReturnsAsync(tokenCount);
        }

        /// <summary>
        /// Sets up token counting for any text with a specific model.
        /// </summary>
        public static void SetupTokenCountForAnyText(this Mock<ITokenCounter> mock, string modelName, int tokenCount)
        {
            mock.Setup(x => x.EstimateTokenCountAsync(modelName, It.IsAny<string>()))
                .ReturnsAsync(tokenCount);
        }

        /// <summary>
        /// Sets up token counting for messages.
        /// </summary>
        public static void SetupMessageTokenCount(this Mock<ITokenCounter> mock, string modelName, List<Message> messages, int tokenCount)
        {
            mock.Setup(x => x.EstimateTokenCountAsync(modelName, messages))
                .ReturnsAsync(tokenCount);
        }

        /// <summary>
        /// Sets up token counting for any messages with a specific model.
        /// </summary>
        public static void SetupMessageTokenCountForAny(this Mock<ITokenCounter> mock, string modelName, int tokenCount)
        {
            mock.Setup(x => x.EstimateTokenCountAsync(modelName, It.IsAny<List<Message>>()))
                .ReturnsAsync(tokenCount);
        }

        /// <summary>
        /// Sets up default token counting for any model and input.
        /// </summary>
        public static void SetupDefaultTokenCounting(this Mock<ITokenCounter> mock, int defaultTextTokens = 10, int defaultMessageTokens = 20)
        {
            mock.Setup(x => x.EstimateTokenCountAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(defaultTextTokens);

            mock.Setup(x => x.EstimateTokenCountAsync(It.IsAny<string>(), It.IsAny<List<Message>>()))
                .ReturnsAsync(defaultMessageTokens);
        }
    }
}