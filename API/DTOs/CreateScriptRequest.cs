using System.Text.Json.Serialization;
using HostedService.Enums;

namespace API.DTOs
{
    //public record CreateScriptRequest(
    //    [property: JsonPropertyName("name")] string Name,
    //    [property: JsonPropertyName("scriptPath")] string ScriptPath,
    //    [property: JsonPropertyName("arguments")] string? Arguments,
    //    [property: JsonPropertyName("tipo")] ScriptType Tipo);

    public record CreateScriptRequest(string Name, string ScriptPath, string Arguments, string Tipo);
}
