using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using API.Audit;
using API.DTOs;
using API.Exceptions;
using API.Services.Interfaces;
using HostedService;
using HostedService.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using File = Google.Apis.Drive.v3.Data.File;

namespace API.Services.Services;

public class DestinoService : IDestinoService
{
    private readonly AppDbContext _context;
    private readonly IDestinoCredentialProtector _credentialProtector;
    private readonly ILogAccionesUsuarioWriter _logAcciones;

    public DestinoService(
        AppDbContext context,
        IDestinoCredentialProtector credentialProtector,
        ILogAccionesUsuarioWriter logAcciones)
    {
        _context = context;
        _credentialProtector = credentialProtector;
        _logAcciones = logAcciones;
    }

    public async Task<IEnumerable<DestinoResponse>> GetAll()
    {
        var list = await _context.Destinos
            .AsNoTracking()
            .OrderBy(d => d.Nombre)
            .ToListAsync();
        return list.Select(MapToResponse).OrderBy(x => x.Id);
    }

    public async Task<DestinoResponse?> GetById(int id)
    {
        var entity = await _context.Destinos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

        if (entity is null)
            throw new NotFoundException($"Destino con Id '{id}' no existe");

        return MapToResponse(entity);
    }

    public async Task<DestinoResponse> Create(CreateDestinoRequest request)
    {
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        var tipo = NormalizeAndValidateTipo(request.Tipo);

        var nombre = request.Nombre.Trim();
        await EnsureNombreUnicoAsync(nombre, excludeId: null);

        var idCarpeta = tipo == DestinoTipos.GoogleDrive
            ? ValidateIdCarpetaGoogleDrive(request.IdCarpeta)
            : string.Empty;

        var blobPlain = "{}";
        var accessKeyId = string.Empty;
        var secretEnc = string.Empty;
        var bucketName = string.Empty;
        var s3Region = string.Empty;
        var googleServiceEmail = string.Empty;
        var googlePrivateEnc = string.Empty;
        var azureContainer = string.Empty;
        var azureConnEnc = string.Empty;
        var carpetaDestino = string.Empty;

        if (tipo == DestinoTipos.S3)
        {
            var ak = request.AccessKeyId?.Trim() ?? string.Empty;
            var hasKeys = !string.IsNullOrWhiteSpace(ak);
            if (hasKeys)
            {
                if (string.IsNullOrWhiteSpace(request.SecretAccessKey))
                    throw new BadRequestException("secretAccessKey es obligatorio para S3 con claves de acceso.");
                if (string.IsNullOrWhiteSpace(request.BucketName) || string.IsNullOrWhiteSpace(request.Region))
                    throw new BadRequestException("bucketName y region son obligatorios para S3 con claves de acceso.");
                accessKeyId = ak;
                secretEnc = _credentialProtector.Protect(request.SecretAccessKey.Trim());
                bucketName = request.BucketName.Trim();
                s3Region = request.Region.Trim();
                blobPlain = BuildS3KeysCredencialesJson(accessKeyId);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.BucketName) || string.IsNullOrWhiteSpace(request.Region))
                    throw new BadRequestException("bucketName y region son obligatorios para S3 con IAM (Bucket y región).");
                bucketName = request.BucketName.Trim();
                s3Region = request.Region.Trim();
            }

