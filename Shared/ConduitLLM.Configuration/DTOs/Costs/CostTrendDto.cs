namespace ConduitLLM.Configuration.DTOs.Costs
{
    /// <summary>
    /// Data transfer object for cost trend data
    /// </summary>
    public class CostTrendDto
    {
        /// <summary>
        /// Period type (daily, weekly, monthly)
        /// </summary>
        public string Period { get; set; } = "daily";

        /// <summary>
        /// Start date of the entire period
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of the entire period
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Cost trend data points
        /// </summary>
        public List<CostTrendDataDto> Data { get; set; } = new List<CostTrendDataDto>();
    }
}
