using API.DTOs;
using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Backup;
using HostedService.Entities;
using HostedService.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Services;

public class TrabajoEjecucionService : ITrabajoEjecucionService
{
    private readonly AppDbContext _context;
    private readonly IDestinoCredentialProtector _credentialProtector;
    private readonly IDestinoToCloudCopier _destinoCopier;
    private readonly ILogger<TrabajoEjecucionService> _logger;

    public TrabajoEjecucionService(
        AppDbContext context,
        IDestinoCredentialProtector credentialProtector,
        IDestinoToCloudCopier destinoCopier,
        ILogger<TrabajoEjecucionService> logger)
    {
        _context = context;
        _credentialProtector = credentialProtector;
        _destinoCopier = destinoCopier;
        _logger = logger;
    }

    public async Task<EjecutarTrabajoResponse> EjecutarManualAsync(int trabajoId, CancellationToken cancellationToken = default)
    {
        var claimed = await _context.Trabajos
            .Where(t => t.Id == trabajoId && !t.Procesando)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.Procesando, true)
                    .SetProperty(t => t.FechaModificacion, DateTime.UtcNow),
                cancellationToken);

        if (claimed == 0)
        {
            var exists = await _context.Trabajos.AnyAsync(t => t.Id == trabajoId, cancellationToken);
            if (!exists)
                throw new NotFoundException($"Trabajo con Id '{trabajoId}' no existe");
            throw new ConflictException("Este trabajo ya se está ejecutando. Espera a que termine.");
        }

        var trabajo = await _context.Trabajos
            .AsNoTracking()
            .Include(t => t.TrabajosOrigenDestino)
            .ThenInclude(l => l.Origen)
            .Include(t => t.TrabajosOrigenDestino)
            .ThenInclude(l => l.Destino)
            .FirstAsync(t => t.Id == trabajoId, cancellationToken);

        var origen = trabajo.TrabajosOrigenDestino.Origen;
        var destino = trabajo.TrabajosOrigenDestino.Destino;
        var rootPath = Path.GetFullPath(origen.Ruta.Trim());

        var history = new HistoryBackupExecutions
        {
            TrabajoId = trabajoId,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            Status = BackupStatus.InProgress,
            ErrorMessage = null
        };
        _context.HistoryBackupExecutions.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            if (!Directory.Exists(rootPath))
                throw new BadRequestException($"La ruta de origen no existe o no es una carpeta: «{rootPath}».");

            int copied;
            try
            {
                copied = await _destinoCopier.CopyOrigenToDestinoAsync(
                    rootPath,
                    origen.FiltrosExclusiones,
                    destino,
                    trabajo.Nombre,
                    s => _credentialProtector.Unprotect(s),
                    cancellationToken);
            }
            catch (DestinoCopyException ex)
            {
                throw new BadRequestException(ex.Message);
            }

            await _context.HistoryBackupExecutions
                .Where(h => h.Id == history.Id)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(h => h.Status, BackupStatus.Completed)
                        .SetProperty(h => h.EndTime, DateTime.UtcNow)
                        .SetProperty(h => h.ErrorMessage, (string?)null),
                    cancellationToken);

            var mensaje = copied == 0
                ? "Ejecución finalizada; no había archivos para copiar (o todos fueron excluidos por filtros)."
                : $"Ejecución correcta. Archivos copiados: {copied}.";

            return new EjecutarTrabajoResponse(history.Id, copied, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al ejecutar trabajo {TrabajoId}", trabajoId);
            var msg = ex is BadRequestException or NotFoundException or ConflictException
                ? ex.Message
                : $"Error inesperado: {ex.Message}";
            await _context.HistoryBackupExecutions
                .Where(h => h.Id == history.Id)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(h => h.Status, BackupStatus.Failed)
                        .SetProperty(h => h.EndTime, DateTime.UtcNow)
                        .SetProperty(h => h.ErrorMessage, msg),
                    cancellationToken);
            throw;
        }
        finally
        {
            await _context.Trabajos
                .Where(t => t.Id == trabajoId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(t => t.Procesando, false)
                        .SetProperty(t => t.FechaModificacion, DateTime.UtcNow),
                    cancellationToken);
        }
    }
}
