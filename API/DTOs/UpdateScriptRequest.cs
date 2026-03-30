using System.Text.Json.Serialization;
using HostedService.Enums;

namespace API.DTOs
{
    public record UpdateScriptRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("scriptPath")] string ScriptPath,
        [property: JsonPropertyName("arguments")] string Arguments,
        [property: JsonPropertyName("tipo")] string Tipo);
}
