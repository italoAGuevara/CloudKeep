using HostedService.Entities;

namespace HostedService.Backup;

/// <summary>
/// Límites opcionales para incluir archivos en la copia (después de exclusiones por ruta/nombre).
/// Las fechas deben interpretarse en UTC respecto a los metadatos del archivo en disco.
/// </summary>
public sealed class CopiaArchivosFiltros
{
    public long? TamanoMinBytes { get; init; }
    public long? TamanoMaxBytes { get; init; }
    public DateTime? FechaCreacionDesdeUtc { get; init; }
    public DateTime? FechaCreacionHastaUtc { get; init; }
    public DateTime? FechaActualizacionDesdeUtc { get; init; }
    public DateTime? FechaActualizacionHastaUtc { get; init; }

    public bool PermiteArchivo(long tamanoBytes, DateTime creacionUtc, DateTime actualizacionUtc)
    {
        if (TamanoMinBytes is { } minTam && tamanoBytes < minTam)
            return false;
        if (TamanoMaxBytes is { } maxTam && tamanoBytes > maxTam)
            return false;
        if (FechaCreacionDesdeUtc is { } c0 && creacionUtc < c0)
            return false;
        if (FechaCreacionHastaUtc is { } c1 && creacionUtc > c1)
            return false;
        if (FechaActualizacionDesdeUtc is { } m0 && actualizacionUtc < m0)
            return false;
        if (FechaActualizacionHastaUtc is { } m1 && actualizacionUtc > m1)
            return false;
        return true;
    }

    public static CopiaArchivosFiltros? FromTrabajo(Trabajo trabajo)
    {
        if (trabajo.CopiaTamanoMinBytes is null
            && trabajo.CopiaTamanoMaxBytes is null
            && trabajo.CopiaCreacionDesdeUtc is null
            && trabajo.CopiaCreacionHastaUtc is null
            && trabajo.CopiaActualizacionDesdeUtc is null
            && trabajo.CopiaActualizacionHastaUtc is null)
            return null;

        return new CopiaArchivosFiltros
        {
            TamanoMinBytes = trabajo.CopiaTamanoMinBytes,
            TamanoMaxBytes = trabajo.CopiaTamanoMaxBytes,
            FechaCreacionDesdeUtc = trabajo.CopiaCreacionDesdeUtc,
            FechaCreacionHastaUtc = trabajo.CopiaCreacionHastaUtc,
            FechaActualizacionDesdeUtc = trabajo.CopiaActualizacionDesdeUtc,
            FechaActualizacionHastaUtc = trabajo.CopiaActualizacionHastaUtc
        };
    }
}
