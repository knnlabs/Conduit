using System;
using Conf = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Wrapper for ConduitLLM.Configuration.DTOs.DetailedCostDataDto that adds backward compatibility properties.
    /// This class should be used in the WebUI project to ensure compatibility with older code.
    /// </summary>
    public class DetailedCostDataDto : Conf.DetailedCostDataDto
    {
        /// <summary>
        /// Number of requests (legacy property name)
        /// </summary>
        public int RequestCount 
        { 
            get => base.Requests; 
            set => base.Requests = value;
        }
    }
}