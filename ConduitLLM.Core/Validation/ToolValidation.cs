using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Validation;

/// <summary>
/// Provides validation logic for tool and function calling related models.
/// </summary>
public static class ToolValidation
{
    /// <summary>
    /// Validates a list of tools for correctness.
    /// </summary>
    /// <param name="tools">The list of tools to validate.</param>
    /// <exception cref="ValidationException">Thrown when validation fails.</exception>
    public static void ValidateTools(IEnumerable<Tool>? tools)
    {
        if (tools == null)
        {
            return;
        }

        foreach (var tool in tools)
        {
            if (string.IsNullOrEmpty(tool.Type))
            {
                throw new ValidationException("Tool type cannot be null or empty.");
            }

            if (tool.Type != "function")
            {
                throw new ValidationException($"Tool type '{tool.Type}' is not supported. Currently only 'function' is supported.");
            }

            ValidateFunctionDefinition(tool.Function);
        }
    }

    /// <summary>
    /// Validates a function definition for correctness.
    /// </summary>
    /// <param name="functionDefinition">The function definition to validate.</param>
    /// <exception cref="ValidationException">Thrown when validation fails.</exception>
    public static void ValidateFunctionDefinition(FunctionDefinition functionDefinition)
    {
        if (functionDefinition == null)
        {
            throw new ValidationException("Function definition cannot be null.");
        }

        if (string.IsNullOrEmpty(functionDefinition.Name))
        {
            throw new ValidationException("Function name cannot be null or empty.");
        }

        // Validate function name format
        if (!IsValidFunctionName(functionDefinition.Name))
        {
            throw new ValidationException("Function name must contain only a-z, A-Z, 0-9, underscores and dashes, with a maximum length of 64 characters.");
        }

        // Optionally validate the parameters JSON schema if present
        if (functionDefinition.Parameters != null)
        {
            try
            {
                // Basic schema validation could be added here
                if (functionDefinition.Parameters["type"] == null)
                {
                    throw new ValidationException("Function parameters schema must include a 'type' field.");
                }
            }
            catch (Exception ex) when (!(ex is ValidationException))
            {
                throw new ValidationException($"Invalid function parameters JSON schema: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Validates tool calls for correctness.
    /// </summary>
    /// <param name="toolCalls">The tool calls to validate.</param>
    /// <exception cref="ValidationException">Thrown when validation fails.</exception>
    public static void ValidateToolCalls(IEnumerable<ToolCall>? toolCalls)
    {
        if (toolCalls == null)
        {
            return;
        }

        foreach (var toolCall in toolCalls)
        {
            if (string.IsNullOrEmpty(toolCall.Id))
            {
                throw new ValidationException("Tool call ID cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(toolCall.Type))
            {
                throw new ValidationException("Tool call type cannot be null or empty.");
            }

            if (toolCall.Type != "function")
            {
                throw new ValidationException($"Tool call type '{toolCall.Type}' is not supported. Currently only 'function' is supported.");
            }

            ValidateFunctionCall(toolCall.Function);
        }
    }

    /// <summary>
    /// Validates a function call for correctness.
    /// </summary>
    /// <param name="functionCall">The function call to validate.</param>
    /// <exception cref="ValidationException">Thrown when validation fails.</exception>
    public static void ValidateFunctionCall(FunctionCall functionCall)
    {
        if (functionCall == null)
        {
            throw new ValidationException("Function call cannot be null.");
        }

        if (string.IsNullOrEmpty(functionCall.Name))
        {
            throw new ValidationException("Function call name cannot be null or empty.");
        }

        if (string.IsNullOrEmpty(functionCall.Arguments))
        {
            throw new ValidationException("Function call arguments cannot be null or empty.");
        }

        // Validate that arguments are valid JSON
        try
        {
            JsonDocument.Parse(functionCall.Arguments);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"Function call arguments must be valid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the function name follows the required format.
    /// </summary>
    /// <param name="name">The function name to check.</param>
    /// <returns>True if the name is valid, false otherwise.</returns>
    private static bool IsValidFunctionName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 64)
        {
            return false;
        }

        // Only allow a-z, A-Z, 0-9, underscores and dashes
        return name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }
}
