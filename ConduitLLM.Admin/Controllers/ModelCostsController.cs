using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Extensions;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing model costs
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ModelCostsController : ControllerBase
    {
        private readonly IAdminModelCostService _modelCostService;
        private readonly ILogger<ModelCostsController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelCostsController
        /// </summary>
        /// <param name="modelCostService">The model cost service</param>
        /// <param name="logger">The logger</param>
        public ModelCostsController(
            IAdminModelCostService modelCostService,
            ILogger<ModelCostsController> logger)
        {
            _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model costs
        /// </summary>
        /// <returns>List of all model costs</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelCostDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllModelCosts()
        {
            try
            {
                var modelCosts = await _modelCostService.GetAllModelCostsAsync();
                return Ok(modelCosts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model costs");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
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
            try
            {
                var modelCost = await _modelCostService.GetModelCostByIdAsync(id);

                if (modelCost == null)
                {
                    return NotFound(new { error = "Model cost not found" });
                }

                return Ok(modelCost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets model costs by provider name
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>List of model costs for the specified provider</returns>
        [HttpGet("provider/{providerName}")]
        [ProducesResponseType(typeof(IEnumerable<ModelCostDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelCostsByProvider(string providerName)
        {
            try
            {
                var modelCosts = await _modelCostService.GetModelCostsByProviderAsync(providerName);
                return Ok(modelCosts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model costs for provider '{ProviderName}'", providerName.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets a model cost by pattern
        /// </summary>
        /// <param name="pattern">The model ID pattern</param>
        /// <returns>The model cost</returns>
        [HttpGet("pattern/{pattern}")]
        [ProducesResponseType(typeof(ModelCostDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelCostByPattern(string pattern)
        {
            try
            {
                var modelCost = await _modelCostService.GetModelCostByPatternAsync(pattern);

                if (modelCost == null)
                {
                    return NotFound(new { error = "Model cost not found" });
                }

                return Ok(modelCost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with pattern '{Pattern}'", pattern.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdModelCost = await _modelCostService.CreateModelCostAsync(modelCost);
                return CreatedAtAction(nameof(GetModelCostById), new { id = createdModelCost.Id }, createdModelCost);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when creating model cost");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Ensure ID in route matches ID in body
            if (id != modelCost.Id)
            {
                return BadRequest("ID in route must match ID in body");
            }

            try
            {
                var success = await _modelCostService.UpdateModelCostAsync(modelCost);

                if (!success)
                {
                    return NotFound(new { error = "Model cost not found" });
                }

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when updating model cost");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model cost with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
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
            try
            {
                var success = await _modelCostService.DeleteModelCostAsync(id);

                if (!success)
                {
                    return NotFound(new { error = "Model cost not found" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model cost with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
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

            try
            {
                var overview = await _modelCostService.GetModelCostOverviewAsync(startDate, endDate);
                return Ok(overview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost overview for period {StartDate} to {EndDate}",
                    startDate, endDate);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
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

            try
            {
                var importedCount = await _modelCostService.ImportModelCostsAsync(modelCosts);
                return Ok(importedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing model costs");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Exports model costs in CSV format
        /// </summary>
        /// <param name="provider">Optional provider name to filter by</param>
        /// <returns>CSV file containing model costs</returns>
        [HttpGet("export/csv")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportCsv([FromQuery] string? provider = null)
        {
            try
            {
                var csvData = await _modelCostService.ExportModelCostsAsync("csv", provider);
                var bytes = Encoding.UTF8.GetBytes(csvData);
                var fileName = $"model-costs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.csv";
                
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting model costs as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Exports model costs in JSON format
        /// </summary>
        /// <param name="provider">Optional provider name to filter by</param>
        /// <returns>JSON file containing model costs</returns>
        [HttpGet("export/json")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportJson([FromQuery] string? provider = null)
        {
            try
            {
                var jsonData = await _modelCostService.ExportModelCostsAsync("json", provider);
                var bytes = Encoding.UTF8.GetBytes(jsonData);
                var fileName = $"model-costs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json";
                
                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting model costs as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Imports model costs from CSV file
        /// </summary>
        /// <param name="file">CSV file containing model costs</param>
        /// <returns>Import result with statistics</returns>
        [HttpPost("import/csv")]
        [ProducesResponseType(typeof(BulkImportResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided for import");
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File must be a CSV file");
            }

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var csvData = await reader.ReadToEndAsync();

                var result = await _modelCostService.ImportModelCostsAsync(csvData, "csv");
                
                if (result.SuccessCount == 0 && result.FailureCount > 0)
                {
                    return BadRequest(new { 
                        message = "Import failed", 
                        errors = result.Errors,
                        successCount = result.SuccessCount,
                        failureCount = result.FailureCount 
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing model costs from CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Imports model costs from JSON file
        /// </summary>
        /// <param name="file">JSON file containing model costs</param>
        /// <returns>Import result with statistics</returns>
        [HttpPost("import/json")]
        [ProducesResponseType(typeof(BulkImportResult), StatusCodes.Status200OK)]
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

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var jsonData = await reader.ReadToEndAsync();

                var result = await _modelCostService.ImportModelCostsAsync(jsonData, "json");
                
                if (result.SuccessCount == 0 && result.FailureCount > 0)
                {
                    return BadRequest(new { 
                        message = "Import failed", 
                        errors = result.Errors,
                        successCount = result.SuccessCount,
                        failureCount = result.FailureCount 
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing model costs from JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
