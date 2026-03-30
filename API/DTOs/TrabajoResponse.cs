using System.Text.Json.Serialization;

namespace API.DTOs;

public record TrabajoResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("descripcion")] string Descripcion,
    [property: JsonPropertyName("trabajosOrigenDestinoId")] int TrabajosOrigenDestinoId,
    [property: JsonPropertyName("origenId")] int OrigenId,
    [property: JsonPropertyName("destinoId")] int DestinoId,
    [property: JsonPropertyName("cronExpression")] string CronExpression,
    [property: JsonPropertyName("activo")] bool Activo,
    [property: JsonPropertyName("procesando")] bool Procesando,
    [property: JsonPropertyName("estatusPrevio")] string EstatusPrevio,
    [property: JsonPropertyName("fechaCreacion")] DateTime FechaCreacion,
    [property: JsonPropertyName("fechaModificacion")] DateTime FechaModificacion
);
