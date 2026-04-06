using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using HostedService.Entities;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace HostedService.Backup;

public sealed class DestinoToCloudCopier : IDestinoToCloudCopier
{
    /// <inheritdoc />
    public async Task<int> CopyOrigenToDestinoAsync(
        string rootPath,
        string filtrosExclusiones,
        Destino destino,
        string trabajoNombre,
        Func<string, string> unprotectSecret,
        CancellationToken cancellationToken = default)
    {
        var exclusionPatterns = ParseExclusionPatterns(filtrosExclusiones);

        if (string.Equals(destino.TipoDeDestino, DestinoTipos.S3, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(destino.CarpetaDestino))
                throw new DestinoCopyException(
                    "El destino S3 no tiene carpeta destino configurada. Edita el destino e indica el prefijo dentro del bucket.");
            return await CopyToS3Async(
                rootPath,
                destino,
                destino.CarpetaDestino,
                exclusionPatterns,
                unprotectSecret,
                cancellationToken);
        }

        if (string.Equals(destino.TipoDeDestino, DestinoTipos.GoogleDrive, StringComparison.Ordinal))
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var jobSegment = SanitizePathSegment(trabajoNombre);
            return await CopyToGoogleDriveAsync(
                rootPath,
                destino,
                $"{jobSegment}-{stamp}",
                exclusionPatterns,
                unprotectSecret,
                cancellationToken);
        }

        if (string.Equals(destino.TipoDeDestino, DestinoTipos.AzureBlob, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(destino.CarpetaDestino))
                throw new DestinoCopyException(
                    "El destino Azure Blob no tiene carpeta destino configurada. Edita el destino e indica el prefijo dentro del contenedor.");
            return await CopyToAzureBlobAsync(
                rootPath,
                destino,
                destino.CarpetaDestino,
                exclusionPatterns,
                unprotectSecret,
                cancellationToken);
        }