            carpetaDestino = CloudCarpetaDestino.NormalizePrefijo(request.CarpetaDestino);
            if (string.IsNullOrEmpty(carpetaDestino))
                throw new BadRequestException("carpetaDestino es obligatoria para Amazon S3.");
        }
        else if (tipo == DestinoTipos.GoogleDrive)
        {
            if (string.IsNullOrWhiteSpace(request.ServiceAccountEmail))
                throw new BadRequestException("serviceAccountEmail es obligatorio para Google Drive.");
            if (string.IsNullOrWhiteSpace(request.PrivateKey))
                throw new BadRequestException("privateKey es obligatorio para Google Drive.");
            await ValidarGoogleDriveCredencialesAsync(
                idCarpeta,
                request.ServiceAccountEmail.Trim(),
                request.PrivateKey,
                CancellationToken.None);
            googleServiceEmail = request.ServiceAccountEmail.Trim();
            googlePrivateEnc = _credentialProtector.Protect(request.PrivateKey.Trim());
        }
        else if (tipo == DestinoTipos.AzureBlob)
        {
            if (string.IsNullOrWhiteSpace(request.AzureBlobContainerName))
                throw new BadRequestException("azureBlobContainerName es obligatorio para Azure Blob Storage.");
            if (string.IsNullOrWhiteSpace(request.AzureBlobConnectionString))
                throw new BadRequestException("azureBlobConnectionString es obligatorio para Azure Blob Storage.");
            var ac = request.AzureBlobContainerName.Trim();
            var aconn = request.AzureBlobConnectionString.Trim();
            await ValidarAzureBlobCredencialesAsync(aconn, ac, CancellationToken.None);
            azureContainer = ac;
            azureConnEnc = _credentialProtector.Protect(aconn);
            carpetaDestino = CloudCarpetaDestino.NormalizePrefijo(request.CarpetaDestino);
            if (string.IsNullOrEmpty(carpetaDestino))
                throw new BadRequestException("carpetaDestino es obligatoria para Azure Blob Storage.");
        }

        var entity = new Destino
        {
            Nombre = nombre,
            TipoDeDestino = tipo,
            Credenciales = _credentialProtector.Protect(blobPlain),
            IdCarpeta = idCarpeta,
            AccessKeyId = accessKeyId,
            SecretAccessKey = secretEnc,
            BucketName = bucketName,
            S3Region = s3Region,
            GoogleServiceAccountEmail = googleServiceEmail,
            GooglePrivateKey = googlePrivateEnc,
            AzureBlobContainerName = azureContainer,
            AzureBlobConnectionString = azureConnEnc,
            CarpetaDestino = carpetaDestino
        };
        _context.Destinos.Add(entity);
        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Destino, AccionLog.Create, null, SnapshotDestino(entity));
        return MapToResponse(entity);
    }

    public async Task<DestinoResponse?> Update(int id, UpdateDestinoRequest request)
    {
        var entity = await _context.Destinos.FirstOrDefaultAsync(d => d.Id == id);
        if (entity is null) return null;

        var antes = SnapshotDestino(entity);

        if (request.Nombre is not null)
        {
            ValidateRequired(request.Nombre, nameof(request.Nombre));
            var nombre = request.Nombre.Trim();
            await EnsureNombreUnicoAsync(nombre, excludeId: id);
            entity.Nombre = nombre;
        }

        if (request.Tipo is not null)
            entity.TipoDeDestino = NormalizeAndValidateTipo(request.Tipo);

        if (HasS3CredentialPatch(request) && entity.TipoDeDestino == DestinoTipos.S3)
            ApplyS3CredentialsFromPatch(entity, request);
        else if (HasGoogleCredentialPatch(request) && entity.TipoDeDestino == DestinoTipos.GoogleDrive)
            ApplyGoogleCredentialsFromPatch(entity, request);
        else if (HasAzureCredentialPatch(request) && entity.TipoDeDestino == DestinoTipos.AzureBlob)
            ApplyAzureCredentialsFromPatch(entity, request);

        if (request.IdCarpeta is not null)
            entity.IdCarpeta = request.IdCarpeta.Trim();

        if (request.CarpetaDestino is not null
            && (entity.TipoDeDestino == DestinoTipos.S3 || entity.TipoDeDestino == DestinoTipos.AzureBlob))
        {
            var p = CloudCarpetaDestino.NormalizePrefijo(request.CarpetaDestino);
            if (string.IsNullOrEmpty(p))
                throw new BadRequestException("carpetaDestino no puede estar vacía para S3 ni Azure Blob Storage.");
            entity.CarpetaDestino = p;
        }

        if (entity.TipoDeDestino == DestinoTipos.S3)
        {
            entity.IdCarpeta = string.Empty;
            entity.GoogleServiceAccountEmail = string.Empty;
            entity.GooglePrivateKey = string.Empty;
            entity.AzureBlobContainerName = string.Empty;
            entity.AzureBlobConnectionString = string.Empty;
            if (string.IsNullOrWhiteSpace(entity.CarpetaDestino))
                throw new BadRequestException("carpetaDestino es obligatoria para Amazon S3.");
        }
        else if (entity.TipoDeDestino == DestinoTipos.GoogleDrive)
        {
            entity.AccessKeyId = string.Empty;
            entity.SecretAccessKey = string.Empty;
            entity.BucketName = string.Empty;
            entity.S3Region = string.Empty;
            entity.AzureBlobContainerName = string.Empty;
            entity.AzureBlobConnectionString = string.Empty;
            entity.CarpetaDestino = string.Empty;
            if (string.IsNullOrWhiteSpace(entity.IdCarpeta))
                throw new BadRequestException("idCarpeta es obligatorio para Google Drive.");
        }
        else if (entity.TipoDeDestino == DestinoTipos.AzureBlob)
        {
            entity.IdCarpeta = string.Empty;
            entity.AccessKeyId = string.Empty;
            entity.SecretAccessKey = string.Empty;
            entity.BucketName = string.Empty;
            entity.S3Region = string.Empty;
            entity.GoogleServiceAccountEmail = string.Empty;
            entity.GooglePrivateKey = string.Empty;
            if (string.IsNullOrWhiteSpace(entity.AzureBlobContainerName))
                throw new BadRequestException("azureBlobContainerName es obligatorio para Azure Blob Storage.");
            if (string.IsNullOrWhiteSpace(entity.CarpetaDestino))
                throw new BadRequestException("carpetaDestino es obligatoria para Azure Blob Storage.");
            var azureConn = GetAzureConnectionStringPlaintextOrThrow(entity);
            await ValidarAzureBlobCredencialesAsync(
                azureConn,
                entity.AzureBlobContainerName.Trim(),
                CancellationToken.None);
        }

        if (entity.TipoDeDestino == DestinoTipos.GoogleDrive)
        {
            if (string.IsNullOrWhiteSpace(entity.GoogleServiceAccountEmail))
                throw new BadRequestException("serviceAccountEmail es obligatorio para Google Drive.");
            var plainKey = GetGooglePrivateKeyPlaintextOrThrow(entity);
            await ValidarGoogleDriveCredencialesAsync(
                entity.IdCarpeta,
                entity.GoogleServiceAccountEmail,
                plainKey,
                CancellationToken.None);
        }

        entity.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Destino, AccionLog.Update, antes, SnapshotDestino(entity));
        return MapToResponse(entity);
    }

    public async Task<bool> Delete(int id)
    {
        var entity = await _context.Destinos.FirstOrDefaultAsync(d => d.Id == id);
        if (entity is null) return false;

        var enUso = await _context.Trabajos.AnyAsync(t => t.TrabajosOrigenDestino.DestinoId == id);
        if (enUso)
            throw new ConflictException("El destino está asociado a uno o más trabajos.");

        var vinculosHuerfanos = await _context.TrabajosOrigenDestinos
            .Where(l => l.DestinoId == id)
            .ToListAsync();
        if (vinculosHuerfanos.Count > 0)
        {
            _context.TrabajosOrigenDestinos.RemoveRange(vinculosHuerfanos);
            await _context.SaveChangesAsync();
        }

        var antes = SnapshotDestino(entity);
        _context.Destinos.Remove(entity);
        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Destino, AccionLog.Delete, antes, null);
        return true;
    }

    public async Task<GoogleDriveValidacionResponse> ValidarConexionGoogleDriveAsync(
        ValidarGoogleDriveRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.IdCarpeta, nameof(request.IdCarpeta));
        ValidateRequired(request.ServiceAccountEmail, nameof(request.ServiceAccountEmail));
        ValidateRequired(request.PrivateKey, nameof(request.PrivateKey));
        var idCarpeta = ValidateIdCarpetaGoogleDrive(request.IdCarpeta);
        return await ValidarGoogleDriveCredencialesAsync(
            idCarpeta,
            request.ServiceAccountEmail.Trim(),
            request.PrivateKey,
            cancellationToken);
    }

    public async Task<S3ValidacionResponse> ValidarConexionS3Async(
        ValidarS3Request request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BucketName))
            throw new BadRequestException("bucketName es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Region))
            throw new BadRequestException("region es obligatoria.");

        var bucket = request.BucketName.Trim();
        var regionName = request.Region.Trim();
        var ak = request.AccessKeyId?.Trim() ?? string.Empty;
        var skRaw = request.SecretAccessKey ?? string.Empty;
        var sk = skRaw.Trim();
        var hasAk = !string.IsNullOrWhiteSpace(ak);
        var hasSk = !string.IsNullOrWhiteSpace(skRaw);
        if (hasAk != hasSk)
            throw new BadRequestException("Para validar con claves de acceso, envía accessKeyId y secretAccessKey juntos.");

        RegionEndpoint regionEndpoint;
        try
        {
            regionEndpoint = RegionEndpoint.GetBySystemName(regionName);
        }
        catch (ArgumentException)
        {
            throw new BadRequestException($"Región AWS no reconocida: «{regionName}».");
        }

        var config = new AmazonS3Config { RegionEndpoint = regionEndpoint };
        AWSCredentials? explicitCreds = hasAk ? new BasicAWSCredentials(ak, sk) : null;
        using var s3Client = explicitCreds != null
            ? new AmazonS3Client(explicitCreds, config)
            : new AmazonS3Client(config);

        try
        {
            await s3Client.HeadBucketAsync(new HeadBucketRequest { BucketName = bucket }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
        {
            throw new BadRequestException(DescribeS3HeadBucketFailure(ex));
        }
        catch (AmazonClientException ex)
        {
            throw new BadRequestException(
                $"No se pudieron resolver credenciales de AWS en el servidor o la solicitud falló: {ex.Message}");
        }

        var identityArn = await TryGetCallerIdentityArnAsync(explicitCreds, regionEndpoint, cancellationToken)
            .ConfigureAwait(false);
        var mensaje = identityArn is { Length: > 0 }
            ? $"Conexión correcta con el bucket «{bucket}» (región {regionName}). Identidad: {identityArn}."
            : $"Conexión correcta con el bucket «{bucket}» (región {regionName}).";

        return new S3ValidacionResponse(mensaje, bucket, identityArn);
    }

    public async Task<AzureBlobValidacionResponse> ValidarConexionAzureBlobAsync(
        ValidarAzureBlobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AzureBlobContainerName))
            throw new BadRequestException("azureBlobContainerName es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.AzureBlobConnectionString))
            throw new BadRequestException("azureBlobConnectionString es obligatorio.");
        var container = request.AzureBlobContainerName.Trim();
        var conn = request.AzureBlobConnectionString.Trim();
        return await ValidarAzureBlobCredencialesAsync(conn, container, cancellationToken);
    }

    private static async Task<AzureBlobValidacionResponse> ValidarAzureBlobCredencialesAsync(
        string connectionString,
        string containerName,
        CancellationToken cancellationToken)
    {
        BlobServiceClient service;
        try
        {
            service = new BlobServiceClient(connectionString);
        }
        catch (Exception ex)
        {
            throw new BadRequestException($"Cadena de conexión de Azure no válida: {ex.Message}");
        }

        var container = service.GetBlobContainerClient(containerName);
        try
        {
            await container.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            throw new BadRequestException(DescribeAzureBlobFailure(ex));
        }

        return new AzureBlobValidacionResponse(
            $"Conexión correcta con el contenedor «{containerName}».",
            containerName);
    }

    private static string DescribeAzureBlobFailure(RequestFailedException ex)
    {
        if (ex.Status == 403)
            return "Acceso denegado al contenedor. Revisa la cadena de conexión y permisos (p. ej. rol de colaborador de datos de almacenamiento o SAS con lectura).";
        if (ex.Status == 404)
            return "El contenedor no existe o el nombre es incorrecto.";
        if (ex.ErrorCode is { Length: > 0 } code)
            return $"Azure Storage respondió ({code}): {ex.Message}";
        return $"Azure Storage respondió: {ex.Message}";
    }

    private static async Task<string?> TryGetCallerIdentityArnAsync(
        AWSCredentials? credentials,
        RegionEndpoint regionEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            using var sts = credentials != null
                ? new AmazonSecurityTokenServiceClient(credentials, regionEndpoint)
                : new AmazonSecurityTokenServiceClient(regionEndpoint);
            var resp = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest(), cancellationToken)
                .ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(resp.Arn) ? null : resp.Arn;
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeS3HeadBucketFailure(AmazonS3Exception ex)
    {
        var code = ex.ErrorCode;
        if (string.Equals(code, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
            return "El bucket no existe o el nombre es incorrecto.";
        if (string.Equals(code, "AccessDenied", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "Forbidden", StringComparison.OrdinalIgnoreCase)
            || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return "Acceso denegado al bucket. Revisa permisos IAM (p. ej. s3:ListBucket) y que el bucket sea el correcto.";
        if (string.Equals(code, "InvalidAccessKeyId", StringComparison.OrdinalIgnoreCase))
            return "Access Key ID no válido.";
        if (string.Equals(code, "SignatureDoesNotMatch", StringComparison.OrdinalIgnoreCase))
            return "Secret Access Key incorrecta.";
        if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            return "El bucket no existe o el nombre es incorrecto.";

        var detail = !string.IsNullOrWhiteSpace(code) ? code : ex.StatusCode.ToString();
        return $"AWS S3 respondió ({detail}): {ex.Message}";
    }

    /// <summary>Comprueba credenciales y que <paramref name="idCarpeta"/> sea una carpeta accesible.</summary>
    private async Task<GoogleDriveValidacionResponse> ValidarGoogleDriveCredencialesAsync(
        string idCarpeta,
        string serviceAccountEmail,
        string privateKeyOrJson,
        CancellationToken cancellationToken)
    {
        var folderId = idCarpeta.Trim();
        var email = serviceAccountEmail.Trim();
        var pk = NormalizeGooglePrivateKeyInput(privateKeyOrJson);
        if (string.IsNullOrWhiteSpace(pk))
            throw new BadRequestException("privateKey es obligatorio.");
                
        var serviceAccountJson = BuildMinimalServiceAccountJson(email, pk);
        GoogleCredential credential;
        try
        {
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(serviceAccountJson));
            credential = await GoogleCredential.FromStreamAsync(ms, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new BadRequestException($"Credenciales de cuenta de servicio no válidas: {ex.Message}");
        }

        credential = credential.CreateScoped(DriveService.Scope.DriveReadonly);

        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "portafolio-v-1-2",
        });

        File meta;
        try
        {
            var req = service.Files.Get(folderId);
            req.Fields = "id,name,mimeType";
            req.SupportsAllDrives = true;
            meta = await req.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex)
        {
            var detail = ex.Error?.Message ?? ex.Message;
            throw new BadRequestException(
                $"Google Drive rechazó la operación. Comprueba el ID de carpeta, que la carpeta esté compartida con la cuenta de servicio y el detalle: {detail}");
        }

        if (!string.Equals(meta.MimeType, "application/vnd.google-apps.folder", StringComparison.Ordinal))
            throw new BadRequestException("El idCarpeta no corresponde a una carpeta de Google Drive.");

        var nombre = meta.Name ?? folderId;
        return new GoogleDriveValidacionResponse(
            $"Conexión correcta. Carpeta: «{nombre}».",
            nombre);
    }

    private static string NormalizeGooglePrivateKeyInput(string key)
    {
        var k = key.Trim();
        if (!k.StartsWith("{", StringComparison.Ordinal))
        {
            return k.Replace("\\r\\n", "\n", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal);
        }

        try
        {
            using var doc = JsonDocument.Parse(k);
            if (!doc.RootElement.TryGetProperty("private_key", out var pkEl))
                throw new BadRequestException("El JSON de la cuenta de servicio no contiene private_key.");
            var extracted = pkEl.GetString();
            if (string.IsNullOrWhiteSpace(extracted))
                throw new BadRequestException("private_key vacío en el JSON.");
            return extracted;
        }
        catch (JsonException)
        {
            throw new BadRequestException("privateKey debe ser el PEM de la clave privada o el JSON completo de la cuenta de servicio.");
        }
    }

    private static string BuildMinimalServiceAccountJson(string clientEmail, string privateKeyPem)
    {
        var payload = new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "cloudkeep-connection-test",
            ["private_key_id"] = "connection-test",
            ["private_key"] = privateKeyPem,
            ["client_email"] = clientEmail,
            ["client_id"] = "0",
            ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
            ["auth_provider_x509_cert_url"] = "https://www.googleapis.com/oauth2/v1/certs"
        };
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>No persiste credenciales; solo indica si había valor almacenado.</summary>
    private static object SnapshotDestino(Destino d) => new
    {
        d.Id,
        d.Nombre,
        d.TipoDeDestino,
        d.IdCarpeta,
        d.AccessKeyId,
        secretAccessKeyAlmacenada = !string.IsNullOrEmpty(d.SecretAccessKey),
        d.BucketName,
        d.S3Region,
        d.GoogleServiceAccountEmail,
        privateKeyGoogleAlmacenada = !string.IsNullOrEmpty(d.GooglePrivateKey),
        d.AzureBlobContainerName,
        azureConnectionStringAlmacenada = !string.IsNullOrEmpty(d.AzureBlobConnectionString),
        d.CarpetaDestino,
        d.FechaCreacion,
        d.FechaModificacion
    };

    private static DestinoResponse MapToResponse(Destino d) => new(
        d.Id,
        d.Nombre,
        d.TipoDeDestino,
        d.IdCarpeta,
        d.AccessKeyId,
        !string.IsNullOrEmpty(d.SecretAccessKey),
        d.BucketName,
        d.S3Region,
        d.GoogleServiceAccountEmail,
        !string.IsNullOrEmpty(d.GooglePrivateKey),
        d.AzureBlobContainerName,
        !string.IsNullOrEmpty(d.AzureBlobConnectionString),
        d.CarpetaDestino,
        d.FechaCreacion,
        d.FechaModificacion
    );

    private async Task EnsureNombreUnicoAsync(string nombre, int? excludeId)
    {
        var exists = excludeId is null
            ? await _context.Destinos.AnyAsync(d => d.Nombre == nombre)
            : await _context.Destinos.AnyAsync(d => d.Nombre == nombre && d.Id != excludeId);

        if (exists)
            throw new ConflictException($"Ya existe un destino con el nombre '{nombre}'.");
    }

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} es obligatorio.");
    }

    private static string NormalizeAndValidateTipo(string tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            throw new BadRequestException("tipo es obligatorio.");

        var t = tipo.Trim();
        if (!DestinoTipos.Allowed.Contains(t))
            throw new BadRequestException(
                $"tipo debe ser '{DestinoTipos.S3}', '{DestinoTipos.GoogleDrive}' o '{DestinoTipos.AzureBlob}'.");

        return t;
    }

    private static string ValidateIdCarpetaGoogleDrive(string? idCarpeta)
    {
        if (string.IsNullOrWhiteSpace(idCarpeta))
            throw new BadRequestException("idCarpeta es obligatorio para Google Drive.");
        return idCarpeta.Trim();
    }

    private static bool HasS3CredentialPatch(UpdateDestinoRequest r) =>
        r.BucketName != null || r.Region != null || r.AccessKeyId != null || r.SecretAccessKey != null;

    private static bool HasGoogleCredentialPatch(UpdateDestinoRequest r) =>
        r.ServiceAccountEmail != null || r.PrivateKey != null;

    private static bool HasAzureCredentialPatch(UpdateDestinoRequest r) =>
        r.AzureBlobContainerName != null || r.AzureBlobConnectionString != null;

    private void ApplyS3CredentialsFromPatch(Destino entity, UpdateDestinoRequest r)
    {
        entity.GoogleServiceAccountEmail = string.Empty;
        entity.GooglePrivateKey = string.Empty;
        entity.AzureBlobContainerName = string.Empty;
        entity.AzureBlobConnectionString = string.Empty;

        var bucketTouched = r.BucketName != null;
        var regionTouched = r.Region != null;
        if (bucketTouched != regionTouched)
            throw new BadRequestException("bucketName y region deben enviarse juntos para S3 IAM.");

        var akRaw = r.AccessKeyId?.Trim() ?? string.Empty;
        var hasAk = !string.IsNullOrWhiteSpace(akRaw);

        if (hasAk)
        {
            if (string.IsNullOrWhiteSpace(r.BucketName) || string.IsNullOrWhiteSpace(r.Region))
                throw new BadRequestException("bucketName y region son obligatorios para S3 con claves de acceso.");
            entity.AccessKeyId = akRaw;
            entity.BucketName = r.BucketName.Trim();
            entity.S3Region = r.Region.Trim();
            if (r.SecretAccessKey != null)
            {
                entity.SecretAccessKey = string.IsNullOrWhiteSpace(r.SecretAccessKey)
                    ? string.Empty
                    : _credentialProtector.Protect(r.SecretAccessKey.Trim());
            }

            entity.Credenciales = _credentialProtector.Protect(BuildS3KeysCredencialesJson(entity.AccessKeyId));
            return;
        }

        if (bucketTouched && r.BucketName != null && r.Region != null)
        {
            if (string.IsNullOrWhiteSpace(r.BucketName) || string.IsNullOrWhiteSpace(r.Region))
                throw new BadRequestException("bucketName y region son obligatorios para S3 con IAM (Bucket y región).");
            entity.AccessKeyId = string.Empty;
            entity.SecretAccessKey = string.Empty;
            entity.BucketName = r.BucketName.Trim();
            entity.S3Region = r.Region.Trim();
            entity.Credenciales = _credentialProtector.Protect("{}");
            return;
        }

        if (r.SecretAccessKey != null && !string.IsNullOrWhiteSpace(entity.AccessKeyId))
        {
            entity.SecretAccessKey = string.IsNullOrWhiteSpace(r.SecretAccessKey)
                ? string.Empty
                : _credentialProtector.Protect(r.SecretAccessKey.Trim());
        }
    }

    private string GetGooglePrivateKeyPlaintextOrThrow(Destino entity)
    {
        if (string.IsNullOrWhiteSpace(entity.GooglePrivateKey))
            throw new BadRequestException(
                "privateKey es obligatoria para Google Drive (vuelve a pegar la clave si editaste el destino).");
        try
        {
            return _credentialProtector.Unprotect(entity.GooglePrivateKey);
        }
        catch (CryptographicException)
        {
            throw new BadRequestException(
                "No se pudo descifrar la clave privada almacenada. Guarda de nuevo la clave de la cuenta de servicio.");
        }
    }

    private void ApplyGoogleCredentialsFromPatch(Destino entity, UpdateDestinoRequest r)
    {
        entity.AccessKeyId = string.Empty;
        entity.SecretAccessKey = string.Empty;
        entity.BucketName = string.Empty;
        entity.S3Region = string.Empty;
        entity.AzureBlobContainerName = string.Empty;
        entity.AzureBlobConnectionString = string.Empty;
        entity.CarpetaDestino = string.Empty;

        if (r.ServiceAccountEmail != null)
            entity.GoogleServiceAccountEmail = r.ServiceAccountEmail.Trim();
        if (r.PrivateKey != null)
        {
            entity.GooglePrivateKey = string.IsNullOrWhiteSpace(r.PrivateKey)
                ? string.Empty
                : _credentialProtector.Protect(r.PrivateKey.Trim());
        }

        entity.Credenciales = _credentialProtector.Protect("{}");
    }

    private void ApplyAzureCredentialsFromPatch(Destino entity, UpdateDestinoRequest r)
    {
        entity.AccessKeyId = string.Empty;
        entity.SecretAccessKey = string.Empty;
        entity.BucketName = string.Empty;
        entity.S3Region = string.Empty;
        entity.GoogleServiceAccountEmail = string.Empty;
        entity.GooglePrivateKey = string.Empty;
        entity.IdCarpeta = string.Empty;

        if (r.AzureBlobContainerName != null)
            entity.AzureBlobContainerName = r.AzureBlobContainerName.Trim();
        if (r.AzureBlobConnectionString != null)
        {
            entity.AzureBlobConnectionString = string.IsNullOrWhiteSpace(r.AzureBlobConnectionString)
                ? string.Empty
                : _credentialProtector.Protect(r.AzureBlobConnectionString.Trim());
        }

        entity.Credenciales = _credentialProtector.Protect("{}");
    }

    private string GetAzureConnectionStringPlaintextOrThrow(Destino entity)
    {
        if (string.IsNullOrWhiteSpace(entity.AzureBlobConnectionString))
            throw new BadRequestException(
                "azureBlobConnectionString es obligatoria para Azure Blob Storage (vuelve a pegar la cadena si editaste el destino).");
        try
        {
            return _credentialProtector.Unprotect(entity.AzureBlobConnectionString);
        }
        catch (CryptographicException)
        {
            throw new BadRequestException(
                "No se pudo descifrar la cadena de conexión almacenada. Guarda de nuevo el destino.");
        }
    }

    private static string BuildS3KeysCredencialesJson(string accessKeyId) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["AccessKeyId"] = accessKeyId });
}
