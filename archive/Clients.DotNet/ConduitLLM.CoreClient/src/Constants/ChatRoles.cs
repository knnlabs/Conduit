using ConduitLLM.CoreClient.Exceptions;

namespace ConduitLLM.CoreClient.Constants;

/// <summary>
/// Chat message role constants and validation utilities.
/// </summary>
public static class ChatRoles
{
    /// <summary>
    /// System role for context-setting messages.
    /// </summary>
    public const string System = "system";

    /// <summary>
    /// User role for user-generated messages.
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// Assistant role for AI-generated messages.
    /// </summary>
    public const string Assistant = "assistant";

    /// <summary>
    /// Tool role for tool call result messages.
    /// </summary>
    public const string Tool = "tool";

    /// <summary>
    /// All valid chat message roles.
    /// </summary>
    public static readonly string[] All = { System, User, Assistant, Tool };

    /// <summary>
    /// Validates if the specified role is a valid chat message role.
    /// </summary>
    /// <param name="role">The role to validate.</param>
    /// <returns>True if the role is valid; otherwise, false.</returns>
    public static bool IsValid(string role)
    {
        return All.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates if the specified role is a valid chat message role and throws an exception if not.
    /// </summary>
    /// <param name="role">The role to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <exception cref="ValidationException">Thrown when the role is invalid.</exception>
    public static void ValidateRole(string role, string parameterName = "role")
    {
        if (!IsValid(role))
        {
            throw new ValidationException($"Invalid message role: {role}. Valid roles are: {string.Join(", ", All)}", parameterName);
        }
    }

    /// <summary>
    /// Checks if the specified role requires a tool call ID.
    /// </summary>
    /// <param name="role">The role to check.</param>
    /// <returns>True if the role requires a tool call ID; otherwise, false.</returns>
    public static bool RequiresToolCallId(string role)
    {
        return string.Equals(role, Tool, StringComparison.OrdinalIgnoreCase);
    }
}