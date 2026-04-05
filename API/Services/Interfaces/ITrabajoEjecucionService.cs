using API.DTOs;

namespace API.Services.Interfaces;

public interface ITrabajoEjecucionService
{
    /// <summary>Ejecuta la copia del origen al destino (S3 o Google Drive) usando la configuración persistida.</summary>
    Task<EjecutarTrabajoResponse> EjecutarManualAsync(int trabajoId, CancellationToken cancellationToken = default);
}
