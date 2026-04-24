using System.Text.Json.Serialization;

namespace API.DTOs;

public sealed record EjecucionHistorialItemResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("trabajoId")] int TrabajoId,
    [property: JsonPropertyName("trabajoNombre")] string TrabajoNombre,
    [property: JsonPropertyName("startTime")] DateTime StartTime,
    [property: JsonPropertyName("endTime")] DateTime EndTime,
    [property: JsonPropertyName("duracionSegundos")] double? DuracionSegundos,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("archivosCopiados")] int? ArchivosCopiados,
    [property: JsonPropertyName("disparo")] string Disparo,
    [property: JsonPropertyName("ejecutadoPor")] string EjecutadoPor,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage);

public sealed record EjecucionHistorialListResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<EjecucionHistorialItemResponse> Items,
    [property: JsonPropertyName("total")] int Total);
