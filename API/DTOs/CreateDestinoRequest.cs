using System.Text.Json.Serialization;

namespace API.DTOs;

public record CreateDestinoRequest(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("idCarpeta")] string? IdCarpeta = null,
    [property: JsonPropertyName("bucketName")] string? BucketName = null,
    [property: JsonPropertyName("region")] string? Region = null,
    [property: JsonPropertyName("accessKeyId")] string? AccessKeyId = null,
    [property: JsonPropertyName("secretAccessKey")] string? SecretAccessKey = null,
    [property: JsonPropertyName("serviceAccountEmail")] string? ServiceAccountEmail = null,
    [property: JsonPropertyName("privateKey")] string? PrivateKey = null
);
