using System.Text.Json;
using ConduitLLM.Admin.Auditing;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Controller for managing pricing rules validation, simulation, and audit
    /// </summary>
    public partial class PricingEndpoints
    {
        private readonly IPricingRulesValidator _pricingValidator;
        private readonly IPricingRulesEvaluator _pricingEvaluator;
        private readonly IPricingAuditService _pricingAuditService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<PricingEndpoints> _logger;

        // Metrics for pricing API operations
        private static readonly Counter PricingValidations = Prometheus.Metrics
            .CreateCounter("conduit_admin_pricing_validations_total", "Total pricing validations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status" }
                });

        private static readonly Counter PricingSimulations = Prometheus.Metrics
            .CreateCounter("conduit_admin_pricing_simulations_total", "Total pricing simulations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status" }
                });

        private static readonly Histogram PricingOperationDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_pricing_operation_duration_seconds", "Pricing operation duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 10) // 1ms to ~1s
                });

        private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes the Pricing endpoint handler.
        /// </summary>
        public PricingEndpoints(
            IPricingRulesValidator pricingValidator,
            IPricingRulesEvaluator pricingEvaluator,
            IPricingAuditService pricingAuditService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<PricingEndpoints> logger)
        {
            _pricingValidator = pricingValidator ?? throw new ArgumentNullException(nameof(pricingValidator));
            _pricingEvaluator = pricingEvaluator ?? throw new ArgumentNullException(nameof(pricingEvaluator));
            _pricingAuditService = pricingAuditService ?? throw new ArgumentNullException(nameof(pricingAuditService));
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public static IEndpointRouteBuilder MapPricingEndpoints(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/Pricing").RequireAuthorization("MasterKeyPolicy").AddEndpointFilter<ValidationEndpointFilter>().AddEndpointFilter<OperationLoggingEndpointFilter>().WithTags("Pricing");
            group.MapGet("/types", ([FromServices] PricingEndpoints e) => e.GetPricingTypes()).WithName("Pricing_GetTypes").Produces<IEnumerable<PricingTypeInfo>>();
            group.MapGet("/operators", ([FromServices] PricingEndpoints e) => e.GetConditionOperators()).WithName("Pricing_GetOperators").Produces<IEnumerable<OperatorInfo>>();
            group.MapGet("/template", ([FromServices] PricingEndpoints e, string? pricingType = "per_second") => e.GetPricingTemplate(pricingType)).WithName("Pricing_GetTemplate").Produces<object>();
            group.MapPost("/validate", ([FromServices] PricingEndpoints e, PricingValidationRequest request) => e.ValidatePricingConfiguration(request)).WithName("Pricing_Validate").Produces<PricingValidationResponse>().Produces(StatusCodes.Status400BadRequest);
            group.MapPost("/simulate", ([FromServices] PricingEndpoints e, PricingSimulationRequest request) => e.SimulatePricing(request)).WithName("Pricing_Simulate").Produces<PricingSimulationResponse>().Produces(StatusCodes.Status400BadRequest);
            group.MapPost("/audit/query", ([FromServices] PricingEndpoints e, PricingAuditQueryRequest request) => e.QueryPricingAuditEvents(request)).WithName("Pricing_QueryAudit").Produces<PricingAuditQueryResponse>().Produces(StatusCodes.Status400BadRequest);
            group.MapGet("/audit/summary", ([FromServices] PricingEndpoints e, DateTime? from = null, DateTime? to = null) => e.GetPricingAuditSummary(from ?? default, to ?? default)).WithName("Pricing_GetAuditSummary").Produces<PricingAuditSummary>().Produces(StatusCodes.Status400BadRequest);
            group.MapGet("/audit/request/{requestId}", ([FromServices] PricingEndpoints e, string requestId) => e.GetPricingAuditByRequestId(requestId)).WithName("Pricing_GetAuditByRequestId").Produces<IEnumerable<PricingAuditEventDto>>().Produces(StatusCodes.Status404NotFound);
            return app;
        }

        /// <summary>
        /// Attempts to deserialize a JSON string into a pricing configuration object.
        /// Returns true if successful, false if the JSON is invalid or null.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="json">The JSON string to deserialize</param>
        /// <param name="config">The deserialized configuration, or null on failure</param>
        /// <param name="errorMessage">The error message if deserialization fails, or null on success</param>
        /// <returns>True if deserialization succeeded and result is non-null</returns>
        private static bool TryDeserializePricingConfig<T>(string json, out T? config, out string? errorMessage) where T : class
        {
            config = null;
            errorMessage = null;

            try
            {
                config = JsonSerializer.Deserialize<T>(json, CaseInsensitiveJsonOptions);
            }
            catch (JsonException ex)
            {
                errorMessage = $"Invalid JSON format: {ex.Message}";
                return false;
            }

            if (config == null)
            {
                errorMessage = "Configuration could not be parsed";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get available pricing types
        /// </summary>
        /// <returns>List of pricing types with descriptions</returns>
        public IResult GetPricingTypes()
        {
            var pricingTypes = new[]
            {
                new PricingTypeInfo { Type = "per_unit", Description = "Per unit pricing (e.g., per image, per video)", Example = "1.0 * rate" },
                new PricingTypeInfo { Type = "per_second", Description = "Per second pricing (e.g., video duration)", Example = "duration_seconds * rate" },
                new PricingTypeInfo { Type = "per_step", Description = "Per inference step pricing", Example = "inference_steps * rate" },
                new PricingTypeInfo { Type = "per_token", Description = "Per token pricing (input/output)", Example = "tokens * rate / 1_000_000" }
            };

            return Results.Ok(pricingTypes);
        }

        /// <summary>
        /// Get available condition operators
        /// </summary>
        /// <returns>List of operators with descriptions</returns>
        public IResult GetConditionOperators()
        {
            var operators = new[]
            {
                new OperatorInfo { Operator = "eq", Description = "Equals", Example = "resolution eq '1080p'" },
                new OperatorInfo { Operator = "ne", Description = "Not equals", Example = "quality ne 'draft'" },
                new OperatorInfo { Operator = "gt", Description = "Greater than", Example = "duration gt 30" },
                new OperatorInfo { Operator = "gte", Description = "Greater than or equal", Example = "fps gte 30" },
                new OperatorInfo { Operator = "lt", Description = "Less than", Example = "duration lt 10" },
                new OperatorInfo { Operator = "lte", Description = "Less than or equal", Example = "count lte 5" },
                new OperatorInfo { Operator = "in", Description = "In list", Example = "resolution in ['720p','1080p']" },
                new OperatorInfo { Operator = "nin", Description = "Not in list", Example = "quality nin ['draft','preview']" },
                new OperatorInfo { Operator = "contains", Description = "Contains substring", Example = "style contains 'hd'" },
                new OperatorInfo { Operator = "startswith", Description = "Starts with", Example = "model startswith 'gpt-'" },
                new OperatorInfo { Operator = "endswith", Description = "Ends with", Example = "format endswith '.mp4'" },
                new OperatorInfo { Operator = "regex", Description = "Regex match", Example = "model regex '^gpt-4.*'" },
                new OperatorInfo { Operator = "exists", Description = "Property exists", Example = "audio exists true" }
            };

            return Results.Ok(operators);
        }

        /// <summary>
        /// Get pricing configuration template
        /// </summary>
        /// <param name="pricingType">The pricing type to get template for</param>
        /// <returns>JSON template for the pricing configuration</returns>
        public IResult GetPricingTemplate(string? pricingType = "per_second")
        {
            // Note: Conditions are Dictionary<string, object> in the actual model
            // This template shows the simplified key-value condition format
            object template = pricingType?.ToLowerInvariant() switch
            {
                "per_unit" => new
                {
                    pricingType = "per_unit",
                    defaultRate = 0.05m,
                    unitField = "ImageCount",
                    rules = new object[]
                    {
                        new
                        {
                            priority = 1,
                            description = "HD quality",
                            conditions = new Dictionary<string, object> { ["quality"] = "hd" },
                            rate = 0.08m
                        },
                        new
                        {
                            priority = 2,
                            description = "Standard quality",
                            conditions = new Dictionary<string, object> { ["quality"] = "standard" },
                            rate = 0.05m
                        }
                    }
                },
                "per_second" => new
                {
                    pricingType = "per_second",
                    defaultRate = 0.025m,
                    unitField = "VideoDurationSeconds",
                    rules = new object[]
                    {
                        new
                        {
                            priority = 1,
                            description = "1080p video with audio",
                            conditions = new Dictionary<string, object> { ["resolution"] = "1080p", ["with_audio"] = true },
                            rate = 0.15m
                        },
                        new
                        {
                            priority = 2,
                            description = "1080p video without audio",
                            conditions = new Dictionary<string, object> { ["resolution"] = "1080p" },
                            rate = 0.06m
                        },
                        new
                        {
                            priority = 3,
                            description = "720p video",
                            conditions = new Dictionary<string, object> { ["resolution"] = "720p" },
                            rate = 0.025m
                        },
                        new
                        {
                            priority = 4,
                            description = "480p video",
                            conditions = new Dictionary<string, object> { ["resolution"] = "480p" },
                            rate = 0.015m
                        }
                    }
                },
                "per_step" => new
                {
                    pricingType = "per_step",
                    defaultRate = 0.00013m,
                    unitField = "InferenceSteps",
                    rules = new object[]
                    {
                        new
                        {
                            priority = 1,
                            description = "High quality (50+ steps)",
                            conditions = new Dictionary<string, object> { ["inference_steps_gte"] = 50 },
                            rate = 0.00015m
                        },
                        new
                        {
                            priority = 2,
                            description = "Standard quality",
                            conditions = new Dictionary<string, object>(),
                            rate = 0.00013m
                        }
                    }
                },
                _ => new
                {
                    pricingType = "per_second",
                    defaultRate = 0.025m,
                    unitField = "VideoDurationSeconds",
                    rules = Array.Empty<object>()
                }
            };

            return Results.Ok(template);
        }

        private void LogAdminAudit(string operation, string entityType, object? entityId = null, string? detail = null) => AdminAudit.Log(_httpContextAccessor.HttpContext!, _logger, operation, entityType, entityId, detail);
    }
}
