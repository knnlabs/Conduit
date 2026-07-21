using ConduitLLM.Core.Extensions;
using System.Text;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Controller for managing model costs
    /// </summary>
    public class ModelCostsEndpoints
    {
        private readonly IAdminModelCostService _modelCostService;
        private readonly IPricingRulesValidator _pricingRulesValidator;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ModelCostsEndpoints> _logger;

        /// <summary>
        /// Initializes the Model Costs endpoint handler.
        /// </summary>
        /// <param name="modelCostService">The model cost service</param>
        /// <param name="pricingRulesValidator">The pricing rules validator</param>
        /// <param name="logger">The logger</param>
        public ModelCostsEndpoints(
            IAdminModelCostService modelCostService,
            IPricingRulesValidator pricingRulesValidator,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ModelCostsEndpoints> logger)
        {
            _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
            _pricingRulesValidator = pricingRulesValidator ?? throw new ArgumentNullException(nameof(pricingRulesValidator));
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public static IEndpointRouteBuilder MapModelCostsEndpoints(IEndpointRouteBuilder app)
        {
            var g = app.MapGroup("/api/ModelCosts").RequireAuthorization("MasterKeyPolicy").AddEndpointFilter<ValidationEndpointFilter>().AddEndpointFilter<OperationLoggingEndpointFilter>().WithTags("Model Costs");
            g.MapGet("/", ([FromServices] ModelCostsEndpoints e, int? page=null, int? pageSize=null, string? modelType=null, int? providerId=null, bool? isActive=null) => e.GetAllModelCosts(page,pageSize,modelType,providerId,isActive)).WithName("ModelCosts_GetAll").Produces<PagedResult<ModelCostDto>>();
            g.MapGet("/{id}", ([FromServices] ModelCostsEndpoints e,int id)=>e.GetModelCostById(id)).WithName("ModelCosts_GetById").Produces<ModelCostDto>().Produces(StatusCodes.Status404NotFound);
            g.MapGet("/provider/{providerId}", ([FromServices] ModelCostsEndpoints e,int providerId)=>e.GetModelCostsByProvider(providerId)).WithName("ModelCosts_GetByProvider").Produces<IEnumerable<ModelCostDto>>();
            g.MapGet("/name/{costName}", ([FromServices] ModelCostsEndpoints e,string costName)=>e.GetModelCostByCostName(costName)).WithName("ModelCosts_GetByName").Produces<ModelCostDto>().Produces(StatusCodes.Status404NotFound);
            g.MapPost("/", ([FromServices] ModelCostsEndpoints e,CreateModelCostDto d)=>e.CreateModelCost(d)).WithName("ModelCosts_Create").Produces<ModelCostDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);
            g.MapPut("/{id}", ([FromServices] ModelCostsEndpoints e,int id,UpdateModelCostDto d)=>e.UpdateModelCost(id,d)).WithName("ModelCosts_Update").Produces<ModelCostDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
            g.MapDelete("/{id}", ([FromServices] ModelCostsEndpoints e,int id)=>e.DeleteModelCost(id)).WithName("ModelCosts_Delete").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);
            g.MapGet("/overview", ([FromServices] ModelCostsEndpoints e,DateTime? startDate=null,DateTime? endDate=null)=>e.GetModelCostOverview(startDate ?? default,endDate ?? default)).WithName("ModelCosts_GetOverview").Produces<IEnumerable<ModelCostOverviewDto>>().Produces(StatusCodes.Status400BadRequest);
            g.MapPost("/import", ([FromServices] ModelCostsEndpoints e,IEnumerable<CreateModelCostDto> d)=>e.ImportModelCosts(d)).WithName("ModelCosts_Import").Accepts<IEnumerable<CreateModelCostDto>>("application/json", "text/json", "application/*+json").Produces<BulkImportResult>().Produces(StatusCodes.Status400BadRequest);
            g.MapGet("/export/csv", ([FromServices] ModelCostsEndpoints e,int? providerId=null)=>e.ExportCsv(providerId)).WithName("ModelCosts_ExportCsv").Produces(StatusCodes.Status200OK,typeof(void),"text/csv");
            g.MapGet("/export/json", ([FromServices] ModelCostsEndpoints e,int? providerId=null)=>e.ExportJson(providerId)).WithName("ModelCosts_ExportJson").Produces(StatusCodes.Status200OK,typeof(void),"application/json");
            g.MapPost("/import/csv", ([FromServices] ModelCostsEndpoints e,IFormFile file)=>e.ImportCsv(file)).WithName("ModelCosts_ImportCsv").DisableAntiforgery().Accepts<IFormFile>("multipart/form-data").Produces<BulkImportResult>().Produces(StatusCodes.Status400BadRequest);
            g.MapPost("/import/json", ([FromServices] ModelCostsEndpoints e,IFormFile file)=>e.ImportJson(file)).WithName("ModelCosts_ImportJson").DisableAntiforgery().Accepts<IFormFile>("multipart/form-data").Produces<BulkImportResult>().Produces(StatusCodes.Status400BadRequest);
            g.MapPost("/{id}/validate-pricing-rules", ([FromServices] ModelCostsEndpoints e,int id,ValidatePricingRulesRequest d)=>e.ValidatePricingRules(id,d)).WithName("ModelCosts_ValidatePricingRules").Produces<ValidationResult>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
            g.MapPost("/validate-pricing-rules", ([FromServices] ModelCostsEndpoints e,ValidatePricingRulesRequest d)=>e.ValidatePricingRulesStandalone(d)).WithName("ModelCosts_ValidatePricingRulesStandalone").Produces<ValidationResult>().Produces(StatusCodes.Status400BadRequest);
            return app;
        }

        /// <summary>
        /// Gets all model costs with optional pagination and filtering
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="modelType">Optional filter by model type (chat, image, video, embedding, audio)</param>
        /// <returns>List of all model costs or paginated response</returns>
        public async Task<IResult> GetAllModelCosts(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? modelType = null,
            [FromQuery] int? providerId = null,
            [FromQuery] bool? isActive = null)
        {
            var effectivePage = Math.Max(1, page ?? 1);
            var effectivePageSize = Math.Clamp(pageSize ?? 50, 1, 100);
            var modelCosts = providerId.HasValue
                ? await _modelCostService.GetModelCostsByProviderAsync(providerId.Value)
                : await _modelCostService.GetAllModelCostsAsync();

            // Apply modelType filter if provided
            if (!string.IsNullOrWhiteSpace(modelType))
            {
                modelCosts = modelCosts.Where(c =>
                    string.Equals(c.ModelType, modelType, StringComparison.OrdinalIgnoreCase));
            }

            if (isActive.HasValue)
                modelCosts = modelCosts.Where(c => c.IsActive == isActive.Value);

            var totalCount = modelCosts.Count();
            return Results.Ok(new PagedResult<ModelCostDto>
            {
                Items = modelCosts.Skip((effectivePage - 1) * effectivePageSize).Take(effectivePageSize).ToList(),
                TotalCount = totalCount,
                Page = effectivePage,
                PageSize = effectivePageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)effectivePageSize)
            });
        }

        /// <summary>
        /// Gets a model cost by ID
        /// </summary>
        /// <param name="id">The ID of the model cost</param>
        /// <returns>The model cost</returns>
        public async Task<IResult> GetModelCostById(int id)
        {
            var modelCost = await _modelCostService.GetModelCostByIdAsync(id);
            if (modelCost == null)
            {
                return AdminResults.NotFoundEntity("Model cost", id);
            }
            return Results.Ok(modelCost);
        }

        /// <summary>
        /// Gets model costs by provider ID
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <returns>List of model costs for the specified provider</returns>
        public async Task<IResult> GetModelCostsByProvider(int providerId)
        {
            var result = await _modelCostService.GetModelCostsByProviderAsync(providerId);
            return Results.Ok(result);
        }

        /// <summary>
        /// Gets a model cost by cost name
        /// </summary>
        /// <param name="costName">The cost name</param>
        /// <returns>The model cost</returns>
        public async Task<IResult> GetModelCostByCostName(string costName)
        {
            var modelCost = await _modelCostService.GetModelCostByCostNameAsync(costName);
            if (modelCost == null)
            {
                return AdminResults.NotFoundEntity("Model cost", costName);
            }
            return Results.Ok(modelCost);
        }

        /// <summary>
        /// Creates a new model cost
        /// </summary>
        /// <param name="modelCost">The model cost to create</param>
        /// <returns>The created model cost</returns>
        public async Task<IResult> CreateModelCost(CreateModelCostDto modelCost)
        {
            var result = await _modelCostService.CreateModelCostAsync(modelCost);
            LogAdminAudit("Created", "ModelCost", result.Id, $"CostName: {LoggingSanitizer.S(result.CostName)}");
            return Results.Created($"/api/ModelCosts/{result.Id}", result);
        }

        /// <summary>
        /// Updates a model cost
        /// </summary>
        /// <param name="id">The ID of the model cost to update</param>
        /// <param name="modelCost">The updated model cost data</param>
        /// <returns>No content if successful</returns>
        public async Task<IResult> UpdateModelCost(int id, UpdateModelCostDto modelCost)
        {
            var updated = await _modelCostService.UpdateModelCostAsync(id, modelCost);

            if (updated == null)
            {
                throw new KeyNotFoundException($"Model cost with ID '{id}' not found");
            }

            LogAdminAudit("Updated", "ModelCost", id, $"CostName: {LoggingSanitizer.S(modelCost.CostName)}");

            return Results.Ok(updated);
        }

        /// <summary>
        /// Deletes a model cost
        /// </summary>
        /// <param name="id">The ID of the model cost to delete</param>
        /// <returns>No content if successful</returns>
        public async Task<IResult> DeleteModelCost(int id)
        {
            var existing = await _modelCostService.GetModelCostByIdAsync(id);
            var success = await _modelCostService.DeleteModelCostAsync(id);

            if (!success)
            {
                throw new KeyNotFoundException($"Model cost with ID '{id}' not found");
            }

            LogAdminAudit("Deleted", "ModelCost", id, existing != null ? $"CostName: {LoggingSanitizer.S(existing.CostName)}" : null);

            return Results.NoContent();
        }

        /// <summary>
        /// Gets model cost overview data for a specific time period
        /// </summary>
        /// <param name="startDate">The start date for the period (inclusive)</param>
        /// <param name="endDate">The end date for the period (inclusive)</param>
        /// <returns>List of model cost overview data</returns>
        public async Task<IResult> GetModelCostOverview(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            if (startDate > endDate)
            {
                return Results.BadRequest("Start date cannot be after end date");
            }

            var result = await _modelCostService.GetModelCostOverviewAsync(startDate, endDate);
            return Results.Ok(result);
        }

        /// <summary>
        /// Imports model costs from a list of DTOs
        /// </summary>
        /// <param name="modelCosts">The list of model costs to import</param>
        /// <returns>The number of model costs imported</returns>
        public async Task<IResult> ImportModelCosts(IEnumerable<CreateModelCostDto> modelCosts)
        {
            if (modelCosts == null || !modelCosts.Any())
            {
                return Results.BadRequest("No model costs provided for import");
            }

            var result = await _modelCostService.ImportModelCostsAsync(modelCosts);
            LogAdminAuditBulk("Imported", "ModelCost", result.SuccessCount, result.FailureCount);
            return Results.Ok(result);
        }

        /// <summary>
        /// Exports model costs in CSV format
        /// </summary>
        /// <param name="providerId">Optional provider ID to filter by</param>
        /// <returns>CSV file containing model costs</returns>
        public async Task<IResult> ExportCsv(int? providerId = null)
        {
            var result = await _modelCostService.ExportModelCostsAsync("csv", providerId);
            var bytes = Encoding.UTF8.GetBytes(result);
            var fileName = $"model-costs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.csv";
            return Results.File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Exports model costs in JSON format
        /// </summary>
        /// <param name="providerId">Optional provider ID to filter by</param>
        /// <returns>JSON file containing model costs</returns>
        public async Task<IResult> ExportJson(int? providerId = null)
        {
            var result = await _modelCostService.ExportModelCostsAsync("json", providerId);
            var bytes = Encoding.UTF8.GetBytes(result);
            var fileName = $"model-costs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json";
            return Results.File(bytes, "application/json", fileName);
        }

        /// <summary>
        /// Imports model costs from CSV file
        /// </summary>
        /// <param name="file">CSV file containing model costs</param>
        /// <returns>Import result with statistics</returns>
        public async Task<IResult> ImportCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new ErrorResponseDto("No file provided for import"));
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ErrorResponseDto("File must be a CSV file"));
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
            return Results.Ok(result);
        }

        /// <summary>
        /// Imports model costs from JSON file
        /// </summary>
        /// <param name="file">JSON file containing model costs</param>
        /// <returns>Import result with statistics</returns>
        public async Task<IResult> ImportJson(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file provided for import");
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("File must be a JSON file");
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
            return Results.Ok(result);
        }

        /// <summary>
        /// Validates a pricing rules configuration JSON
        /// </summary>
        /// <param name="id">The ID of the model cost (used to retrieve associated model's parameter schema)</param>
        /// <param name="request">The pricing configuration to validate</param>
        /// <returns>Validation result with errors and warnings</returns>
        public async Task<IResult> ValidatePricingRules(
            int id,
            [FromBody] ValidatePricingRulesRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PricingConfiguration))
            {
                return Results.BadRequest(new ErrorResponseDto("Pricing configuration is required"));
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
            return Results.Ok(result);
        }

        /// <summary>
        /// Validates a pricing rules configuration JSON without a model cost context
        /// </summary>
        /// <param name="request">The pricing configuration to validate</param>
        /// <returns>Validation result with errors and warnings</returns>
        public async Task<IResult> ValidatePricingRulesStandalone(ValidatePricingRulesRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PricingConfiguration))
            {
                return Results.BadRequest(new ErrorResponseDto("Pricing configuration is required"));
            }

            await Task.CompletedTask;

            var result = _pricingRulesValidator.ValidateJson(
                request.PricingConfiguration,
                request.ParameterSchema);
            return Results.Ok(result);
        }

        private void LogAdminAudit(string operation, string entityType, object? entityId = null, string? detail = null) => AdminAudit.Log(_httpContextAccessor.HttpContext!, _logger, operation, entityType, entityId, detail);
        private void LogAdminAuditBulk(string operation, string entityType, int successCount, int failureCount) => AdminAudit.LogBulk(_httpContextAccessor.HttpContext!, _logger, operation, entityType, successCount, failureCount);
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