        throw new DestinoCopyException($"Tipo de destino no soportado: «{destino.TipoDeDestino}».");
    }

    private async Task<int> CopyToS3Async(
        string rootPath,
        Destino destino,
        string keyPrefix,
        IReadOnlyList<string> exclusionPatterns,
        Func<string, string> unprotectSecret,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destino.BucketName))
            throw new DestinoCopyException("El destino S3 no tiene bucket configurado.");
        if (string.IsNullOrWhiteSpace(destino.S3Region))
            throw new DestinoCopyException("El destino S3 no tiene región configurada.");

        RegionEndpoint regionEndpoint;
        try
        {
            regionEndpoint = RegionEndpoint.GetBySystemName(destino.S3Region.Trim());
        }
        catch (ArgumentException)
        {
            throw new DestinoCopyException($"Región AWS no reconocida: «{destino.S3Region}».");
        }

        var config = new AmazonS3Config { RegionEndpoint = regionEndpoint };
        AmazonS3Client client;
        try
        {
            if (!string.IsNullOrWhiteSpace(destino.AccessKeyId))
            {
                if (string.IsNullOrWhiteSpace(destino.SecretAccessKey))
                    throw new DestinoCopyException("El destino S3 tiene Access Key pero falta el secreto almacenado.");
                var secret = unprotectSecret(destino.SecretAccessKey);
                client = new AmazonS3Client(
                    new BasicAWSCredentials(destino.AccessKeyId.Trim(), secret),
                    config);
            }
            else
            {
                client = new AmazonS3Client(config);
            }
        }
        catch (CryptographicException)
        {
            throw new DestinoCopyException(
                "No se pudo descifrar las credenciales S3 del destino. Vuelve a guardar el destino.");
        }

        using (client)
        {
            var bucket = destino.BucketName.Trim();
            var count = 0;
            foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(rootPath, filePath);
                if (ShouldExclude(relative, exclusionPatterns))
                    continue;

                var key = keyPrefix + NormalizeS3Key(relative);
                try
                {
                    await using var fs = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 1024 * 64,
                        options: FileOptions.Asynchronous);
                    await client.PutObjectAsync(
                        new PutObjectRequest
                        {
                            BucketName = bucket,
                            Key = key,
                            InputStream = fs,
                            ContentType = GuessContentType(filePath)
                        },
                        cancellationToken);
                    count++;
                }
                catch (AmazonS3Exception ex)
                {
                    throw new DestinoCopyException($"Error al subir a S3 («{key}»): {DescribeS3PutFailure(ex)}");
                }
            }

            return count;
        }
    }

    private static string DescribeS3PutFailure(AmazonS3Exception ex)
    {
        var code = ex.ErrorCode;
        if (string.Equals(code, "AccessDenied", StringComparison.OrdinalIgnoreCase))
            return "Acceso denegado. Revisa permisos IAM (p. ej. s3:PutObject) sobre el bucket y el prefijo.";
        return !string.IsNullOrWhiteSpace(code) ? $"{code}: {ex.Message}" : ex.Message;
    }

    private async Task<int> CopyToAzureBlobAsync(
        string rootPath,
        Destino destino,
        string blobPrefix,
        IReadOnlyList<string> exclusionPatterns,
        Func<string, string> unprotectSecret,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destino.AzureBlobContainerName))
            throw new DestinoCopyException("El destino Azure Blob no tiene contenedor configurado.");

        string connectionPlain;
        try
        {
            if (string.IsNullOrWhiteSpace(destino.AzureBlobConnectionString))
                throw new DestinoCopyException("El destino Azure Blob no tiene cadena de conexión almacenada.");
            connectionPlain = unprotectSecret(destino.AzureBlobConnectionString);
        }
        catch (CryptographicException)
        {
            throw new DestinoCopyException(
                "No se pudo descifrar la cadena de conexión del destino Azure. Vuelve a guardar el destino.");
        }

        BlobServiceClient service;
        try
        {
            service = new BlobServiceClient(connectionPlain);
        }
        catch (Exception ex)
        {
            throw new DestinoCopyException($"Cadena de conexión de Azure no válida: {ex.Message}");
        }

        var container = service.GetBlobContainerClient(destino.AzureBlobContainerName.Trim());
        var count = 0;
        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(rootPath, filePath);
            if (ShouldExclude(relative, exclusionPatterns))
                continue;

            var blobName = blobPrefix + NormalizeS3Key(relative);
            try
            {
                await using var fs = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 64,
                    options: FileOptions.Asynchronous);
                var blob = container.GetBlobClient(blobName);
                await blob.UploadAsync(
                    fs,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = GuessContentType(filePath) }
                    },
                    cancellationToken);
                count++;
            }
            catch (Azure.RequestFailedException ex)
            {
                throw new DestinoCopyException(
                    $"Error al subir a Azure Blob («{blobName}»): {DescribeAzureBlobPutFailure(ex)}");
            }
        }

        return count;
    }

    private static string DescribeAzureBlobPutFailure(Azure.RequestFailedException ex)
    {
        if (ex.Status == 403)
            return "Acceso denegado. Revisa permisos de escritura en el contenedor.";
        if (ex.ErrorCode is { Length: > 0 } code)
            return $"{code}: {ex.Message}";
        return ex.Message;
    }

    private async Task<int> CopyToGoogleDriveAsync(
        string rootPath,
        Destino destino,
        string runFolderName,
        IReadOnlyList<string> exclusionPatterns,
        Func<string, string> unprotectSecret,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destino.IdCarpeta))
            throw new DestinoCopyException("El destino Google Drive no tiene idCarpeta configurado.");
        if (string.IsNullOrWhiteSpace(destino.GoogleServiceAccountEmail))
            throw new DestinoCopyException("El destino Google Drive no tiene cuenta de servicio configurada.");

        string privateKeyPlain;
        try
        {
            if (string.IsNullOrWhiteSpace(destino.GooglePrivateKey))
                throw new DestinoCopyException("Falta la clave privada de la cuenta de servicio en el destino.");
            privateKeyPlain = unprotectSecret(destino.GooglePrivateKey);
        }
        catch (CryptographicException)
        {
            throw new DestinoCopyException(
                "No se pudo descifrar la clave de Google Drive. Vuelve a guardar el destino con la clave privada.");
        }

        var pk = NormalizeGooglePrivateKeyInput(privateKeyPlain);
        var json = BuildMinimalServiceAccountJson(destino.GoogleServiceAccountEmail.Trim(), pk);
        GoogleCredential credential;
        try
        {
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            credential = await GoogleCredential.FromStreamAsync(ms, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new DestinoCopyException($"Credenciales de cuenta de servicio no válidas: {ex.Message}");
        }

        credential = credential.CreateScoped(DriveService.Scope.Drive);

        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "portafolio-v-1-2",
        });

        var parentId = destino.IdCarpeta.Trim();
        var runFolderId = await CreateDriveFolderAsync(service, runFolderName, parentId, cancellationToken);
        var folderCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = runFolderId
        };

        var count = 0;
        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(rootPath, filePath);
            if (ShouldExclude(relative, exclusionPatterns))
                continue;

            var dirPart = Path.GetDirectoryName(relative);
            var normalizedDir = NormalizeRelativeDir(dirPart);
            var parentForFile = await ResolveDriveParentFolderAsync(
                service,
                normalizedDir,
                runFolderId,
                folderCache,
                cancellationToken);

            var fileName = Path.GetFileName(relative);
            try
            {
                await UploadDriveFileAsync(service, filePath, fileName, parentForFile, cancellationToken);
                count++;
            }
            catch (Google.GoogleApiException ex)
            {
                var detail = ex.Error?.Message ?? ex.Message;
                throw new DestinoCopyException(
                    $"Error en Google Drive al subir «{relative}». Comprueba que la carpeta esté compartida con la cuenta de servicio (Editor). Detalle: {detail}");
            }
        }

        return count;
    }

    private static async Task<string> CreateDriveFolderAsync(
        DriveService service,
        string name,
        string parentId,
        CancellationToken cancellationToken)
    {
        var meta = new DriveFile
        {
            Name = name,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new List<string> { parentId }
        };
        var req = service.Files.Create(meta);
        req.Fields = "id";
        req.SupportsAllDrives = true;
        var created = await req.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return created.Id ?? throw new DestinoCopyException("Google Drive no devolvió el id de la carpeta de ejecución.");
    }

    private static async Task<string> ResolveDriveParentFolderAsync(
        DriveService service,
        string normalizedRelativeDir,
        string runRootId,
        Dictionary<string, string> folderCache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(normalizedRelativeDir))
            return runRootId;

        if (folderCache.TryGetValue(normalizedRelativeDir, out var cached))
            return cached;

        var slash = normalizedRelativeDir.LastIndexOf('/');
        string parentKey;
        string segment;
        if (slash < 0)
        {
            parentKey = string.Empty;
            segment = normalizedRelativeDir;
        }
        else
        {
            parentKey = normalizedRelativeDir[..slash];
            segment = normalizedRelativeDir[(slash + 1)..];
        }

        var parentId = await ResolveDriveParentFolderAsync(service, parentKey, runRootId, folderCache, cancellationToken);
        var newId = await CreateDriveFolderAsync(service, segment, parentId, cancellationToken);
        folderCache[normalizedRelativeDir] = newId;
        return newId;
    }

    private static async Task UploadDriveFileAsync(
        DriveService service,
        string filePath,
        string displayName,
        string parentId,
        CancellationToken cancellationToken)
    {
        var meta = new DriveFile { Name = displayName, Parents = new List<string> { parentId } };
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 64,
            options: FileOptions.Asynchronous);
        var contentType = GuessContentType(filePath);
        var create = service.Files.Create(meta, stream, contentType);
        create.Fields = "id";
        create.SupportsAllDrives = true;
        var progress = await create.UploadAsync(cancellationToken).ConfigureAwait(false);
        if (progress.Status != UploadStatus.Completed)
            throw new DestinoCopyException($"La subida a Drive no completó (estado: {progress.Status}).");
    }

    private static string NormalizeRelativeDir(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return string.Empty;
        var parts = dir.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/' },
            StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', parts);
    }

    private static string NormalizeS3Key(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Join('/', parts.Select(p => p.Trim()));
    }

    private static string SanitizePathSegment(string name)
    {
        var trimmed = name.Trim();
        var chars = trimmed.Where(static c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray();
        return chars.Length == 0 ? "trabajo" : new string(chars);
    }

    private static IReadOnlyList<string> ParseExclusionPatterns(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();
    }

    private static bool ShouldExclude(string relativePath, IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
            return false;
        var rel = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(rel);
        foreach (var p in patterns)
        {
            if (fileName.Contains(p, StringComparison.OrdinalIgnoreCase))
                return true;
            if (rel.Contains(p, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".zip" => "application/zip",
            ".7z" => "application/x-7z-compressed",
            _ => "application/octet-stream"
        };
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
                throw new DestinoCopyException("El JSON de la cuenta de servicio no contiene private_key.");
            var extracted = pkEl.GetString();
            if (string.IsNullOrWhiteSpace(extracted))
                throw new DestinoCopyException("private_key vacío en el JSON.");
            return extracted;
        }
        catch (JsonException)
        {
            throw new DestinoCopyException(
                "privateKey debe ser el PEM de la clave privada o el JSON completo de la cuenta de servicio.");
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
}
