using System.Text.Json.Serialization;

namespace API.DTOs;

public record AsegurarOrigenPorRutaRequest(
    [property: JsonPropertyName("ruta")] string Ruta,
    [property: JsonPropertyName("filtrosExclusiones")] string? FiltrosExclusiones
);
