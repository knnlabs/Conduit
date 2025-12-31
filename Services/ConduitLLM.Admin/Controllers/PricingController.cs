using System.Text.Json;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing pricing rules validation, simulation, and audit
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class PricingController : ControllerBase
    {
        private readonly IPricingRulesValidator _pricingValidator;
        private readonly IPricingRulesEvaluator _pricingEvaluator;
        private readonly IPricingAuditService _pricingAuditService;
        private readonly ILogger<PricingController> _logger;

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

        /// <summary>
        /// Initializes a new instance of the PricingController
        /// </summary>
        public PricingController(
            IPricingRulesValidator pricingValidator,
            IPricingRulesEvaluator pricingEvaluator,
            IPricingAuditService pricingAuditService,
            ILogger<PricingController> logger)
        {
            _pricingValidator = pricingValidator ?? throw new ArgumentNullException(nameof(pricingValidator));
            _pricingEvaluator = pricingEvaluator ?? throw new ArgumentNullException(nameof(pricingEvaluator));
            _pricingAuditService = pricingAuditService ?? throw new ArgumentNullException(nameof(pricingAuditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validate pricing configuration JSON
        /// </summary>
        /// <param name="request">The pricing configuration to validate</param>
        /// <returns>Validation result with any errors</returns>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(PricingValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ValidatePricingConfiguration([FromBody] PricingValidationRequest request)
        {
            try
            {
                using var timer = PricingOperationDuration.WithLabels("validate").NewTimer();

                PricingRulesConfig? config = null;
                try
                {
                    config = JsonSerializer.Deserialize<PricingRulesConfig>(request.PricingConfiguration, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    PricingValidations.WithLabels("invalid_json").Inc();
                    return Ok(new PricingValidationResponse
                    {
                        IsValid = false,
                        Errors = new[] { $"Invalid JSON format: {ex.Message}" }
                    });
                }

                if (config == null)
                {
                    PricingValidations.WithLabels("null_config").Inc();
                    return Ok(new PricingValidationResponse
                    {
                        IsValid = false,
                        Errors = new[] { "Configuration could not be parsed" }
                    });
                }

                var result = _pricingValidator.Validate(config);

                PricingValidations.WithLabels(result.IsValid ? "valid" : "invalid").Inc();
                return Ok(new PricingValidationResponse
                {
                    IsValid = result.IsValid,
                    Errors = result.Errors.Select(e => $"[{e.Field}] {e.Message}" + (e.RuleIndex.HasValue ? $" (rule {e.RuleIndex})" : "")).ToArray(),
                    Warnings = result.Warnings.ToArray()
                });
            }
            catch (Exception ex)
            {
                PricingValidations.WithLabels("error").Inc();
                _logger.LogError(ex, "Error validating pricing configuration");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while validating pricing configuration");
            }
        }

        /// <summary>
        /// Simulate pricing calculation with test parameters
        /// </summary>
        /// <param name="request">The simulation request with configuration and test parameters</param>
        /// <returns>Calculated pricing result</returns>
        [HttpPost("simulate")]
        [ProducesResponseType(typeof(PricingSimulationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult SimulatePricing([FromBody] PricingSimulationRequest request)
        {
            try
            {
                using var timer = PricingOperationDuration.WithLabels("simulate").NewTimer();

                // Parse pricing configuration
                PricingRulesConfig? config = null;
                try
                {
                    config = JsonSerializer.Deserialize<PricingRulesConfig>(request.PricingConfiguration, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    PricingSimulations.WithLabels("invalid_json").Inc();
                    return BadRequest($"Invalid pricing configuration JSON: {ex.Message}");
                }

                if (config == null)
                {
                    PricingSimulations.WithLabels("null_config").Inc();
                    return BadRequest("Configuration could not be parsed");
                }

                // Validate configuration first
                var validationResult = _pricingValidator.Validate(config);
                if (!validationResult.IsValid)
                {
                    PricingSimulations.WithLabels("invalid_config").Inc();
                    return BadRequest(new
                    {
                        Message = "Pricing configuration is invalid",
                        Errors = validationResult.Errors
                    });
                }

                // Build usage object for simulation
                var usage = new ConduitLLM.Core.Models.Usage
                {
                    VideoDurationSeconds = request.VideoDurationSeconds,
                    VideoResolution = request.VideoResolution,
                    ImageCount = request.ImageCount,
                    ImageResolution = request.ImageResolution,
                    ImageQuality = request.ImageQuality,
                    PricingParameters = request.Parameters ?? new Dictionary<string, object>()
                };

                // Evaluate the pricing rules
                var result = _pricingEvaluator.Evaluate(config, request.Parameters ?? new Dictionary<string, object>(), usage);

                PricingSimulations.WithLabels("success").Inc();
                return Ok(new PricingSimulationResponse
                {
                    CalculatedCost = result.Cost,
                    AppliedRate = result.Rate,
                    Quantity = result.Quantity,
                    MatchedRule = result.MatchedRule != null ? new MatchedRuleInfo
                    {
                        Description = result.MatchedRule.Description,
                        Priority = result.MatchedRule.Priority,
                        Rate = result.MatchedRule.Rate,
                        ConditionsSummary = result.MatchedRule.Conditions?.Select(c => $"{c.Key} = {c.Value}").ToArray()
                    } : null,
                    UsedDefaultRate = result.UsedDefaultRate,
                    WarningMessage = result.UsedDefaultRate ? "No matching rule found, default rate was used" : null
                });
            }
            catch (Exception ex)
            {
                PricingSimulations.WithLabels("error").Inc();
                _logger.LogError(ex, "Error simulating pricing");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while simulating pricing");
            }
        }

        /// <summary>
        /// Get available pricing types
        /// </summary>
        /// <returns>List of pricing types with descriptions</returns>
        [HttpGet("types")]
        [ProducesResponseType(typeof(IEnumerable<PricingTypeInfo>), StatusCodes.Status200OK)]
        public IActionResult GetPricingTypes()
        {
            var pricingTypes = new[]
            {
                new PricingTypeInfo { Type = "per_unit", Description = "Per unit pricing (e.g., per image, per video)", Example = "1.0 * rate" },
                new PricingTypeInfo { Type = "per_second", Description = "Per second pricing (e.g., video duration)", Example = "duration_seconds * rate" },
                new PricingTypeInfo { Type = "per_step", Description = "Per inference step pricing", Example = "inference_steps * rate" },
                new PricingTypeInfo { Type = "per_token", Description = "Per token pricing (input/output)", Example = "tokens * rate / 1_000_000" }
            };

            return Ok(pricingTypes);
        }

        /// <summary>
        /// Get available condition operators
        /// </summary>
        /// <returns>List of operators with descriptions</returns>
        [HttpGet("operators")]
        [ProducesResponseType(typeof(IEnumerable<OperatorInfo>), StatusCodes.Status200OK)]
        public IActionResult GetConditionOperators()
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

            return Ok(operators);
        }

        /// <summary>
        /// Get pricing configuration template
        /// </summary>
        /// <param name="pricingType">The pricing type to get template for</param>
        /// <returns>JSON template for the pricing configuration</returns>
        [HttpGet("template")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetPricingTemplate([FromQuery] string? pricingType = "per_second")
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

            return Ok(template);
        }

        /// <summary>
        /// Query pricing audit events
        /// </summary>
        [HttpPost("audit/query")]
        [ProducesResponseType(typeof(PricingAuditQueryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QueryPricingAuditEvents([FromBody] PricingAuditQueryRequest request)
        {
            if (request.From > request.To)
            {
                return BadRequest("From date must be before or equal to To date");
            }

            if (request.PageSize > 1000)
            {
                return BadRequest("Page size cannot exceed 1000");
            }

            try
            {
                using var timer = PricingOperationDuration.WithLabels("audit_query").NewTimer();

                var (events, totalCount) = await _pricingAuditService.GetAuditEventsAsync(
                    request.From,
                    request.To,
                    request.VirtualKeyId,
                    request.ModelId,
                    request.PricingType,
                    request.PageNumber,
                    request.PageSize);

                var response = new PricingAuditQueryResponse
                {
                    Events = events.Select(e => new PricingAuditEventDto
                    {
                        Id = e.Id,
                        Timestamp = e.Timestamp,
                        VirtualKeyId = e.VirtualKeyId,
                        ModelId = e.ModelId,
                        ModelCostId = e.ModelCostId,
                        PricingType = e.PricingType,
                        InputParameters = e.InputParameters,
                        MatchedRule = e.MatchedRule,
                        UsedDefaultRate = e.UsedDefaultRate,
                        AppliedRate = e.AppliedRate,
                        Quantity = e.Quantity,
                        CalculatedCost = e.CalculatedCost,
                        RequestId = e.RequestId
                    }).ToList(),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying pricing audit events");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while querying pricing audit events");
            }
        }

        /// <summary>
        /// Get pricing audit summary
        /// </summary>
        [HttpGet("audit/summary")]
        [ProducesResponseType(typeof(PricingAuditSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPricingAuditSummary(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? virtualKeyId = null)
        {
            if (from > to)
            {
                return BadRequest("From date must be before or equal to To date");
            }

            try
            {
                using var timer = PricingOperationDuration.WithLabels("audit_summary").NewTimer();

                var summary = await _pricingAuditService.GetSummaryAsync(from, to, virtualKeyId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pricing audit summary");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while getting pricing audit summary");
            }
        }

        /// <summary>
        /// Get pricing audit events by request ID
        /// </summary>
        [HttpGet("audit/request/{requestId}")]
        [ProducesResponseType(typeof(IEnumerable<PricingAuditEventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPricingAuditByRequestId(string requestId)
        {
            try
            {
                var events = await _pricingAuditService.GetByRequestIdAsync(requestId);

                if (!events.Any())
                {
                    return NotFound($"No pricing audit events found for request {requestId}");
                }

                return Ok(events.Select(e => new PricingAuditEventDto
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    VirtualKeyId = e.VirtualKeyId,
                    ModelId = e.ModelId,
                    ModelCostId = e.ModelCostId,
                    PricingType = e.PricingType,
                    InputParameters = e.InputParameters,
                    MatchedRule = e.MatchedRule,
                    UsedDefaultRate = e.UsedDefaultRate,
                    AppliedRate = e.AppliedRate,
                    Quantity = e.Quantity,
                    CalculatedCost = e.CalculatedCost,
                    RequestId = e.RequestId
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pricing audit events for request {RequestId}", requestId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while getting pricing audit events");
            }
        }
    }

    #region DTOs

    /// <summary>
    /// Request to validate pricing configuration
    /// </summary>
    public class PricingValidationRequest
    {
        /// <summary>
        /// The pricing configuration JSON to validate
        /// </summary>
        public string PricingConfiguration { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from pricing validation
    /// </summary>
    public class PricingValidationResponse
    {
        /// <summary>
        /// Whether the configuration is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation errors if any
        /// </summary>
        public string[] Errors { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Validation warnings if any
        /// </summary>
        public string[] Warnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Request to simulate pricing calculation
    /// </summary>
    public class PricingSimulationRequest
    {
        /// <summary>
        /// The pricing configuration JSON
        /// </summary>
        public string PricingConfiguration { get; set; } = string.Empty;

        /// <summary>
        /// Parameters for the simulation
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }

        /// <summary>
        /// Video duration in seconds (for per_second pricing)
        /// </summary>
        public double? VideoDurationSeconds { get; set; }

        /// <summary>
        /// Video resolution (e.g., "1080p")
        /// </summary>
        public string? VideoResolution { get; set; }

        /// <summary>
        /// Image count (for per_unit pricing)
        /// </summary>
        public int? ImageCount { get; set; }

        /// <summary>
        /// Image resolution (e.g., "1024x1024")
        /// </summary>
        public string? ImageResolution { get; set; }

        /// <summary>
        /// Image quality (e.g., "hd", "standard")
        /// </summary>
        public string? ImageQuality { get; set; }
    }

    /// <summary>
    /// Response from pricing simulation
    /// </summary>
    public class PricingSimulationResponse
    {
        /// <summary>
        /// The calculated cost
        /// </summary>
        public decimal CalculatedCost { get; set; }

        /// <summary>
        /// The rate that was applied
        /// </summary>
        public decimal AppliedRate { get; set; }

        /// <summary>
        /// The quantity used in calculation
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Information about the matched rule, if any
        /// </summary>
        public MatchedRuleInfo? MatchedRule { get; set; }

        /// <summary>
        /// Whether the default rate was used
        /// </summary>
        public bool UsedDefaultRate { get; set; }

        /// <summary>
        /// Warning message if any
        /// </summary>
        public string? WarningMessage { get; set; }
    }

    /// <summary>
    /// Information about a matched pricing rule
    /// </summary>
    public class MatchedRuleInfo
    {
        /// <summary>
        /// Rule description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Rule priority
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Rule rate
        /// </summary>
        public decimal Rate { get; set; }

        /// <summary>
        /// Summary of conditions
        /// </summary>
        public string[]? ConditionsSummary { get; set; }
    }

    /// <summary>
    /// Information about a pricing type
    /// </summary>
    public class PricingTypeInfo
    {
        /// <summary>
        /// The pricing type identifier
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Description of the pricing type
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Example calculation
        /// </summary>
        public string Example { get; set; } = string.Empty;
    }

    /// <summary>
    /// Information about a condition operator
    /// </summary>
    public class OperatorInfo
    {
        /// <summary>
        /// The operator identifier
        /// </summary>
        public string Operator { get; set; } = string.Empty;

        /// <summary>
        /// Description of the operator
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Example usage
        /// </summary>
        public string Example { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to query pricing audit events
    /// </summary>
    public class PricingAuditQueryRequest
    {
        /// <summary>
        /// Start date
        /// </summary>
        public DateTime From { get; set; }

        /// <summary>
        /// End date
        /// </summary>
        public DateTime To { get; set; }

        /// <summary>
        /// Optional virtual key ID filter
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Optional model ID filter
        /// </summary>
        public string? ModelId { get; set; }

        /// <summary>
        /// Optional pricing type filter
        /// </summary>
        public string? PricingType { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Response from pricing audit query
    /// </summary>
    public class PricingAuditQueryResponse
    {
        /// <summary>
        /// The audit events
        /// </summary>
        public List<PricingAuditEventDto> Events { get; set; } = new();

        /// <summary>
        /// Total count of matching events
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }
    }

    /// <summary>
    /// Pricing audit event DTO
    /// </summary>
    public class PricingAuditEventDto
    {
        /// <summary>Unique identifier for the audit event.</summary>
        public long Id { get; set; }
        /// <summary>When the pricing event occurred.</summary>
        public DateTime Timestamp { get; set; }
        /// <summary>The virtual key ID associated with this event.</summary>
        public int VirtualKeyId { get; set; }
        /// <summary>The model identifier used for pricing.</summary>
        public string ModelId { get; set; } = string.Empty;
        /// <summary>The model cost configuration ID that was applied.</summary>
        public int ModelCostId { get; set; }
        /// <summary>The type of pricing applied (e.g., token, image, audio).</summary>
        public string PricingType { get; set; } = string.Empty;
        /// <summary>JSON representation of input parameters used for pricing calculation.</summary>
        public string InputParameters { get; set; } = string.Empty;
        /// <summary>The pricing rule that matched, if any.</summary>
        public string? MatchedRule { get; set; }
        /// <summary>Whether the default rate was used instead of a specific rule.</summary>
        public bool UsedDefaultRate { get; set; }
        /// <summary>The rate that was applied for pricing.</summary>
        public decimal AppliedRate { get; set; }
        /// <summary>The quantity (tokens, images, seconds, etc.) being priced.</summary>
        public decimal Quantity { get; set; }
        /// <summary>The final calculated cost.</summary>
        public decimal CalculatedCost { get; set; }
        /// <summary>The request ID for correlation, if available.</summary>
        public string? RequestId { get; set; }
    }

    #endregion
}
