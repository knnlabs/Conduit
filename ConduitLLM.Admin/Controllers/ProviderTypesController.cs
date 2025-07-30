using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for provider type information and capabilities.
    /// Uses the centralized Provider Registry for all provider metadata.
    /// </summary>
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ProviderTypesController : ControllerBase
    {
        private readonly IProviderMetadataRegistry _providerRegistry;
        private readonly ILogger<ProviderTypesController> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderTypesController class.
        /// </summary>
        /// <param name="providerRegistry">The provider registry service</param>
        /// <param name="logger">The logger instance</param>
        public ProviderTypesController(
            IProviderMetadataRegistry providerRegistry,
            ILogger<ProviderTypesController> logger)
        {
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all available provider types with their metadata.
        /// </summary>
        /// <returns>List of provider types with comprehensive information</returns>
        /// <response code="200">Returns the list of provider types</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ProviderTypeInfo>), 200)]
        public IActionResult GetProviderTypes()
        {
            try
            {
                var providerTypes = Enum.GetValues<ProviderType>()
                    .Select(pt =>
                    {
                        // Try to get metadata from registry, fallback to basic info if not found
                        if (_providerRegistry.TryGetMetadata(pt, out var metadata) && metadata != null)
                        {
                            return new ProviderTypeInfo
                            {
                                Name = pt.ToString(),
                                Value = (int)pt,
                                DisplayName = metadata.DisplayName,
                                IsRegistered = true,
                                DefaultBaseUrl = metadata.DefaultBaseUrl
                            };
                        }
                        else
                        {
                            _logger.LogWarning("Provider {ProviderType} not found in registry", pt);
                            return new ProviderTypeInfo
                            {
                                Name = pt.ToString(),
                                Value = (int)pt,
                                DisplayName = pt.ToString(),
                                IsRegistered = false,
                                DefaultBaseUrl = null
                            };
                        }
                    })
                    .OrderBy(pt => pt.Value)
                    .ToList();

                return Ok(providerTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider types");
                return StatusCode(500, "An error occurred while retrieving provider types");
            }
        }

        /// <summary>
        /// Gets detailed capabilities for a specific provider type.
        /// </summary>
        /// <param name="providerType">The provider type to get capabilities for</param>
        /// <returns>Detailed provider capabilities</returns>
        /// <response code="200">Returns the provider capabilities</response>
        /// <response code="404">Provider type not found in registry</response>
        [HttpGet("{providerType}/capabilities")]
        [ProducesResponseType(typeof(ProviderCapabilities), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetProviderCapabilities(ProviderType providerType)
        {
            try
            {
                if (_providerRegistry.TryGetMetadata(providerType, out var metadata) && metadata != null)
                {
                    return Ok(metadata.Capabilities);
                }

                return NotFound(new { message = $"Provider '{providerType}' not found in registry" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving capabilities for provider {ProviderType}", providerType);
                return StatusCode(500, "An error occurred while retrieving provider capabilities");
            }
        }

        /// <summary>
        /// Gets authentication requirements for a specific provider type.
        /// </summary>
        /// <param name="providerType">The provider type to get auth requirements for</param>
        /// <returns>Authentication requirements</returns>
        /// <response code="200">Returns the authentication requirements</response>
        /// <response code="404">Provider type not found in registry</response>
        [HttpGet("{providerType}/auth-requirements")]
        [ProducesResponseType(typeof(AuthenticationRequirements), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetAuthRequirements(ProviderType providerType)
        {
            try
            {
                if (_providerRegistry.TryGetMetadata(providerType, out var metadata) && metadata != null)
                {
                    return Ok(metadata.AuthRequirements);
                }

                return NotFound(new { message = $"Provider '{providerType}' not found in registry" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving auth requirements for provider {ProviderType}", providerType);
                return StatusCode(500, "An error occurred while retrieving authentication requirements");
            }
        }

        /// <summary>
        /// Gets configuration hints for a specific provider type.
        /// </summary>
        /// <param name="providerType">The provider type to get configuration hints for</param>
        /// <returns>Configuration hints and tips</returns>
        /// <response code="200">Returns the configuration hints</response>
        /// <response code="404">Provider type not found in registry</response>
        [HttpGet("{providerType}/configuration-hints")]
        [ProducesResponseType(typeof(ProviderConfigurationHints), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetConfigurationHints(ProviderType providerType)
        {
            try
            {
                if (_providerRegistry.TryGetMetadata(providerType, out var metadata) && metadata != null)
                {
                    return Ok(metadata.ConfigurationHints);
                }

                return NotFound(new { message = $"Provider '{providerType}' not found in registry" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration hints for provider {ProviderType}", providerType);
                return StatusCode(500, "An error occurred while retrieving configuration hints");
            }
        }

        /// <summary>
        /// Gets providers that support specific features.
        /// </summary>
        /// <param name="feature">The feature to filter by (e.g., "streaming", "embeddings", "imageGeneration")</param>
        /// <returns>List of providers supporting the feature</returns>
        /// <response code="200">Returns the list of providers</response>
        /// <response code="400">Invalid feature name</response>
        [HttpGet("by-feature/{feature}")]
        [ProducesResponseType(typeof(IEnumerable<ProviderTypeInfo>), 200)]
        [ProducesResponseType(400)]
        public IActionResult GetProvidersByFeature(string feature)
        {
            try
            {
                Func<FeatureSupport, bool> predicate = feature.ToLowerInvariant() switch
                {
                    "streaming" => f => f.Streaming,
                    "embeddings" => f => f.Embeddings,
                    "imagegeneration" => f => f.ImageGeneration,
                    "visioninput" => f => f.VisionInput,
                    "functioncalling" => f => f.FunctionCalling,
                    "audiotranscription" => f => f.AudioTranscription,
                    "texttospeech" => f => f.TextToSpeech,
                    _ => null!
                };

                if (predicate == null)
                {
                    return BadRequest(new { message = $"Invalid feature: {feature}" });
                }

                var providers = _providerRegistry.GetProvidersByFeature(predicate)
                    .Select(metadata => new ProviderTypeInfo
                    {
                        Name = metadata.ProviderType.ToString(),
                        Value = (int)metadata.ProviderType,
                        DisplayName = metadata.DisplayName,
                        IsRegistered = true,
                        DefaultBaseUrl = metadata.DefaultBaseUrl
                    })
                    .ToList();

                return Ok(providers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving providers by feature {Feature}", feature);
                return StatusCode(500, "An error occurred while retrieving providers by feature");
            }
        }

        /// <summary>
        /// Gets diagnostic information about the provider registry.
        /// </summary>
        /// <returns>Registry diagnostics</returns>
        /// <response code="200">Returns the diagnostics</response>
        [HttpGet("diagnostics")]
        [ProducesResponseType(typeof(ProviderRegistryDiagnostics), 200)]
        public IActionResult GetRegistryDiagnostics()
        {
            try
            {
                var diagnostics = _providerRegistry.GetDiagnostics();
                return Ok(diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registry diagnostics");
                return StatusCode(500, "An error occurred while retrieving registry diagnostics");
            }
        }
    }

    /// <summary>
    /// Information about a provider type
    /// </summary>
    public class ProviderTypeInfo
    {
        /// <summary>
        /// The enum name (e.g., "OpenAI")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The numeric value of the enum
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// A friendly display name (e.g., "OpenAI" or "Azure OpenAI")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether this provider is registered in the provider registry
        /// </summary>
        public bool IsRegistered { get; set; }

        /// <summary>
        /// The default base URL for this provider's API
        /// </summary>
        public string? DefaultBaseUrl { get; set; }
    }
}