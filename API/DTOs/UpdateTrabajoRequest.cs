using System.Text.Json.Serialization;

namespace API.DTOs;

public record UpdateTrabajoRequest(
    [property: JsonPropertyName("nombre")] string? Nombre,
    [property: JsonPropertyName("descripcion")] string? Descripcion,
    [property: JsonPropertyName("origenId")] int? OrigenId,
    [property: JsonPropertyName("destinoId")] int? DestinoId,
    [property: JsonPropertyName("cronExpression")] string? CronExpression,
    [property: JsonPropertyName("activo")] bool? Activo,
    [property: JsonPropertyName("procesando")] bool? Procesando,
    [property: JsonPropertyName("estatusPrevio")] string? EstatusPrevio
);
