using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for listing provider types and their numeric values
    /// </summary>
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize]
    public class ProviderTypesController : ControllerBase
    {
        /// <summary>
        /// Gets all available provider types with their numeric values
        /// </summary>
        /// <returns>List of provider types with names and values</returns>
        /// <response code="200">Returns the list of provider types</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ProviderTypeInfo>), 200)]
        public IActionResult GetProviderTypes()
        {
            var providerTypes = Enum.GetValues<ProviderType>()
                .Select(pt => new ProviderTypeInfo
                {
                    Name = pt.ToString(),
                    Value = (int)pt,
                    DisplayName = GetDisplayName(pt)
                })
                .OrderBy(pt => pt.Value)
                .ToList();

            return Ok(providerTypes);
        }

        /// <summary>
        /// Gets a friendly display name for a provider type
        /// </summary>
        private string GetDisplayName(ProviderType providerType)
        {
            return providerType switch
            {
                ProviderType.OpenAI => "OpenAI",
                ProviderType.Anthropic => "Anthropic",
                ProviderType.AzureOpenAI => "Azure OpenAI",
                ProviderType.Gemini => "Google Gemini",
                ProviderType.VertexAI => "Google Vertex AI",
                ProviderType.Cohere => "Cohere",
                ProviderType.Mistral => "Mistral AI",
                ProviderType.Groq => "Groq",
                ProviderType.Ollama => "Ollama",
                ProviderType.Replicate => "Replicate",
                ProviderType.Fireworks => "Fireworks AI",
                ProviderType.Bedrock => "AWS Bedrock",
                ProviderType.HuggingFace => "Hugging Face",
                ProviderType.SageMaker => "AWS SageMaker",
                ProviderType.OpenRouter => "OpenRouter",
                ProviderType.OpenAICompatible => "OpenAI Compatible",
                ProviderType.MiniMax => "MiniMax",
                ProviderType.Ultravox => "Ultravox",
                ProviderType.ElevenLabs => "ElevenLabs",
                ProviderType.GoogleCloud => "Google Cloud",
                ProviderType.Cerebras => "Cerebras",
                _ => providerType.ToString()
            };
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
    }
}