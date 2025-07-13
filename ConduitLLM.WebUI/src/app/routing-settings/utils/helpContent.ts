/**
 * Help content for routing settings UI components
 * Provides tooltips, descriptions, and inline help text
 */

export const HELP_TEXT = {
  // Rule Configuration
  rulePriority: "Lower numbers have higher priority. Rules are evaluated in order from lowest to highest priority number.",
  ruleDescription: "Optional description explaining the rule's purpose and business logic. Helpful for team collaboration.",
  ruleEnabled: "Disabled rules are skipped during evaluation. Use this to temporarily disable rules without deleting them.",
  
  // Conditions
  conditions: "All conditions must match for the rule to apply. Use AND logic between multiple conditions.",
  conditionType: "Select the category of data to evaluate (model, region, cost, metadata, headers, or time).",
  conditionField: "Specific field within the selected type. Available fields depend on the condition type.",
  conditionOperator: "How to compare the actual value with the expected value (equals, contains, greater than, etc.).",
  conditionValue: "The expected value to match against. Use appropriate format for the selected operator.",
  
  // Actions
  actions: "Define what happens when the rule matches. You can specify multiple actions for a single rule.",
  routeToProvider: "Send the request to a specific provider. The request will bypass normal provider priority order.",
  setCostThreshold: "Apply a maximum cost limit per token in USD. Requests exceeding this limit may be rejected or routed to cheaper providers.",
  setFallbackChain: "Define an ordered list of providers to try if the primary selection fails. Providers are tried in the specified order.",
  addMetadata: "Attach additional key-value data to the request. Useful for tracking, analytics, or downstream processing.",
  setTimeout: "Override the default request timeout in milliseconds. Higher values allow for slower providers but may impact user experience.",
  enableCaching: "Cache the response for the specified duration in seconds. Reduces costs and improves performance for repeated requests.",
  rejectRequest: "Block the request with a specified reason. Use for access control or compliance requirements.",
  
  // Provider Priority
  providerPriority: "Determines the default order providers are tried when no routing rules apply. Lower numbers = higher priority.",
  providerType: "Category of provider: Primary (main production), Backup (fallback), or Special (specific use cases).",
  providerEnabled: "Disabled providers are excluded from all routing decisions. Use during maintenance or provider issues.",
  dragToReorder: "Drag providers up or down to change their priority order. Changes are saved automatically.",
  bulkActions: "Select multiple providers to enable/disable or adjust priorities in bulk.",
  
  // Testing
  testModel: "Select the model to test routing rules against. This affects model-based conditions in your rules.",
  testRegion: "Choose the user or provider region for testing. Used by geographic routing rules.",
  testCostThreshold: "Set a cost threshold for testing cost-based routing rules. Format: decimal value (e.g., 0.001).",
  testMetadata: "Add custom key-value pairs to simulate request metadata. Used by metadata-based routing conditions.",
  testHeaders: "Simulate HTTP headers for testing header-based routing rules. Format: key=value pairs.",
  testAdvancedParams: "Expand to configure additional test parameters like custom metadata, headers, and time simulation.",
  
  // Results
  matchedRules: "Shows which rules matched the test request and whether their actions were applied.",
  ruleEvaluation: "Detailed breakdown of how each rule's conditions were evaluated against the test parameters.",
  providerSelection: "Explains why a specific provider was chosen, including the routing strategy and decision reasoning.",
  evaluationTimeline: "Step-by-step timeline showing the evaluation process, performance metrics, and execution details.",
  testHistory: "Previously run tests are automatically saved. Click any test to reload its configuration and results."
} as const;

export const TOOLTIPS = {
  // Rule Management
  addRule: "Create a new routing rule with custom conditions and actions",
  editRule: "Modify the selected rule's configuration",
  deleteRule: "Permanently remove this rule from the routing system",
  duplicateRule: "Create a copy of this rule as a starting point for a new rule",
  enableRule: "Toggle whether this rule is active in the routing evaluation",
  
  // Rule Priority
  priorityUp: "Increase rule priority (lower priority number)",
  priorityDown: "Decrease rule priority (higher priority number)",
  priorityInput: "Set exact priority value. Lower numbers = higher priority (evaluated first)",
  
  // Condition Operators
  equals: "Exact match: value must be identical",
  notEquals: "Value must not match exactly", 
  contains: "Value must contain the specified substring",
  notContains: "Value must not contain the specified substring",
  in: "Value must be in the provided list (comma-separated)",
  notIn: "Value must not be in the provided list",
  greaterThan: "Numeric value must be greater than specified",
  lessThan: "Numeric value must be less than specified",
  greaterThanOrEqual: "Numeric value must be greater than or equal to specified",
  lessThanOrEqual: "Numeric value must be less than or equal to specified",
  regex: "Value must match the regular expression pattern",
  startsWith: "Value must start with the specified prefix",
  endsWith: "Value must end with the specified suffix",
  
  // Provider Management
  enableProvider: "Include this provider in routing decisions",
  disableProvider: "Exclude this provider from routing (maintenance mode)",
  providerHealth: "Current health status based on health checks and performance metrics",
  providerLatency: "Average response time for requests to this provider",
  providerSuccessRate: "Percentage of successful requests to this provider",
  
  // Testing Controls
  runTest: "Execute routing evaluation with the configured test parameters",
  clearTest: "Reset all test parameters to default values",
  saveTest: "Save current test configuration for future use",
  exportResults: "Export test results and configuration to JSON format",
  importTest: "Load a previously exported test configuration",
  
  // Performance Indicators
  evaluationTime: "Total time taken to evaluate routing rules and select provider",
  ruleCount: "Number of routing rules evaluated for this request",
  conditionCount: "Total number of conditions checked across all matching rules",
  fallbackUsed: "Indicates whether the primary provider failed and fallback was used"
} as const;

