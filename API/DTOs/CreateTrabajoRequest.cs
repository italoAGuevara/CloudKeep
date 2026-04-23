using System.Text.Json.Serialization;

namespace API.DTOs;

public record CreateTrabajoRequest(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("descripcion")] string Descripcion,
    [property: JsonPropertyName("origenId")] int OrigenId,
    [property: JsonPropertyName("destinoId")] int DestinoId,
    [property: JsonPropertyName("scriptPreId")] int? ScriptPreId,
    [property: JsonPropertyName("scriptPostId")] int? ScriptPostId,
    [property: JsonPropertyName("preDetenerEnFallo")] bool? PreDetenerEnFallo,
    [property: JsonPropertyName("postDetenerEnFallo")] bool? PostDetenerEnFallo,
    [property: JsonPropertyName("cronExpression")] string CronExpression,
    [property: JsonPropertyName("activo")] bool? Activo,
    [property: JsonPropertyName("copiaTamanoMinBytes")] long? CopiaTamanoMinBytes = null,
    [property: JsonPropertyName("copiaTamanoMaxBytes")] long? CopiaTamanoMaxBytes = null,
    [property: JsonPropertyName("copiaCreacionDesdeUtc")] DateTime? CopiaCreacionDesdeUtc = null,
    [property: JsonPropertyName("copiaCreacionHastaUtc")] DateTime? CopiaCreacionHastaUtc = null,
    [property: JsonPropertyName("copiaActualizacionDesdeUtc")] DateTime? CopiaActualizacionDesdeUtc = null,
    [property: JsonPropertyName("copiaActualizacionHastaUtc")] DateTime? CopiaActualizacionHastaUtc = null
);
