using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Enums;
using HostedService.Scheduling;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Scheduling;

public sealed class TrabajoCronTickHandler : ITrabajoCronTickHandler
{
    private readonly AppDbContext _context;
    private readonly ITrabajoEjecucionService _ejecucion;
    private readonly ICronMinutoEjecucionTracker _minutoTracker;
    private readonly ILogger<TrabajoCronTickHandler> _logger;

    public TrabajoCronTickHandler(
        AppDbContext context,
        ITrabajoEjecucionService ejecucion,
        ICronMinutoEjecucionTracker minutoTracker,
        ILogger<TrabajoCronTickHandler> logger)
    {
        _context = context;
        _ejecucion = ejecucion;
        _minutoTracker = minutoTracker;
        _logger = logger;
    }

    public async Task ProcessDueJobsAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        var candidatos = await _context.Trabajos
            .AsNoTracking()
            .Where(t => t.Activo && !t.Procesando)
            .Select(t => new { t.Id, t.CronExpression, t.Nombre })
            .ToListAsync(cancellationToken);

        foreach (var t in candidatos)
        {
            if (!CronScheduleUtc.IsDueThisUtcMinute(t.CronExpression, utcNow))
                continue;

            if (!_minutoTracker.TryBeginEjecucionEsteMinutoUtc(t.Id, utcNow))
                continue;

            try
            {
                _logger.LogInformation(
                    "Ejecución programada (cron): trabajo {TrabajoId} «{Nombre}»",
                    t.Id, t.Nombre);

                await _ejecucion.EjecutarManualAsync(
                    t.Id,
                    cancellationToken,
                    JobExecutionTrigger.Programada,
                    "Sistema");
            }
            catch (ConflictException)
            {
                // Otro hilo o petición manual tomó Procesando antes de llegar aquí.
            }
            catch (BadRequestException ex)
            {
                _logger.LogWarning(ex, "Trabajo programado {TrabajoId}: validación o copia rechazada.", t.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar trabajo programado {TrabajoId}.", t.Id);
            }
        }
    }
}
