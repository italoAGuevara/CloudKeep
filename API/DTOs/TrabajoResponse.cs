using System.Text.Json.Serialization;

namespace API.DTOs;

public record TrabajoResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("descripcion")] string Descripcion,
    [property: JsonPropertyName("trabajosOrigenDestinoId")] int TrabajosOrigenDestinoId,
    [property: JsonPropertyName("origenId")] int OrigenId,
    [property: JsonPropertyName("destinoId")] int DestinoId,
    [property: JsonPropertyName("trabajosScriptsId")] int TrabajosScriptsId,
    [property: JsonPropertyName("scriptPreId")] int? ScriptPreId,
    [property: JsonPropertyName("scriptPostId")] int? ScriptPostId,
    [property: JsonPropertyName("preDetenerEnFallo")] bool PreDetenerEnFallo,
    [property: JsonPropertyName("postDetenerEnFallo")] bool PostDetenerEnFallo,
    [property: JsonPropertyName("cronExpression")] string CronExpression,
    [property: JsonPropertyName("activo")] bool Activo,
    [property: JsonPropertyName("procesando")] bool Procesando,
    [property: JsonPropertyName("estatusPrevio")] string EstatusPrevio,
    [property: JsonPropertyName("fechaCreacion")] DateTime FechaCreacion,
    [property: JsonPropertyName("fechaModificacion")] DateTime FechaModificacion,
    [property: JsonPropertyName("copiaTamanoMinBytes")] long? CopiaTamanoMinBytes,
    [property: JsonPropertyName("copiaTamanoMaxBytes")] long? CopiaTamanoMaxBytes,
    [property: JsonPropertyName("copiaCreacionDesdeUtc")] DateTime? CopiaCreacionDesdeUtc,
    [property: JsonPropertyName("copiaCreacionHastaUtc")] DateTime? CopiaCreacionHastaUtc,
    [property: JsonPropertyName("copiaActualizacionDesdeUtc")] DateTime? CopiaActualizacionDesdeUtc,
    [property: JsonPropertyName("copiaActualizacionHastaUtc")] DateTime? CopiaActualizacionHastaUtc
);
