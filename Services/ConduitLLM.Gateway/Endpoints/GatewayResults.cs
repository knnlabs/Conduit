using ConduitLLM.Core.Models;
using System.Text.Json;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>OpenAI-compatible explicit results shared by Gateway endpoint groups.</summary>
public static class GatewayResults
{
    public static IResult OpenAIError(
        int statusCode,
        string message,
        string code,
        string type = "invalid_request_error",
        string? param = null,
        JsonElement? metadata = null) =>
        Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = message,
                Type = type,
                Code = code,
                Param = param,
                Metadata = metadata
            }
        }, statusCode: statusCode);
}
