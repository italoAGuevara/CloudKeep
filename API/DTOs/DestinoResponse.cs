using System.Text.Json.Serialization;

namespace API.DTOs;

public record DestinoResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("credencialesConfiguradas")] bool CredencialesConfiguradas,
    [property: JsonPropertyName("fechaCreacion")] DateTime FechaCreacion,
    [property: JsonPropertyName("fechaModificacion")] DateTime FechaModificacion
);
