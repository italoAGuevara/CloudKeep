using System.Text.Json.Serialization;

namespace API.DTOs;

public sealed record ScriptExecutionTimeoutResponse(
    [property: JsonPropertyName("scriptExecutionTimeoutMinutes")] int ScriptExecutionTimeoutMinutes);

public sealed record UpdateScriptExecutionTimeoutRequest(
    [property: JsonPropertyName("scriptExecutionTimeoutMinutes")] int ScriptExecutionTimeoutMinutes);