export const FIELD_DESCRIPTIONS = {
  // Model Fields
  modelName: "The specific model name (e.g., 'gpt-4', 'claude-3-opus')",
  modelProvider: "The provider offering the model (e.g., 'openai', 'anthropic')",
  modelCapability: "Model capabilities (e.g., 'text-generation', 'code-generation', 'analysis')",
  
  // Region Fields  
  userRegion: "Geographic region of the requesting user (e.g., 'us-east-1', 'eu-west-1')",
  providerRegion: "Geographic region where the provider is located",
  
  // Cost Fields
  costPerToken: "Cost per token in USD (e.g., 0.001 for $0.001 per token)",
  monthlyBudget: "Monthly spending budget in USD",
  dailyUsage: "Daily usage amount in USD",
  
  // Time Fields
  hour: "Hour of day (0-23, where 0 = midnight)",
  dayOfWeek: "Day of week (0-6, where 0 = Sunday)",
  timezone: "Timezone for time-based evaluations (e.g., 'UTC', 'America/New_York')",
  
  // Header Fields
  apiVersion: "API version header (e.g., 'v1', 'v2')",
  authorization: "Authorization header content",
  userAgent: "User agent string from the request",
  requestId: "Unique request identifier",
  
  // Common Metadata Fields
  userId: "Unique identifier for the requesting user",
  organizationId: "Organization or tenant identifier", 
  userTier: "User subscription tier (e.g., 'free', 'premium', 'enterprise')",
  priority: "Request priority level (e.g., 'low', 'normal', 'high')",
  source: "Request source (e.g., 'web_app', 'mobile_app', 'api')"
} as const;

export const EXAMPLES = {
  // Condition Examples
  modelEquals: 'Example: "gpt-4" matches requests for GPT-4 model',
  regionStartsWith: 'Example: "us-" matches "us-east-1", "us-west-2", etc.',
  costGreaterThan: 'Example: "0.001" matches requests with cost > $0.001 per token',
  metadataIn: 'Example: "premium,enterprise" matches premium or enterprise users',
  timeRange: 'Example: "18,19,20,21" matches 6 PM to 9 PM',
  
  // Action Examples
  routeExample: 'Example: "openai-premium" routes to the OpenAI premium provider',
  costExample: 'Example: "0.002" sets maximum cost to $0.002 per token',
  fallbackExample: 'Example: "provider-1,provider-2,provider-3" creates fallback chain',
  metadataExample: 'Example: priority="high" adds priority metadata to request',
  timeoutExample: 'Example: "30000" sets 30 second timeout',
  cacheExample: 'Example: "3600" caches response for 1 hour',
  
  // Common Patterns
  geographicRouting: 'Route EU users to EU providers: region.user_region starts_with "eu-"',
  costOptimization: 'Apply cost limits: cost.cost_per_token greater_than 0.001',
  timeBasedRouting: 'Off-hours routing: time.hour in "22,23,0,1,2,3,4,5"',
  priorityUsers: 'Premium user routing: metadata.user_tier equals "premium"',
  modelSpecific: 'Expensive model routing: model.name in "gpt-4,claude-3-opus"'
} as const;

// Helper function to get help text for a specific component
export function getHelpText(key: keyof typeof HELP_TEXT): string {
  return HELP_TEXT[key];
}

// Helper function to get tooltip text
export function getTooltip(key: keyof typeof TOOLTIPS): string {
  return TOOLTIPS[key];
}

// Helper function to get field description
export function getFieldDescription(key: keyof typeof FIELD_DESCRIPTIONS): string {
  return FIELD_DESCRIPTIONS[key];
}

// Helper function to get example text
export function getExample(key: keyof typeof EXAMPLES): string {
  return EXAMPLES[key];
}