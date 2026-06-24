using ConduitLLM.Core.Extensions;
using System.Text;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing model costs
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class ModelCostsController : AdminControllerBase
    {
        private readonly IAdminModelCostService _modelCostService;
        private readonly IPricingRulesValidator _pricingRulesValidator;

        /// <summary>
        /// Initializes a new instance of the ModelCostsController
        /// </summary>
        /// <param name="modelCostService">The model cost service</param>
        /// <param name="pricingRulesValidator">The pricing rules validator</param>
        /// <param name="logger">The logger</param>
        public ModelCostsController(
            IAdminModelCostService modelCostService,
            IPricingRulesValidator pricingRulesValidator,
            ILogger<ModelCostsController> logger)
            : base(logger)
        {
            _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
            _pricingRulesValidator = pricingRulesValidator ?? throw new ArgumentNullException(nameof(pricingRulesValidator));
        }

        /// <summary>
        /// Gets all model costs with optional pagination and filtering
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="modelType">Optional filter by model type (chat, image, video, embedding, audio)</param>
        /// <returns>List of all model costs or paginated response</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelCostDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllModelCosts(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? modelType = null)
        {
            var modelCosts = await _modelCostService.GetAllModelCostsAsync();

            // Apply modelType filter if provided
            if (!string.IsNullOrWhiteSpace(modelType))
            {
                modelCosts = modelCosts.Where(c =>
                    string.Equals(c.ModelType, modelType, StringComparison.OrdinalIgnoreCase));
            }

            // If pagination parameters are provided, return paginated response
            if (page.HasValue && pageSize.HasValue)
            {
                var totalCount = modelCosts.Count();
                var items = modelCosts
                    .Skip((page.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value)
                    .ToList();

                return Ok(new
                {
                    items = items,
                    totalCount = totalCount,
                    page = page.Value,
                    pageSize = pageSize.Value,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize.Value)
                });
            }

            // Otherwise return all items (backward compatibility)
            return Ok(modelCosts);
        }

        /// <summary>
        /// Gets a model cost by ID
        /// </summary>
        /// <param name="id">The ID of the model cost</param>
        /// <returns>The model cost</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ModelCostDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelCostById(int id)
        {
            var modelCost = await _modelCostService.GetModelCostByIdAsync(id);
            if (modelCost == null)
            {
                return this.NotFoundEntity("Model cost", id);
            }
            return Ok(modelCost);
        }

        /// <summary>
        /// Gets model costs by provider ID
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <returns>List of model costs for the specified provider</returns>
        [HttpGet("provider/{providerId}")]
        [ProducesResponseType(typeof(IEnumerable<ModelCostDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelCostsByProvider(int providerId)
        {
            var result = await _modelCostService.GetModelCostsByProviderAsync(providerId);
            return Ok(result);
        }

        /// <summary>
        /// Gets a model cost by cost name
        /// </summary>
        /// <param name="costName">The cost name</param>
        /// <returns>The model cost</returns>
        [HttpGet("name/{costName}")]
        [ProducesResponseType(typeof(ModelCostDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelCostByCostName(string costName)
        {
            var modelCost = await _modelCostService.GetModelCostByCostNameAsync(costName);
            if (modelCost == null)
            {
                return this.NotFoundEntity("Model cost", costName);
            }
            return Ok(modelCost);
        }

        /// <summary>
        /// Creates a new model cost
        /// </summary>
        /// <param name="modelCost">The model cost to create</param>
        /// <returns>The created model cost</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ModelCostDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateModelCost([FromBody] CreateModelCostDto modelCost)
        {
            var result = await _modelCostService.CreateModelCostAsync(modelCost);
            LogAdminAudit("Created", "ModelCost", result.Id, $"CostName: {LoggingSanitizer.S(result.CostName)}");
            return CreatedAtAction(nameof(GetModelCostById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Updates a model cost
        /// </summary>
        /// <param name="id">The ID of the model cost to update</param>
        /// <param name="modelCost">The updated model cost data</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateModelCost(int id, [FromBody] UpdateModelCostDto modelCost)
        {
            // Ensure ID in route matches ID in body
            if (id != modelCost.Id)
            {
                return BadRequest("ID in route must match ID in body");
            }

            var success = await _modelCostService.UpdateModelCostAsync(modelCost);

            if (!success)
            {
                throw new KeyNotFoundException($"Model cost with ID '{id}' not found");
            }

            LogAdminAudit("Updated", "ModelCost", id, $"CostName: {LoggingSanitizer.S(modelCost.CostName)}");

            return NoContent();
        }

        /// <summary>
        /// Deletes a model cost
        /// </summary>
        /// <param name="id">The ID of the model cost to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteModelCost(int id)
        {
            var existing = await _modelCostService.GetModelCostByIdAsync(id);
            var success = await _modelCostService.DeleteModelCostAsync(id);

            if (!success)
            {
                throw new KeyNotFoundException($"Model cost with ID '{id}' not found");
            }

            LogAdminAudit("Deleted", "ModelCost", id, existing != null ? $"CostName: {LoggingSanitizer.S(existing.CostName)}" : null);

            return NoContent();
        }

        /// <summary>
        /// Gets model cost overview data for a specific time period
        /// </summary>
        /// <param name="startDate">The start date for the period (inclusive)</param>
        /// <param name="endDate">The end date for the period (inclusive)</param>
        /// <returns>List of model cost overview data</returns>
        [HttpGet("overview")]
        [ProducesResponseType(typeof(IEnumerable<ModelCostOverviewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelCostOverview(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest("Start date cannot be after end date");
            }

            var result = await _modelCostService.GetModelCostOverviewAsync(startDate, endDate);
            return Ok(result);
        }

        /// <summary>
        /// Imports model costs from a list of DTOs
        /// </summary>
        /// <param name="modelCosts">The list of model costs to import</param>
        /// <returns>The number of model costs imported</returns>
        [HttpPost("import")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportModelCosts([FromBody] IEnumerable<CreateModelCostDto> modelCosts)
        {
            if (modelCosts == null || !modelCosts.Any())
            {
                return BadRequest("No model costs provided for import");
            }

            var result = await _modelCostService.ImportModelCostsAsync(modelCosts);
            LogAdminAudit("Imported", "ModelCost", detail: $"Count: {result}");
            return Ok(result);
        }

        /// <summary>
        /// Exports model costs in CSV format
        /// </summary>
        /// <param name="providerId">Optional provider ID to filter by</param>
        /// <returns>CSV file containing model costs</returns>
        [HttpGet("export/csv")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportCsv([FromQuery] int? providerId = null)
        {
            var result = await _modelCostService.ExportModelCostsAsync("csv", providerId);
            var bytes = Encoding.UTF8.GetBytes(result);
            var fileName = $"model-costs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Exports model costs in JSON format
        /// </summary>
        /// <param name="providerId">Optional provider ID to filter by</param>
        /// <returns>JSON file containing model costs</returns>
        [HttpGet("export/json")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportJson([FromQuery] int? providerId = null)
        {
            var result = await _modelCostService.ExportModelCostsAsync("json", providerId);
            var bytes = Encoding.UTF8.GetBytes(result);
            var fileName = $"model-costs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json";
            return File(bytes, "application/json", fileName);
        }

        /// <summary>
        /// Imports model costs from CSV file
        /// </summary>
        /// <param name="file">CSV file containing model costs</param>
        /// <returns>Import result with statistics</returns>
        [HttpPost("import/csv")]
        // [ProducesResponseType(typeof(BulkImportResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ErrorResponseDto("No file provided for import"));
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ErrorResponseDto("File must be a CSV file"));
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var csvData = await reader.ReadToEndAsync();

            var result = await _modelCostService.ImportModelCostsAsync(csvData, "csv");

            if (result.SuccessCount == 0 && result.FailureCount > 0)
            {
                throw new InvalidOperationException(
                    System.Text.Json.JsonSerializer.Serialize(new {
                        message = "Import failed",
                        errors = result.Errors,
                        successCount = result.SuccessCount,
                        failureCount = result.FailureCount
                    }));
            }

            LogAdminAuditBulk("ImportedCsv", "ModelCost", result.SuccessCount, result.FailureCount);
            return Ok(result);
        }

        /// <summary>
        /// Imports model costs from JSON file
        /// </summary>
        /// <param name="file">JSON file containing model costs</param>
        /// <returns>Import result with statistics</returns>
        [HttpPost("import/json")]
        // [ProducesResponseType(typeof(BulkImportResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportJson(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided for import");
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File must be a JSON file");
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var jsonData = await reader.ReadToEndAsync();

            var result = await _modelCostService.ImportModelCostsAsync(jsonData, "json");

            if (result.SuccessCount == 0 && result.FailureCount > 0)
            {
                throw new InvalidOperationException(
                    System.Text.Json.JsonSerializer.Serialize(new {
                        message = "Import failed",
                        errors = result.Errors,
                        successCount = result.SuccessCount,
                        failureCount = result.FailureCount
                    }));
            }

            LogAdminAuditBulk("ImportedJson", "ModelCost", result.SuccessCount, result.FailureCount);
            return Ok(result);
        }

        /// <summary>
        /// Validates a pricing rules configuration JSON
        /// </summary>
        /// <param name="id">The ID of the model cost (used to retrieve associated model's parameter schema)</param>
        /// <param name="request">The pricing configuration to validate</param>
        /// <returns>Validation result with errors and warnings</returns>
        [HttpPost("{id}/validate-pricing-rules")]
        [ProducesResponseType(typeof(ValidationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidatePricingRules(
            int id,
            [FromBody] ValidatePricingRulesRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PricingConfiguration))
            {
                return BadRequest(new ErrorResponseDto("Pricing configuration is required"));
            }

            // Verify the model cost exists
            var modelCost = await _modelCostService.GetModelCostByIdAsync(id);
            if (modelCost == null)
            {
                throw new KeyNotFoundException($"Model cost with ID '{id}' not found");
            }

            // Get parameter schema from associated model if available
            string? parameterSchema = null;
            if (!string.IsNullOrEmpty(request.ParameterSchema))
            {
                // Use provided schema (for testing or when model schema is known)
                parameterSchema = request.ParameterSchema;
            }
            // TODO: In the future, we could look up the model's parameter schema from ModelSeries

            // Validate the configuration
            var result = _pricingRulesValidator.ValidateJson(request.PricingConfiguration, parameterSchema);
            return Ok(result);
        }

        /// <summary>
        /// Validates a pricing rules configuration JSON without a model cost context
        /// </summary>
        /// <param name="request">The pricing configuration to validate</param>
        /// <returns>Validation result with errors and warnings</returns>
        [HttpPost("validate-pricing-rules")]
        [ProducesResponseType(typeof(ValidationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidatePricingRulesStandalone([FromBody] ValidatePricingRulesRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PricingConfiguration))
            {
                return BadRequest(new ErrorResponseDto("Pricing configuration is required"));
            }

            await Task.CompletedTask;

            var result = _pricingRulesValidator.ValidateJson(
                request.PricingConfiguration,
                request.ParameterSchema);
            return Ok(result);
        }
    }

    /// <summary>
    /// Request model for validating pricing rules
    /// </summary>
    public class ValidatePricingRulesRequest
    {
        /// <summary>
        /// The pricing configuration JSON to validate
        /// </summary>
        public string PricingConfiguration { get; set; } = string.Empty;

        /// <summary>
        /// Optional parameter schema JSON for validation against model parameters
        /// </summary>
        public string? ParameterSchema { get; set; }
    }
}
