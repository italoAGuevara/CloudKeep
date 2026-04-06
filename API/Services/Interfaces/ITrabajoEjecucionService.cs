using API.DTOs;

namespace API.Services.Interfaces;

public interface ITrabajoEjecucionService
{
    /// <summary>Ejecuta la copia del origen al destino en la nube usando la configuración persistida (lógica en HostedService).</summary>
    Task<EjecutarTrabajoResponse> EjecutarManualAsync(int trabajoId, CancellationToken cancellationToken = default);
}
