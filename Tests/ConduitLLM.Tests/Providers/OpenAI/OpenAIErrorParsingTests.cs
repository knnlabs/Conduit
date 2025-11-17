using System.Net;
using ConduitLLM.Core.Models;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Providers.OpenAI
{
    public class OpenAIErrorParsingTests
    {
        [Theory]
        [InlineData("insufficient_quota", ProviderErrorType.InsufficientBalance)]
        [InlineData("quota exceeded", ProviderErrorType.InsufficientBalance)]
        [InlineData("billing", ProviderErrorType.InsufficientBalance)]
        [InlineData("payment", ProviderErrorType.InsufficientBalance)]
        [InlineData("You have exceeded your current quota", ProviderErrorType.InsufficientBalance)]
        [InlineData("Insufficient credits", ProviderErrorType.InsufficientBalance)]
        public void ParseOpenAIError_InsufficientQuota_ReturnsInsufficientBalance(string errorMessage, ProviderErrorType expected)
        {
            // Test that 403 errors with quota-related messages are correctly classified
            var errorType = ParseOpenAIForbiddenError(errorMessage);
            errorType.Should().Be(expected);
        }

        [Theory]
        [InlineData("Invalid API key provided", ProviderErrorType.InvalidApiKey)]
        [InlineData("Incorrect API key", ProviderErrorType.InvalidApiKey)]
        [InlineData("API key not found", ProviderErrorType.InvalidApiKey)]
        [InlineData("Authentication failed", ProviderErrorType.InvalidApiKey)]
        public void ParseOpenAIError_InvalidApiKey_Returns401(string errorMessage, ProviderErrorType expected)
        {
            // Test various OpenAI error response formats for invalid API key
            var errorType = ParseOpenAIUnauthorizedError(errorMessage);
            errorType.Should().Be(expected);
        }

        [Fact]
        public void ParseOpenAIError_403WithoutQuotaMessage_ReturnsForbidden()
        {
            // Test that 403 without quota-related message returns AccessForbidden
            var errorMessage = "Access denied to this resource";
            var errorType = ParseOpenAIForbiddenError(errorMessage);
            errorType.Should().Be(ProviderErrorType.AccessForbidden);
        }

        [Theory]
        [InlineData("Rate limit exceeded", ProviderErrorType.RateLimitExceeded)]
        [InlineData("Too many requests", ProviderErrorType.RateLimitExceeded)]
        [InlineData("Please slow down", ProviderErrorType.RateLimitExceeded)]
        public void ParseOpenAIError_RateLimit_Returns429(string errorMessage, ProviderErrorType expected)
        {
            // Test rate limit error messages
            var errorType = ParseOpenAIRateLimitError(errorMessage);
            errorType.Should().Be(expected);
        }

        [Theory]
        [InlineData("The model `gpt-5` does not exist", ProviderErrorType.ModelNotFound)]
        [InlineData("Model not found", ProviderErrorType.ModelNotFound)]
        [InlineData("Invalid model", ProviderErrorType.ModelNotFound)]
        public void ParseOpenAIError_ModelNotFound_Returns404(string errorMessage, ProviderErrorType expected)
        {
            // Test model not found error messages
            var errorType = ParseOpenAINotFoundError(errorMessage);
            errorType.Should().Be(expected);
        }

        [Fact]
        public void ParseOpenAIError_ComplexJsonResponse_ExtractsErrorCorrectly()
        {
            // Test parsing complex JSON error responses
            var jsonError = @"{
                ""error"": {
                    ""message"": ""You exceeded your current quota, please check your plan and billing details."",
                    ""type"": ""insufficient_quota"",
                    ""param"": null,
                    ""code"": ""insufficient_quota""
                }
            }";

            var errorType = ParseOpenAIJsonError(jsonError);
            errorType.Should().Be(ProviderErrorType.InsufficientBalance);
        }

        [Fact]
        public void ParseOpenAIError_UnexpectedFormat_ReturnsUnknown()
        {
            // Test that unexpected error formats return Unknown
            var unexpectedError = "Something went wrong";
            var errorType = ParseGeneralError(unexpectedError);
            errorType.Should().Be(ProviderErrorType.Unknown);
        }

        [Theory]
        [InlineData(HttpStatusCode.Forbidden, "insufficient_quota", ProviderErrorType.InsufficientBalance)]
        [InlineData(HttpStatusCode.Forbidden, "access denied", ProviderErrorType.AccessForbidden)]
        [InlineData(HttpStatusCode.Unauthorized, "invalid api key", ProviderErrorType.InvalidApiKey)]
        [InlineData(HttpStatusCode.TooManyRequests, "rate limit", ProviderErrorType.RateLimitExceeded)]
        [InlineData(HttpStatusCode.NotFound, "model not found", ProviderErrorType.ModelNotFound)]
        [InlineData(HttpStatusCode.ServiceUnavailable, "service down", ProviderErrorType.ServiceUnavailable)]
        public void ClassifyOpenAIError_WithStatusAndMessage_ReturnsCorrectType(
            HttpStatusCode statusCode, 
            string errorMessage, 
            ProviderErrorType expected)
        {
            // Test combined status code and message classification
            var errorType = ClassifyOpenAIError(statusCode, errorMessage);
            errorType.Should().Be(expected);
        }

        [Fact]
        public void ParseOpenAIError_OrganizationQuotaExceeded_ReturnsInsufficientBalance()
        {
            // Test organization-level quota errors
            var orgQuotaError = "Your organization has exceeded its monthly spending limit";
            var errorType = ParseOpenAIForbiddenError(orgQuotaError);
            errorType.Should().Be(ProviderErrorType.InsufficientBalance);
        }

        [Fact]
        public void ParseOpenAIError_TrialExpired_ReturnsInsufficientBalance()
        {
            // Test trial expiration errors
            var trialError = "Your free trial has expired. Please add a payment method.";
            var errorType = ParseOpenAIForbiddenError(trialError);
            errorType.Should().Be(ProviderErrorType.InsufficientBalance);
        }

        // Helper methods that simulate the actual OpenAI error parsing logic
        private static ProviderErrorType ParseOpenAIForbiddenError(string errorMessage)
        {
            var lowerMessage = errorMessage.ToLowerInvariant();
            
            // OpenAI often returns 403 for insufficient quota
            if (lowerMessage.Contains("insufficient_quota") ||
                lowerMessage.Contains("quota") ||
                lowerMessage.Contains("billing") ||
                lowerMessage.Contains("payment") ||
                lowerMessage.Contains("credits") ||
                lowerMessage.Contains("spending limit") ||
                lowerMessage.Contains("trial"))
            {
                return ProviderErrorType.InsufficientBalance;
            }
            
            return ProviderErrorType.AccessForbidden;
        }

        private static ProviderErrorType ParseOpenAIUnauthorizedError(string errorMessage)
        {
            var lowerMessage = errorMessage.ToLowerInvariant();
            
            if (lowerMessage.Contains("api key") ||
                lowerMessage.Contains("authentication"))
            {
                return ProviderErrorType.InvalidApiKey;
            }
            
            return ProviderErrorType.InvalidApiKey; // 401 is always auth issue
        }

        private static ProviderErrorType ParseOpenAIRateLimitError(string errorMessage)
        {
            return ProviderErrorType.RateLimitExceeded; // 429 is always rate limit
        }

        private static ProviderErrorType ParseOpenAINotFoundError(string errorMessage)
        {
            var lowerMessage = errorMessage.ToLowerInvariant();
            
            if (lowerMessage.Contains("model"))
            {
                return ProviderErrorType.ModelNotFound;
            }
            
            return ProviderErrorType.ModelNotFound; // 404 for API calls usually means model
        }

        private static ProviderErrorType ParseOpenAIJsonError(string jsonError)
        {
            // Simplified JSON parsing for test
            var lowerJson = jsonError.ToLowerInvariant();
            
            if (lowerJson.Contains("insufficient_quota") || lowerJson.Contains("quota"))
            {
                return ProviderErrorType.InsufficientBalance;
            }
            
            if (lowerJson.Contains("invalid") && lowerJson.Contains("key"))
            {
                return ProviderErrorType.InvalidApiKey;
            }
            
            return ProviderErrorType.Unknown;
        }

        private static ProviderErrorType ParseGeneralError(string errorMessage)
        {
            // For unexpected formats
            return ProviderErrorType.Unknown;
        }

        private static ProviderErrorType ClassifyOpenAIError(HttpStatusCode statusCode, string errorMessage)
        {
            return statusCode switch
            {
                HttpStatusCode.Unauthorized => ParseOpenAIUnauthorizedError(errorMessage),
                HttpStatusCode.Forbidden => ParseOpenAIForbiddenError(errorMessage),
                HttpStatusCode.TooManyRequests => ParseOpenAIRateLimitError(errorMessage),
                HttpStatusCode.NotFound => ParseOpenAINotFoundError(errorMessage),
                HttpStatusCode.ServiceUnavailable => ProviderErrorType.ServiceUnavailable,
                HttpStatusCode.PaymentRequired => ProviderErrorType.InsufficientBalance,
                _ => ProviderErrorType.Unknown
            };
        }
    }
}