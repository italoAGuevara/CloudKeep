using System.Text.Json.Serialization;

namespace API.DTOs;

public record UpdateDestinoRequest(
    [property: JsonPropertyName("nombre")] string? Nombre = null,
    [property: JsonPropertyName("tipo")] string? Tipo = null,
    [property: JsonPropertyName("idCarpeta")] string? IdCarpeta = null,
    [property: JsonPropertyName("bucketName")] string? BucketName = null,
    [property: JsonPropertyName("region")] string? Region = null,
    [property: JsonPropertyName("accessKeyId")] string? AccessKeyId = null,
    [property: JsonPropertyName("secretAccessKey")] string? SecretAccessKey = null,
    [property: JsonPropertyName("serviceAccountEmail")] string? ServiceAccountEmail = null,
    [property: JsonPropertyName("privateKey")] string? PrivateKey = null
);
