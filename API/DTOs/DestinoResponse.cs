using System.Text.Json.Serialization;

namespace API.DTOs;

public record DestinoResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("idCarpeta")] string IdCarpeta,
    [property: JsonPropertyName("accessKeyId")] string AccessKeyId,
    [property: JsonPropertyName("secretAccessKeyConfigurada")] bool SecretAccessKeyConfigurada,
    [property: JsonPropertyName("bucketName")] string BucketName,
    [property: JsonPropertyName("region")] string Region,
    [property: JsonPropertyName("serviceAccountEmail")] string ServiceAccountEmail,
    [property: JsonPropertyName("privateKeyConfigurada")] bool PrivateKeyConfigurada,
    [property: JsonPropertyName("fechaCreacion")] DateTime FechaCreacion,
    [property: JsonPropertyName("fechaModificacion")] DateTime FechaModificacion
);
