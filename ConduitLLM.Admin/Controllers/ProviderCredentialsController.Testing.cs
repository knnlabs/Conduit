using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;
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
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderConnection(int id)
        {
            try
            {
                var provider = await _providerRepository.GetByIdAsync(id);
                if (provider == null)
                {
                    return NotFound(new ErrorResponseDto("Provider not found"));
                }

                // Get a client for this provider to test
                var client = _clientFactory.GetClientByProviderId(id);
                
                // Perform a simple test - list models
                try
                {
                    var models = await client.ListModelsAsync();
                    return Ok(new
                    {
                        Success = true,
                        ProviderId = id,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = models?.Count() ?? 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = "Connection successful"
                    });
                }
                catch (Exception testEx)
                {
                    return Ok(new
                    {
                        Success = false,
                        ProviderId = id,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = testEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Tests a provider connection without saving
        /// </summary>
        /// <returns>The test result</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderConnectionWithCredentials([FromBody] TestProviderRequest testRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
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
                
                try
                {
                    var models = await client.ListModelsAsync();
                    return Ok(new
                    {
                        Success = true,
                        ProviderType = testProvider.ProviderType.ToString(),
                        ModelCount = models?.Count() ?? 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = "Connection successful"
                    });
                }
                catch (Exception testEx)
                {
                    return Ok(new
                    {
                        Success = false,
                        ProviderType = testProvider.ProviderType.ToString(),
                        ModelCount = 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = testEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider {ProviderType}", testRequest?.ProviderType.ToString() ?? "unknown");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Tests a specific key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to test</param>
        /// <returns>The test result</returns>
        [HttpPost("{providerId}/keys/{keyId}/test")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestProviderKeyCredential(int providerId, int keyId)
        {
            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null || key.ProviderId != providerId)
                {
                    return NotFound(new ErrorResponseDto("Key credential not found"));
                }

                var provider = await _providerRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    return NotFound(new ErrorResponseDto("Provider not found"));
                }

                // Test the connection with this specific key
                var client = _clientFactory.CreateTestClient(provider, key);
                
                try
                {
                    var models = await client.ListModelsAsync();
                    return Ok(new
                    {
                        Success = true,
                        ProviderId = providerId,
                        KeyId = keyId,
                        KeyName = key.KeyName,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = models?.Count() ?? 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = "Connection successful"
                    });
                }
                catch (Exception testEx)
                {
                    return Ok(new
                    {
                        Success = false,
                        ProviderId = providerId,
                        KeyId = keyId,
                        KeyName = key.KeyName,
                        ProviderType = provider.ProviderType.ToString(),
                        ProviderName = provider.ProviderName,
                        ModelCount = 0,
                        ResponseTime = DateTime.UtcNow,
                        Message = testEx.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing key credential {KeyId} for provider {ProviderId}", keyId, providerId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
