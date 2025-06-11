namespace ConduitLLM.Configuration.Services.Dtos
{
    /// <summary>
    /// Data transfer object for request statistics by model
    /// </summary>
    public class RequestsByModelDto
    {
        /// <summary>
        /// The model name
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Total number of requests for this model
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Total cost of all requests for this model
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Total cost of all requests for this model (alias for backward compatibility)
        /// </summary>
        public decimal TotalCost
        {
            get => Cost;
            set => Cost = value;
        }

        /// <summary>
        /// Percentage of total requests that used this model
        /// </summary>
        public double Percentage { get; set; }
    }
}
