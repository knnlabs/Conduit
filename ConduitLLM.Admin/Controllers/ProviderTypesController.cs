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
                ProviderType.Google => "Google",
                ProviderType.Perplexity => "Perplexity",
                ProviderType.OpenRouter => "OpenRouter",
                ProviderType.MiniMax => "MiniMax",
                ProviderType.Groq => "Groq",
                ProviderType.Together => "Together AI",
                ProviderType.Deepseek => "DeepSeek",
                ProviderType.XAI => "xAI",
                ProviderType.HuggingFace => "Hugging Face",
                ProviderType.Ollama => "Ollama",
                ProviderType.Bedrock => "AWS Bedrock",
                ProviderType.VertexAI => "Google Vertex AI",
                ProviderType.Fireworks => "Fireworks AI",
                ProviderType.Cohere => "Cohere",
                ProviderType.Mistral => "Mistral AI",
                ProviderType.Databricks => "Databricks",
                ProviderType.Replicate => "Replicate",
                ProviderType.ElevenLabs => "ElevenLabs",
                ProviderType.PlayHT => "Play.ht",
                ProviderType.DeepGram => "Deepgram",
                ProviderType.GoogleTTS => "Google Text-to-Speech",
                ProviderType.AzureTTS => "Azure Text-to-Speech",
                ProviderType.AmazonPolly => "Amazon Polly",
                ProviderType.WellSaidLabs => "WellSaid Labs",
                ProviderType.Murf => "Murf.ai",
                ProviderType.Resemble => "Resemble AI",
                ProviderType.Descript => "Descript",
                ProviderType.Speechify => "Speechify",
                ProviderType.NaturalReader => "NaturalReader",
                ProviderType.Coqui => "Coqui",
                ProviderType.Lovo => "LOVO",
                ProviderType.Listnr => "Listnr",
                ProviderType.Speechelo => "Speechelo",
                ProviderType.Synthesys => "Synthesys",
                ProviderType.Replica => "Replica Studios",
                ProviderType.FakeYou => "FakeYou",
                ProviderType.TTSReader => "TTSReader",
                ProviderType.ReadSpeaker => "ReadSpeaker",
                ProviderType.CereProc => "CereProc",
                ProviderType.Nuance => "Nuance",
                ProviderType.Acapela => "Acapela",
                ProviderType.iSpeech => "iSpeech",
                ProviderType.Neospeech => "NeoSpeech",
                ProviderType.Runway => "Runway",
                ProviderType.Pika => "Pika Labs",
                ProviderType.StabilityAI => "Stability AI",
                ProviderType.Synthesia => "Synthesia",
                ProviderType.HeyGen => "HeyGen",
                ProviderType.Pictory => "Pictory",
                ProviderType.Elai => "Elai",
                ProviderType.Colossyan => "Colossyan",
                ProviderType.D_ID => "D-ID",
                ProviderType.Lumen5 => "Lumen5",
                ProviderType.Descript_Video => "Descript (Video)",
                ProviderType.Steve => "Steve AI",
                ProviderType.InVideo => "InVideo",
                ProviderType.Veed => "VEED",
                ProviderType.Kapwing => "Kapwing",
                ProviderType.Animoto => "Animoto",
                ProviderType.Biteable => "Biteable",
                ProviderType.Powtoon => "Powtoon",
                ProviderType.Vyond => "Vyond",
                ProviderType.RawShorts => "RawShorts",
                ProviderType.Wideo => "Wideo",
                ProviderType.Renderforest => "Renderforest",
                ProviderType.FlexClip => "FlexClip",
                ProviderType.Clipchamp => "Clipchamp",
                ProviderType.Muvizu => "Muvizu",
                ProviderType.Plotagon => "Plotagon",
                ProviderType.Toonly => "Toonly",
                ProviderType.CreateStudio => "CreateStudio",
                ProviderType.Doodly => "Doodly",
                ProviderType.Videoscribe => "VideoScribe",
                ProviderType.Explaindio => "Explaindio",
                ProviderType.Animaker => "Animaker",
                ProviderType.Moovly => "Moovly",
                ProviderType.Renderfire => "Renderfire Video",
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