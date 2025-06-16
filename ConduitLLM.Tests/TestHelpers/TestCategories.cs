namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Standard test categories for organizing and filtering tests
    /// </summary>
    public static class TestCategories
    {
        // Test Type Categories
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Performance = "Performance";
        public const string Load = "Load";
        public const string E2E = "E2E";
        public const string Component = "Component";

        // Feature Categories
        public const string Provider = "Provider";
        public const string WebUI = "WebUI";
        public const string Admin = "Admin";
        public const string Router = "Router";
        public const string Cache = "Cache";
        public const string Security = "Security";
        public const string Configuration = "Configuration";
        public const string Database = "Database";
        
        // Provider-Specific Categories
        public const string OpenAI = "OpenAI";
        public const string Anthropic = "Anthropic";
        public const string Azure = "Azure";
        public const string Mistral = "Mistral";
        public const string Gemini = "Gemini";
        public const string Bedrock = "Bedrock";
        public const string Vertex = "Vertex";
        
        // Speed Categories
        public const string Fast = "Fast"; // < 100ms
        public const string Slow = "Slow"; // > 1s
        
        // Stability Categories
        public const string Flaky = "Flaky"; // Known to be unstable
        public const string RequiresNetwork = "RequiresNetwork";
        public const string RequiresDatabase = "RequiresDatabase";
        public const string RequiresDocker = "RequiresDocker";
    }
    
    /// <summary>
    /// Helper class for common test category combinations
    /// </summary>
    public static class TestCategoryGroups
    {
        /// <summary>
        /// Fast unit tests that can run in CI
        /// </summary>
        public static readonly string[] FastUnit = { TestCategories.Unit, TestCategories.Fast };
        
        /// <summary>
        /// Integration tests requiring external dependencies
        /// </summary>
        public static readonly string[] IntegrationWithDeps = 
        { 
            TestCategories.Integration, 
            TestCategories.RequiresNetwork, 
            TestCategories.Slow 
        };
        
        /// <summary>
        /// Database integration tests
        /// </summary>
        public static readonly string[] DatabaseIntegration = 
        { 
            TestCategories.Integration, 
            TestCategories.Database, 
            TestCategories.RequiresDatabase 
        };
        
        /// <summary>
        /// Provider-specific unit tests
        /// </summary>
        public static string[] ProviderUnit(string providerName) => 
            new[] { TestCategories.Unit, TestCategories.Provider, providerName };
    }
}