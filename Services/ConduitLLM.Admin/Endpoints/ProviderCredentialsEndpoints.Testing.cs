using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Metrics;
using ConduitLLM.Admin.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints
{
    public partial class ProviderCredentialsEndpoints
    {
        /// <summary>
        /// Tests the connection to a provider
        /// </summary>
        /// <param name="id">The ID of the provider to test</param>
        /// <returns>The test result</returns>
        public async Task<IResult> TestProviderConnection(int id)
        {
            var provider = await _providerRepository.GetByIdAsync(id);
            if (provider == null)
            {
                return AdminResults.NotFoundEntity("Provider", id);
            }

            // Check if this provider type doesn't support testing
            var nonTestableResponse = ApiKeyTestResultService.CreateErrorResponse(
                new NotSupportedException("Provider does not support API key testing"),
                provider.ProviderType
            );

            if (nonTestableResponse.Result == ApiKeyTestResult.Ignored)
            {
                return Ok(nonTestableResponse);
            }

            // Perform a simple test - list models
            using var activity = AdminRequestMetrics.StartProviderTestActivity(provider.ProviderType.ToString(), id);
            var startTime = DateTime.UtcNow;
            try
            {
                // Client construction resolves the effective base URL (substituting structured
                // settings such as a Cloudflare account ID), so it runs inside the try: a missing
                // required setting becomes an actionable test result instead of an unhandled 500
                // that the caller can only report as an unexpected error.
                var client = await _clientFactory.GetClientByProviderIdAsync(id);
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
        }

        /// <summary>
        /// Tests a provider connection without saving
        /// </summary>
        /// <returns>The test result</returns>
        public async Task<IResult> TestProviderConnectionWithCredentials(TestProviderRequest testRequest)
        {
            if (!ProviderTypeCatalog.IsConfigurable(testRequest.ProviderType))
            {
                return BadRequest("Provider type must identify a configurable provider.");
            }

            // Create a temporary provider for testing
            var testProvider = new Provider
            {
                Id = -1, // Temporary ID
                ProviderType = testRequest.ProviderType,
                ProviderName = "Test Provider",
                BaseUrl = testRequest.BaseUrl,
                Settings = testRequest.Settings,
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
                return Ok(nonTestableResponse);
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
            var startTime = DateTime.UtcNow;
            try
            {
                // Client construction resolves the effective base URL (substituting structured
                // settings such as a Cloudflare account ID), so it runs inside the try to classify
                // configuration errors as actionable test failures rather than 500s.
                var client = _clientFactory.CreateTestClient(testProvider, testKey);
                var models = await client.ListModelsAsync();
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var modelList = models?.Select(m => m.ToString()).ToArray();

                var response = ApiKeyTestResultService.CreateSuccessResponse(
                    responseTime,
                    modelList
                );

                return Ok(response);
            }
            catch (Exception testEx)
            {
                var response = ApiKeyTestResultService.CreateErrorResponse(
                    testEx,
                    testProvider.ProviderType
                );

                return Ok(response);
            }
        }

        /// <summary>
        /// Tests a specific key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to test</param>
        /// <returns>The test result</returns>
        public async Task<IResult> TestProviderKeyCredential(int providerId, int keyId)
        {
            var key = await _keyRepository.GetByIdAsync(keyId);
            if (key == null || key.ProviderId != providerId)
            {
                return NotFound("Key credential not found");
            }

            var provider = await _providerRepository.GetByIdAsync(providerId);
            if (provider == null)
            {
                return NotFound("Provider not found");
            }

            // Check if this provider type doesn't support testing
            var nonTestableResponse = ApiKeyTestResultService.CreateErrorResponse(
                new NotSupportedException("Provider does not support API key testing"),
                provider.ProviderType
            );

            if (nonTestableResponse.Result == ApiKeyTestResult.Ignored)
            {
                return Ok(nonTestableResponse);
            }

            // Test the connection with this specific key
            using var activity = AdminRequestMetrics.StartProviderTestActivity(provider.ProviderType.ToString(), providerId);
            var startTime = DateTime.UtcNow;
            try
            {
                // Inside the try for the same reason as above: resolving the base URL from
                // structured settings can fail with an actionable configuration error.
                var client = _clientFactory.CreateTestClient(provider, key);
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

                return Ok(response);
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

                return Ok(response);
            }
        }
    }
}
