using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Admin.Metrics;
using ConduitLLM.Admin.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    public partial class ProviderCredentialsController
    {
        /// <summary>
        /// Tests the connection to a provider
        /// </summary>
        /// <param name="id">The ID of the provider to test</param>
        /// <returns>The test result</returns>
        [HttpPost("test/{id}")]
        [ProducesResponseType(typeof(StandardApiKeyTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> TestProviderConnection(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _providerRepository.GetByIdAsync(id),
                async provider =>
                {
                    // Check if this provider type doesn't support testing
                    var nonTestableResponse = ApiKeyTestResultService.CreateErrorResponse(
                        new NotSupportedException("Provider does not support API key testing"),
                        provider.ProviderType
                    );

                    if (nonTestableResponse.Result == ApiKeyTestResult.Ignored)
                    {
                        return Ok(nonTestableResponse);
                    }

                    // Get a client for this provider to test
                    var client = await _clientFactory.GetClientByProviderIdAsync(id);

                    // Perform a simple test - list models
                    using var activity = AdminRequestMetrics.StartProviderTestActivity(provider.ProviderType.ToString(), id);
                    var startTime = DateTime.UtcNow;
                    try
                    {
                        var models = await client.ListModelsAsync();
                        var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        var modelList = models?.Select(m => m.ToString()).ToArray();

                        var response = ApiKeyTestResultService.CreateSuccessResponse(
                            responseTime,
                            modelList
                        );

                        LogAdminAudit("Tested", "Provider", id,
                            $"Type: {provider.ProviderType}, Result: Success, ResponseTime: {responseTime:F0}ms, Models: {modelList?.Length ?? 0}");
                        AdminOperationsMetricsService.RecordProviderOperation("test", provider.ProviderType.ToString(), "success", responseTime / 1000.0);

                        return Ok(response);
                    }
                    catch (Exception testEx)
                    {
                        var response = ApiKeyTestResultService.CreateErrorResponse(
                            testEx,
                            provider.ProviderType
                        );

                        LogAdminAudit("Tested", "Provider", id,
                            $"Type: {provider.ProviderType}, Result: {response.Result}, Error: {response.Message}");
                        AdminOperationsMetricsService.RecordProviderOperation("test", provider.ProviderType.ToString(), "failure");

                        return Ok(response);
                    }
                },
                "Provider",
                id,
                "TestProviderConnection");
        }

        /// <summary>
        /// Tests a provider connection without saving
        /// </summary>
        /// <returns>The test result</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(StandardApiKeyTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> TestProviderConnectionWithCredentials([FromBody] TestProviderRequest testRequest)
        {
            return ExecuteAsync(
                async () =>
                {
                    // Create a temporary provider for testing
                    var testProvider = new Provider
                    {
                        Id = -1, // Temporary ID
                        ProviderType = testRequest.ProviderType,
                        ProviderName = "Test Provider",
                        BaseUrl = testRequest.BaseUrl,
                        IsEnabled = true
                    };

                    // Create a temporary key if provided
                    if (!string.IsNullOrEmpty(testRequest.ApiKey))
                    {
                        testProvider.ProviderKeyCredentials = new List<ProviderKeyCredential>
                        {
                            new ProviderKeyCredential
                            {
                                ApiKey = testRequest.ApiKey,
                                Organization = testRequest.Organization,
                                IsPrimary = true,
                                IsEnabled = true
                            }
                        };
                    }

                    // Check if this provider type doesn't support testing
                    var nonTestableResponse = ApiKeyTestResultService.CreateErrorResponse(
                        new NotSupportedException("Provider does not support API key testing"),
                        testProvider.ProviderType
                    );

                    if (nonTestableResponse.Result == ApiKeyTestResult.Ignored)
                    {
                        return (IActionResult)Ok(nonTestableResponse);
                    }

                    // Test the connection
                    var testKey = new ProviderKeyCredential
                    {
                        ApiKey = testRequest.ApiKey,
                        BaseUrl = testRequest.BaseUrl,
                        Organization = testRequest.Organization,
                        IsPrimary = true,
                        IsEnabled = true
                    };
                    var client = _clientFactory.CreateTestClient(testProvider, testKey);

                    var startTime = DateTime.UtcNow;
                    try
                    {
                        var models = await client.ListModelsAsync();
                        var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        var modelList = models?.Select(m => m.ToString()).ToArray();

                        var response = ApiKeyTestResultService.CreateSuccessResponse(
                            responseTime,
                            modelList
                        );

                        return (IActionResult)Ok(response);
                    }
                    catch (Exception testEx)
                    {
                        var response = ApiKeyTestResultService.CreateErrorResponse(
                            testEx,
                            testProvider.ProviderType
                        );

                        return (IActionResult)Ok(response);
                    }
                },
                result => result,
                "TestProviderConnectionWithCredentials",
                new { ProviderType = testRequest?.ProviderType.ToString() ?? "unknown" });
        }

        /// <summary>
        /// Tests a specific key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to test</param>
        /// <returns>The test result</returns>
        [HttpPost("{providerId}/keys/{keyId}/test")]
        [ProducesResponseType(typeof(StandardApiKeyTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> TestProviderKeyCredential(int providerId, int keyId)
        {
            return ExecuteAsync(
                async () =>
                {
                    var key = await _keyRepository.GetByIdAsync(keyId);
                    if (key == null || key.ProviderId != providerId)
                    {
                        return (IActionResult)NotFound(new ErrorResponseDto("Key credential not found"));
                    }

                    var provider = await _providerRepository.GetByIdAsync(providerId);
                    if (provider == null)
                    {
                        return (IActionResult)NotFound(new ErrorResponseDto("Provider not found"));
                    }

                    // Check if this provider type doesn't support testing
                    var nonTestableResponse = ApiKeyTestResultService.CreateErrorResponse(
                        new NotSupportedException("Provider does not support API key testing"),
                        provider.ProviderType
                    );

                    if (nonTestableResponse.Result == ApiKeyTestResult.Ignored)
                    {
                        return (IActionResult)Ok(nonTestableResponse);
                    }

                    // Test the connection with this specific key
                    var client = _clientFactory.CreateTestClient(provider, key);

                    using var activity = AdminRequestMetrics.StartProviderTestActivity(provider.ProviderType.ToString(), providerId);
                    var startTime = DateTime.UtcNow;
                    try
                    {
                        var models = await client.ListModelsAsync();
                        var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        var modelList = models?.Select(m => m.ToString()).ToArray();

                        var response = ApiKeyTestResultService.CreateSuccessResponse(
                            responseTime,
                            modelList
                        );

                        LogAdminAudit("Tested", "ProviderKeyCredential", keyId,
                            $"ProviderId: {providerId}, Result: Success, ResponseTime: {responseTime:F0}ms");
                        AdminOperationsMetricsService.RecordProviderOperation("test", provider.ProviderType.ToString(), "success", responseTime / 1000.0);

                        return (IActionResult)Ok(response);
                    }
                    catch (Exception testEx)
                    {
                        var response = ApiKeyTestResultService.CreateErrorResponse(
                            testEx,
                            provider.ProviderType
                        );

                        LogAdminAudit("Tested", "ProviderKeyCredential", keyId,
                            $"ProviderId: {providerId}, Result: {response.Result}, Error: {response.Message}");
                        AdminOperationsMetricsService.RecordProviderOperation("test", provider.ProviderType.ToString(), "failure");

                        return (IActionResult)Ok(response);
                    }
                },
                result => result,
                "TestProviderKeyCredential",
                new { ProviderId = providerId, KeyId = keyId });
        }
    }
}
