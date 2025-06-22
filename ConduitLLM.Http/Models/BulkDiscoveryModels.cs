using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Request model for bulk capability testing.
    /// </summary>
    public class BulkCapabilityTestRequest
    {
        /// <summary>
        /// List of capability tests to perform.
        /// </summary>
        [Required]
        public List<CapabilityTest> Tests { get; set; } = new();
    }

    /// <summary>
    /// Individual capability test within a bulk request.
    /// </summary>
    public class CapabilityTest
    {
        /// <summary>
        /// The model to test.
        /// </summary>
        [Required]
        public string Model { get; set; } = "";

        /// <summary>
        /// The capability to test.
        /// </summary>
        [Required]
        public string Capability { get; set; } = "";
    }

    /// <summary>
    /// Response model for bulk capability testing.
    /// </summary>
    public class BulkCapabilityTestResponse
    {
        /// <summary>
        /// Results of all capability tests.
        /// </summary>
        public List<CapabilityTestResult> Results { get; set; } = new();

        /// <summary>
        /// Total number of tests requested.
        /// </summary>
        public int TotalTests { get; set; }

        /// <summary>
        /// Number of successful tests.
        /// </summary>
        public int SuccessfulTests { get; set; }

        /// <summary>
        /// Number of failed tests.
        /// </summary>
        public int FailedTests { get; set; }
    }

    /// <summary>
    /// Result of a single capability test.
    /// </summary>
    public class CapabilityTestResult
    {
        /// <summary>
        /// The model that was tested.
        /// </summary>
        public string Model { get; set; } = "";

        /// <summary>
        /// The capability that was tested.
        /// </summary>
        public string Capability { get; set; } = "";

        /// <summary>
        /// Whether the model supports the capability.
        /// </summary>
        public bool Supported { get; set; }

        /// <summary>
        /// Error message if the test failed.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Request model for bulk model discovery.
    /// </summary>
    public class BulkModelDiscoveryRequest
    {
        /// <summary>
        /// List of model IDs to get discovery information for.
        /// </summary>
        [Required]
        public List<string> Models { get; set; } = new();
    }

    /// <summary>
    /// Response model for bulk model discovery.
    /// </summary>
    public class BulkModelDiscoveryResponse
    {
        /// <summary>
        /// Discovery results for all requested models.
        /// </summary>
        public List<ModelDiscoveryResult> Results { get; set; } = new();

        /// <summary>
        /// Total number of models requested.
        /// </summary>
        public int TotalRequested { get; set; }

        /// <summary>
        /// Number of models found.
        /// </summary>
        public int FoundModels { get; set; }

        /// <summary>
        /// Number of models not found.
        /// </summary>
        public int NotFoundModels { get; set; }
    }

    /// <summary>
    /// Discovery result for a single model.
    /// </summary>
    public class ModelDiscoveryResult
    {
        /// <summary>
        /// The model ID.
        /// </summary>
        public string Model { get; set; } = "";

        /// <summary>
        /// The provider name.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// The display name of the model.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Model capabilities as a dictionary.
        /// </summary>
        public Dictionary<string, bool> Capabilities { get; set; } = new();

        /// <summary>
        /// Whether the model was found.
        /// </summary>
        public bool Found { get; set; }

        /// <summary>
        /// Error message if the model was not found or discovery failed.
        /// </summary>
        public string? Error { get; set; }
    }
}