using System;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Builders
{
    /// <summary>
    /// Builder for creating ProviderErrorInfo test data
    /// </summary>
    public class ProviderErrorInfoBuilder
    {
        private ProviderErrorInfo _error;

        public ProviderErrorInfoBuilder()
        {
            _error = new ProviderErrorInfo
            {
                KeyCredentialId = 1,
                ProviderId = 1,
                ErrorType = ProviderErrorType.Unknown,
                ErrorMessage = "Test error",
                OccurredAt = DateTime.UtcNow,
                ProviderName = "TestProvider",
                ModelName = "test-model",
                RequestId = Guid.NewGuid().ToString(),
                RetryAttempt = 0
            };
        }

        public ProviderErrorInfoBuilder WithKeyCredentialId(int keyId)
        {
            _error.KeyCredentialId = keyId;
            return this;
        }

        public ProviderErrorInfoBuilder WithProviderId(int providerId)
        {
            _error.ProviderId = providerId;
            return this;
        }

        public ProviderErrorInfoBuilder WithFatalError(ProviderErrorType errorType = ProviderErrorType.InvalidApiKey)
        {
            // Fatal errors are 1-9
            if ((int)errorType > 9)
            {
                throw new ArgumentException($"Error type {errorType} is not fatal. Fatal errors have values 1-9.");
            }
            _error.ErrorType = errorType;
            _error.HttpStatusCode = errorType switch
            {
                ProviderErrorType.InvalidApiKey => 401,
                ProviderErrorType.InsufficientBalance => 402,
                ProviderErrorType.AccessForbidden => 403,
                _ => 500
            };
            _error.ErrorMessage = $"Fatal error: {errorType}";
            return this;
        }

        public ProviderErrorInfoBuilder WithWarning(ProviderErrorType errorType = ProviderErrorType.RateLimitExceeded)
        {
            // Warnings are 10+
            if ((int)errorType <= 9)
            {
                throw new ArgumentException($"Error type {errorType} is not a warning. Warnings have values 10+.");
            }
            _error.ErrorType = errorType;
            _error.HttpStatusCode = errorType switch
            {
                ProviderErrorType.RateLimitExceeded => 429,
                ProviderErrorType.ModelNotFound => 404,
                ProviderErrorType.ServiceUnavailable => 503,
                _ => 500
            };
            _error.ErrorMessage = $"Warning: {errorType}";
            return this;
        }

        public ProviderErrorInfoBuilder WithErrorType(ProviderErrorType errorType)
        {
            _error.ErrorType = errorType;
            return this;
        }

        public ProviderErrorInfoBuilder WithErrorMessage(string message)
        {
            _error.ErrorMessage = message;
            return this;
        }

        public ProviderErrorInfoBuilder WithHttpStatusCode(int statusCode)
        {
            _error.HttpStatusCode = statusCode;
            return this;
        }

        public ProviderErrorInfoBuilder WithProviderName(string providerName)
        {
            _error.ProviderName = providerName;
            return this;
        }

        public ProviderErrorInfoBuilder WithModelName(string modelName)
        {
            _error.ModelName = modelName;
            return this;
        }

        public ProviderErrorInfoBuilder WithOccurredAt(DateTime occurredAt)
        {
            _error.OccurredAt = occurredAt;
            return this;
        }

        public ProviderErrorInfoBuilder WithRequestId(string requestId)
        {
            _error.RequestId = requestId;
            return this;
        }

        public ProviderErrorInfoBuilder WithRetryAttempt(int retryAttempt)
        {
            _error.RetryAttempt = retryAttempt;
            return this;
        }

        public ProviderErrorInfo Build()
        {
            // Return a new instance to prevent mutation of built objects
            return new ProviderErrorInfo
            {
                KeyCredentialId = _error.KeyCredentialId,
                ProviderId = _error.ProviderId,
                ErrorType = _error.ErrorType,
                ErrorMessage = _error.ErrorMessage,
                HttpStatusCode = _error.HttpStatusCode,
                ProviderName = _error.ProviderName,
                ModelName = _error.ModelName,
                OccurredAt = _error.OccurredAt,
                RequestId = _error.RequestId,
                RetryAttempt = _error.RetryAttempt
            };
        }
    }
}