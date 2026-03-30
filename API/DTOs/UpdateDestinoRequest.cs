using System.Text.Json.Serialization;

namespace API.DTOs;

public record UpdateDestinoRequest(
    [property: JsonPropertyName("nombre")] string? Nombre,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("credenciales")] string? Credenciales
);
