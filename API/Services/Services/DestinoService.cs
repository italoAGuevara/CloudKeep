using System.Text.Json;
using API.Audit;
using API.DTOs;
using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        if (tipo == DestinoTipos.S3)
        {
            var ak = request.AccessKeyId?.Trim() ?? string.Empty;
            var hasKeys = !string.IsNullOrWhiteSpace(ak);
            if (hasKeys)
            {
                if (string.IsNullOrWhiteSpace(request.SecretAccessKey))
                    throw new BadRequestException("secretAccessKey es obligatorio para S3 con claves de acceso.");
                accessKeyId = ak;
                secretEnc = _credentialProtector.Protect(request.SecretAccessKey.Trim());
                blobPlain = BuildS3KeysCredencialesJson(accessKeyId);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.BucketName) || string.IsNullOrWhiteSpace(request.Region))
                    throw new BadRequestException("bucketName y region son obligatorios para S3 con IAM (Bucket y región).");
                bucketName = request.BucketName.Trim();
                s3Region = request.Region.Trim();
            }
        }
        else if (tipo == DestinoTipos.GoogleDrive)
        {
            if (string.IsNullOrWhiteSpace(request.ServiceAccountEmail))
                throw new BadRequestException("serviceAccountEmail es obligatorio para Google Drive.");
            if (string.IsNullOrWhiteSpace(request.PrivateKey))
                throw new BadRequestException("privateKey es obligatorio para Google Drive.");
            googleServiceEmail = request.ServiceAccountEmail.Trim();
            googlePrivateEnc = _credentialProtector.Protect(request.PrivateKey.Trim());
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
            GooglePrivateKey = googlePrivateEnc
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

        if (request.IdCarpeta is not null)
            entity.IdCarpeta = request.IdCarpeta.Trim();

        if (entity.TipoDeDestino == DestinoTipos.S3)
        {
            entity.IdCarpeta = string.Empty;
            entity.GoogleServiceAccountEmail = string.Empty;
            entity.GooglePrivateKey = string.Empty;
        }
        else
        {
            entity.AccessKeyId = string.Empty;
            entity.SecretAccessKey = string.Empty;
            entity.BucketName = string.Empty;
            entity.S3Region = string.Empty;
            if (string.IsNullOrWhiteSpace(entity.IdCarpeta))
                throw new BadRequestException("idCarpeta es obligatorio para Google Drive.");
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
        var antes = SnapshotDestino(entity);
        _context.Destinos.Remove(entity);
        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Destino, AccionLog.Delete, antes, null);
        return true;
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
            throw new BadRequestException($"tipo debe ser '{DestinoTipos.S3}' o '{DestinoTipos.GoogleDrive}'.");

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

    private void ApplyS3CredentialsFromPatch(Destino entity, UpdateDestinoRequest r)
    {
        entity.GoogleServiceAccountEmail = string.Empty;
        entity.GooglePrivateKey = string.Empty;

        var bucketTouched = r.BucketName != null;
        var regionTouched = r.Region != null;
        if (bucketTouched != regionTouched)
            throw new BadRequestException("bucketName y region deben enviarse juntos para S3 IAM.");

        var akRaw = r.AccessKeyId?.Trim() ?? string.Empty;
        var hasAk = !string.IsNullOrWhiteSpace(akRaw);

        if (hasAk)
        {
            entity.AccessKeyId = akRaw;
            entity.BucketName = string.Empty;
            entity.S3Region = string.Empty;
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

    private void ApplyGoogleCredentialsFromPatch(Destino entity, UpdateDestinoRequest r)
    {
        entity.AccessKeyId = string.Empty;
        entity.SecretAccessKey = string.Empty;
        entity.BucketName = string.Empty;
        entity.S3Region = string.Empty;

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

    private static string BuildS3KeysCredencialesJson(string accessKeyId) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["AccessKeyId"] = accessKeyId });
}
