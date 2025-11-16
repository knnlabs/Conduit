using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing global settings
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class GlobalSettingsController : ControllerBase
    {
        private readonly IAdminGlobalSettingService _globalSettingService;
        private readonly ILogger<GlobalSettingsController> _logger;

        /// <summary>
        /// Initializes a new instance of the GlobalSettingsController
        /// </summary>
        /// <param name="globalSettingService">The global setting service</param>
        /// <param name="logger">The logger</param>
        public GlobalSettingsController(
            IAdminGlobalSettingService globalSettingService,
            ILogger<GlobalSettingsController> logger)
        {
            _globalSettingService = globalSettingService ?? throw new ArgumentNullException(nameof(globalSettingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all global settings
        /// </summary>
        /// <returns>List of all global settings</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<GlobalSettingDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllSettings()
        {
            try
            {
                var settings = await _globalSettingService.GetAllSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all global settings");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets a global setting by ID
        /// </summary>
        /// <param name="id">The ID of the setting to get</param>
        /// <returns>The global setting</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSettingById(int id)
        {
            try
            {
                var setting = await _globalSettingService.GetSettingByIdAsync(id);

                if (setting == null)
                {
                    return NotFound(new ErrorResponseDto("Global setting not found"));
                }

                return Ok(setting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global setting with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Gets a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to get</param>
        /// <returns>The global setting</returns>
        [HttpGet("by-key/{key}")]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSettingByKey(string key)
        {
            try
            {
                var setting = await _globalSettingService.GetSettingByKeyAsync(key);

                if (setting == null)
                {
                    return NotFound(new ErrorResponseDto("Global setting not found"));
                }

                return Ok(setting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global setting with key {Key}", key.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Creates a new global setting
        /// </summary>
        /// <param name="setting">The setting to create</param>
        /// <returns>The created setting</returns>
        [HttpPost]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateSetting([FromBody] CreateGlobalSettingDto setting)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdSetting = await _globalSettingService.CreateSettingAsync(setting);
                return CreatedAtAction(nameof(GetSettingById), new { id = createdSetting.Id }, createdSetting);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when creating global setting");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating global setting");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Updates an existing global setting
        /// </summary>
        /// <param name="id">The ID of the setting to update</param>
        /// <param name="setting">The updated setting data</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateSetting(int id, [FromBody] UpdateGlobalSettingDto setting)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Ensure ID in route matches ID in body
            if (id != setting.Id)
            {
                return BadRequest("ID in route must match ID in body");
            }

            try
            {
                var success = await _globalSettingService.UpdateSettingAsync(setting);

                if (!success)
                {
                    return NotFound(new ErrorResponseDto("Global setting not found"));
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global setting with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Updates or creates a global setting by key
        /// </summary>
        /// <param name="setting">The setting data with key, value, and optional description</param>
        /// <returns>No content if successful</returns>
        [HttpPut("by-key")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateSettingByKey([FromBody] UpdateGlobalSettingByKeyDto setting)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _globalSettingService.UpdateSettingByKeyAsync(setting);

                if (!success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update or create global setting");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global setting with key {Key}", setting.Key.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Deletes a global setting
        /// </summary>
        /// <param name="id">The ID of the setting to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteSetting(int id)
        {
            try
            {
                var success = await _globalSettingService.DeleteSettingAsync(id);

                if (!success)
                {
                    return NotFound(new ErrorResponseDto("Global setting not found"));
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Deletes a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("by-key/{key}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteSettingByKey(string key)
        {
            try
            {
                var success = await _globalSettingService.DeleteSettingByKeyAsync(key);

                if (!success)
                {
                    return NotFound(new ErrorResponseDto("Global setting not found"));
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with key {Key}", key.Replace(Environment.NewLine, ""));
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
