using System.Text.Json.Serialization;

namespace API.DTOs;

public record EjecutarTrabajoResponse(
    [property: JsonPropertyName("historialId")] int HistorialId,
    [property: JsonPropertyName("archivosCopiados")] int ArchivosCopiados,
    [property: JsonPropertyName("mensaje")] string Mensaje);
