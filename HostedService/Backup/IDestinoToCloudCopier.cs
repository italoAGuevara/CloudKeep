using HostedService.Entities;

namespace HostedService.Backup;

/// <summary>
/// Copia recursiva de una carpeta local hacia S3, Google Drive o Azure Blob según <see cref="Destino.TipoDeDestino"/>.
/// </summary>
public interface IDestinoToCloudCopier
{
    /// <param name="unprotectSecret">Descifra valores protegidos almacenados en el destino (p. ej. Data Protection en la API).</param>
    Task<int> CopyOrigenToDestinoAsync(
        string rootPath,
        string filtrosExclusiones,
        Destino destino,
        string trabajoNombre,
        Func<string, string> unprotectSecret,
        CancellationToken cancellationToken = default);
}
