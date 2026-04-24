using API.DTOs;
using HostedService.Enums;

namespace API.Services.Interfaces;

public interface ITrabajoEjecucionService
{
    /// <summary>Ejecuta la copia del origen al destino en la nube usando la configuración persistida (lógica en HostedService).</summary>
    Task<EjecutarTrabajoResponse> EjecutarManualAsync(
        int trabajoId,
        CancellationToken cancellationToken = default,
        JobExecutionTrigger trigger = JobExecutionTrigger.Manual,
        string? ejecutadoPor = null);
}
