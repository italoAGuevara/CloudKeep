using API.DTOs;
using API.Services.Interfaces;
using HostedService.Entities;
using HostedService.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Services;

public sealed class JobExecutionReportService : IJobExecutionReportService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;

    public JobExecutionReportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EjecucionHistorialListResponse> GetHistorialAsync(
        int? trabajoId,
        string? ejecutadoPor,
        DateTime? desdeUtc,
        DateTime? hastaUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query =
            from h in _context.HistoryBackupExecutions.AsNoTracking()
            join t in _context.Trabajos.AsNoTracking() on h.TrabajoId equals t.Id into tg
            from t in tg.DefaultIfEmpty()
            select new { h, Nombre = t != null ? t.Nombre : "(trabajo eliminado)" };

        if (trabajoId is > 0)
            query = query.Where(x => x.h.TrabajoId == trabajoId.Value);

        if (!string.IsNullOrWhiteSpace(ejecutadoPor))
        {
            var actor = ejecutadoPor.Trim();
            query = query.Where(x => x.h.EjecutadoPor == actor);
        }

        if (desdeUtc is not null)
            query = query.Where(x => x.h.StartTime >= desdeUtc.Value);

        if (hastaUtc is not null)
            query = query.Where(x => x.h.StartTime <= hastaUtc.Value);

        var total = await query.CountAsync(cancellationToken);

        var pageRows = await query
            .OrderByDescending(x => x.h.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows.Select(x => MapItem(x.h, x.Nombre)).ToList();
        return new EjecucionHistorialListResponse(items, total);
    }

    private static EjecucionHistorialItemResponse MapItem(HistoryBackupExecutions h, string trabajoNombre)
    {
        double? duracion = h.Status == BackupStatus.InProgress
            ? null
            : Math.Max(0, (h.EndTime - h.StartTime).TotalSeconds);

        var estado = h.Status switch
        {
            BackupStatus.Completed => "completado",
            BackupStatus.Failed => "fallido",
            BackupStatus.InProgress => "en_progreso",
            BackupStatus.Pending => "pendiente",
            _ => h.Status.ToString().ToLowerInvariant()
        };

        var disparo = h.Trigger == JobExecutionTrigger.Programada ? "programada" : "manual";

        return new EjecucionHistorialItemResponse(
            h.Id,
            h.TrabajoId,
            trabajoNombre,
            h.StartTime,
            h.EndTime,
            duracion,
            estado,
            h.ArchivosCopiados,
            disparo,
            h.EjecutadoPor,
            h.ErrorMessage);
    }
}
