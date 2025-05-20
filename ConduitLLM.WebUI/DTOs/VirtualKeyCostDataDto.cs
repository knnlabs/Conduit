using System;
using Conf = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Extended wrapper for ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto that adds backward compatibility properties.
    /// This class should be used in the WebUI project to ensure compatibility with older code.
    /// </summary>
    public class VirtualKeyCostDataDto : Conf.VirtualKeyCostDataDto
    {
        /// <summary>
        /// Number of input tokens
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of output tokens
        /// </summary>
        public int OutputTokens { get; set; }
    }
}
